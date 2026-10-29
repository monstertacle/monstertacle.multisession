# Native Provider Contract v1

**Datum**: 2026-10-27 | **Basis**: [research.md](../research.md), [public-api.md](public-api.md)

**Erneute Designprüfung**: Constitution v1.6.0 verlangt vor jeder Adapterimplementierung
die passenden ausführbaren fehlschlagenden HTTP-/Sitzungsvertragstests aus TDD-05.
Native Providergrenze und unveränderter Funktionsumfang bleiben bestehen. Live-Zugänge
sind für den ersten Red-Nachweis nicht erforderlich und ersetzen ihn nicht.

## 1. Gemeinsame Schnittstelle

`ISessionProvider` ist ein schmaler .NET-Vertrag für vom Host bereitgestellte Adapter.
Vertragstypen liegen in Common. Keine CLI-, HTTP-, SDK- oder Herstellerklassen im Kern.

| Operation | Semantik |
| --- | --- |
| OpenAsync | Native Sitzung eröffnen, falls separat möglich; andernfalls explizites noch ungebundenes Binding zurückgeben |
| ReopenAsync | Persistierten nativen Bezug prüfen und wiederverwenden; niemals still neu eröffnen oder Historie replayen |
| SendAsync | Nur neue Eingabe plus unveränderliche Profileinstellungen und letzten bestätigten Binding-Stand übergeben |
| InspectAsync | Bekannte externe Operationskennung lesen; keine erneute Ausführung und kein eigener Agent-Schritt |
| CancelAsync | Native Abbruchanforderung, soweit unterstützt; Bestätigung und tatsächlichen Endzustand unterscheiden |

SendAsync meldet eine erhaltene externe Operationskennung vor dem weiteren Polling an
einen begrenzten Awaitable-Persistenzcallback. Dessen erfolgreicher Abschluss bestätigt,
dass die Kennung gespeichert wurde. Kann sie nicht gespeichert werden, darf Erfolg nicht
ausgegeben werden; der Kern erhält Unknown und hält die Reservierung.

ProviderReply enthält endgültige Ausgabe, Ergebnis-/Fehlerkategorie, bestätigte Gewissheit
und optional aktualisierte versionierte Binding-Daten. Der Kern sichert Ergebnis und
Fortsetzungsstand gemeinsam. Provider dürfen keine Datenbank des Kerns direkt verändern.

Provider müssen kooperative Cancellation und endliche Netzwerkfristen beachten.
Ein nicht kooperativer Adapter ist eine Vertragsverletzung, keine implizit vorhandene
Sicherheits-Sandbox. Der Kern darf nach lokalem Deadline-Ende Unknown melden und muss
weitere Dispatches in Sitzung/Workspace verhindern, statt einen Callback als beendet auszugeben.
Der Adapter prüft vor Übergabe nur lokal bekannte Vertrags-/Konfigurationsfehler.
Erst beim Hersteller erkennbare Funktionsgrenzen sind zugeordnete Providerfehler,
keine Aufforderung zur vollständigen Feature-Erkundung oder zum Nachbau einer Ersatzfunktion.

Übersteigt eine Ausgabe das konfigurierte Größenlimit, darf der Adapter keine gekürzte
Antwort als vollständigen Erfolg melden. Er liefert OutputLimitExceeded mit leerem
Output und OutputComplete=false. Ein dadurch abgebrochener Empfang ist kein Beweis,
dass externe Arbeit beendet ist; Gewissheit und Reservierung folgen dem Recovery-Vertrag.

## 2. Identität und Einstellungen

- Lokale SessionId bleibt immer stabil.
- Native SessionReference darf zunächst fehlen, sofern noch nie gesendet wurde.
- Native OperationId bezeichnet genau einen externen Aufruf, nicht die Sitzung.
- ContinuationState darf nach bestätigtem Abschluss fortgeschrieben werden; dies ist
  kein Provider-/Modellwechsel und kein Austausch der lokalen Sitzungsidentität.
- Agent-Profil, Modell und kontextrelevante Einstellungen sind beim Erstellen eingefroren.
  Credentials werden über eine Referenz frisch aus dem Host bezogen, nicht im Snapshot gespeichert.
- Ein geändertes Fortsetzungsformat benötigt eine explizite Vertrags-/Speichermigration;
  unlesbare ältere/neue Bindings führen zum Fehler statt zu einem leeren Neustart.
  In der ersten Lieferung werden solche Migrationen nicht automatisch vorgenommen:
  Nur die bekannte Formatversion wird geöffnet, andere werden ohne Änderung abgewiesen.

## 3. OpenAI-Referenzadapter

**Protokoll**: HTTPS JSON, OpenAI REST `/v1`, Basis `https://api.openai.com/v1`.
Bearer-Credentials ausschließlich über den Host. Konfigurierbare Basis-URI muss denselben
Conversations/Responses-Vertrag anbieten; Chat-Completions-Kompatibilität reicht nicht aus.

| Ablauf | Konkreter Vertrag |
| --- | --- |
| Open | `POST /conversations`, Conversation-ID sichern |
| Reopen | `GET /conversations/{conversation_id}`; fehlender Bezug ist ProviderSessionExpired |
| Send | `POST /responses` mit model, conversation, ausschließlich neuer input, background=true und store=true |
| Inspect | `GET /responses/{response_id}` |
| Cancel | `POST /responses/{response_id}/cancel`, anschließend Endzustand prüfen |

Die gewählte Conversation-ID wird nicht durch eine Response-ID ersetzt.
`previous_response_id` wird NICHT gleichzeitig mit conversation verwendet.
Queued/in_progress bedeutet laufend; completed ist nur ohne erforderliche clientseitige
Aktion ein erfolgreicher Abschluss. Failed, incomplete und cancelled sind gesonderte Ergebnisse.
Gültige Antwort ohne Text beziehungsweise mit clientseitigem Funktionsaufruf wird nicht
als erfundene Textantwort oder Anlass für eine eigene Werkzeugschleife behandelt.

API-Versionierung folgt dem REST-Vertrag `/v1`; der konkrete Request-/Responseumfang
und dokumentierte Felder werden über eingecheckte Vertragsfixtures eingefroren.
Modelle sind explizite Hostkonfiguration, keine hart codierte Modellliste.

Conversation-Speicherung hebt weder Modellkontextgrenzen noch eventuelle Lebensdauergrenzen
anderer Providerressourcen auf. Die Bibliothek ruft keinen eigenen Compaction-Ablauf auf.
Providerseitig schon konfigurierte automatische Kontextfunktionen bleiben providerseitig.

## 4. Gemini-Referenzadapter

**Protokoll**: HTTPS JSON, stabile Interactions API `/v1`, Basis
`https://generativelanguage.googleapis.com/v1`; Host liefert `x-goog-api-key`.
Keine Vermischung mit alten `/v1beta`-/Api-Revision-Beispielen.

| Ablauf | Konkreter Vertrag |
| --- | --- |
| Open | Nur lokale Bindung vorbereiten; die erste Nachricht erzeugt die native Interaktion |
| Erster Send | `POST /interactions` mit model, neuer input, background=true und store=true |
| Weiterer Send | Gleicher Pfad, zusätzlich previous_interaction_id des letzten bestätigten Abschlusses |
| Reopen / Inspect | `GET /interactions/{interaction_id}`; gespeicherten Kopf prüfen |
| Cancel | `POST /interactions/{interaction_id}/cancel`; spätere Statusbestätigung abwarten |

Erste bestätigte native Interaktion und jeweiliger Chain-Head werden getrennt gespeichert.
Eine Interaktion im Zustand in_progress darf nicht als bestätigter Fortsetzungskopf
verwendet werden. Nach Crash mit ungewissem neuem Kopf kein stilles Branching vom alten Kopf.

Die native Kette enthält Geschichte, übernimmt aber nicht automatisch system_instruction,
tools oder generation_config aus dem letzten Aufruf. Nur bereits zulässige unveränderliche
Profileinstellungen werden erneut übergeben; die Bibliothek erstellt keine Tools oder
Featurekonfigurationen selbst. Unbekannte vom Host gewünschte Einstellungen werden
adapterbezogen geprüft, nicht mit einer zentralen herstellerübergreifenden Featurematrix.

completed ohne erforderliche Clientaktion ist Erfolg. in_progress bleibt laufend;
requires_action ist UnsupportedProviderAction. Failed, incomplete und cancelled bleiben
unterscheidbar. Ein Cancel-POST ist keine dokumentationsübergreifende Idempotenzgarantie.
Retention kann je Tier/Einstellung variieren; abgelaufene Interaktionen werden nicht lokal nachgebaut.

## 5. Ungewisser Ausgang

Vor jedem externen Send persistiert der Kern DispatchId und Reservierung. Ein Timeout
zwischen Übergabe und empfangener OperationId bleibt Unknown. Der Adapter darf nicht
wegen eines Netzwerkfehlers automatisch noch einmal senden.

Ist OperationId bekannt, kann ausdrücklich angefordertes Inspect den nativen Endzustand
feststellen. Dies verändert die lokale Anfrage oder Reservierung noch nicht still;
die Recovery-Schnittstelle entscheidet über sichere Freigabe und gültigen Fortsetzungsstand.
Kein vorhandener Diagnose-Logeintrag ersetzt ein gesichertes Ergebnis oder Binding.

Lesende Statusabfragen dürfen bei temporären Fehlern innerhalb der endlichen Frist
wiederholt werden. Authentifizierungsfehler, Kontextgrenzen und nicht unterstützte
Aktionen werden nicht durch Wiederholung oder automatische Modell-/Providerwechsel kaschiert.

## 6. Erweiterung und Kompatibilitätsprobe

Weitere kompatible Endpunkte werden ausschließlich durch Konfiguration eingebunden.
Ein anderer nativer Vertrag erhält eine weitere Implementierung von ISessionProvider.
Der Host registriert sie ausdrücklich. Es gibt kein Ableiten von Verhalten aus
Provider-Namen, keine beliebige Assembly-Datei im Request und keinen offenen Pluginloader.

Jeder Adapter muss denselben Satz Tests bestehen: Eröffnung/lazy binding, Folgeanfrage,
unveränderte Zuordnung, aktualisierter bestätigter Kopf, falscher/fehlender Bezug,
Fehler-/Timeout-/Cancel-Zustände, Retentionfehler und fehlender eigener Feature-Nachbau.
Eine dritte kontrollierte Testimplementierung weist die Erweiterbarkeit nach, ohne
den Kern oder eine konsumierende Anwendung herstellerspezifisch umzuschreiben.

Live-Abnahme nutzt mindestens zwei reale Hersteller mit je einer Eröffnung, erster
Nachricht und Folgeanfrage auf allen zugesicherten Zielsystemen. Eine Fake-Probe belegt
den Verwaltungsvertrag, nicht den tatsächlichen Herstellerbetrieb.

## 7. Nicht enthalten

Eigene Tokenlimits, Kontextverdichtung, Werkzeugausführung, Agent-Schleifen, multimodale
Konvertierung, Modellkatalogsynchronisation, dynamische Agent-Delegation, uneingeschränkte
CLI-Fernsteuerung und automatische Wiederholung von Send-Operationen.
Remotezugang für Endnutzer oder Betriebssystemdienste sind ebenfalls keine Providerfunktion.

## 8. Generische Agent-Backend-Grenze — verbindliche Konkretisierung

`ISessionProvider` bezeichnet einen registrierten Ausführungsadapter, nicht zwingend
eine Hersteller-HTTP-API. CLI-, SDK- und Modelladapter nutzen dieselbe Sitzungsgrenze.
Keine Auswahl über Produktnamen im Kern und keine Source-/Assembly-Abhängigkeit zum Zielhost.

- `ProviderCapabilities` benennt Unterstützung für Agentaufträge, Modellantworten,
  native Inspektion und nativen Abbruch sowie einen erforderlichen Workspace explizit.
  Ein `AgentProfile` legt `ExecutionKind` (`AgentTask` oder `ModelResponse`) fest.
  Nicht unterstützte Anforderungen werden vor Übergabe abgewiesen; eine Modellantwort
  darf einen verlangten Agentauftrag nicht erfüllen. Kein eigener Toolloop als Ersatz.
- `SessionExecutionContext` trägt den optionalen absoluten `WorkspaceDirectory` und
  den vom Host gelieferten kanonischen WorkspaceKey. Snapshot und Zuordnung werden
  mit der Sitzung gespeichert. Ein erforderlicher Workspace muss vor Start validiert
  werden. Der Schlüssel bleibt separat für Schreibserialisierung maßgeblich.
- `ProviderCall` enthält lokale Session-/Request-/Dispatchkennungen, ModelId,
  ExecutionKind, Ausführungskontext, neue Eingabe, Limits und unveränderliche zulässige
  Profileinstellungen. `ProviderBinding` trägt AdapterId, Formatversion, optionale
  stabile `SessionReference` und getrennten bestätigten `ContinuationState`.
- Open/Reopen nutzen einen `ProviderSessionContext`, ohne Dummy-Anfrage oder Dummy-Prompt.
  Lazy-Open ohne native ID ist nur vor erster möglicher Übergabe zulässig.
- Send erhält einen awaitable Beobachtungscallback. `ProviderObservation` unterscheidet
  `SessionEstablished`, `OperationAccepted` und inhaltsfreien `Progress`.
  Neue native ID/Operations-ID muss der Kern dauerhaft sichern und den Callback
  bestätigen, bevor der Adapter fortfährt. Callbackfehler bedeutet keine bestätigte
  erfolgreiche Ausführung. Bereits gebundene native Identität darf nicht wechseln.
- Nur `ProviderReply` mit explizitem finalem Outcome und ExecutionCertainty bestätigt
  den lokalen Abschluss. Ein Progress-/Tool-Zwischenschritt, eine Operations-ID oder
  ein Prozess-Exitcode allein ist kein Erfolg. ConfirmedEnded und Unknown bleiben
  verschieden; Cancel-Anforderung und belegter Abschluss ebenfalls.
- Inspect/Cancel bei fehlender Fähigkeit liefern ein typisiertes Unsupported-Ergebnis,
  keinen stillen No-op. Die Entscheidung zur Reservierungsfreigabe bleibt im Kern.

**Zielhostabbildung**: hostseitiger RunContext → Session-/Aufrufkontext; beobachtete
native Session-ID → bestätigter SessionEstablished-Callback; TerminalSuccess plus
Exitcode → finaler Reply; CancellationToken → kooperative Adaptercancellation.
Workspace/Modell/Backend bleiben an denselben Sitzungs-Snapshot gebunden. Der Host
verantwortet weiterhin seine Tools, Anmeldungen und Prozessausführung. Diese Abbildung
ist ein überprüfbarer Entwurf, noch kein bestandener Konsum- oder Migrationsnachweis.

**Verbindliche Referenz**: OpenCode **1.19.0**, ausdrücklich vom Projektverantwortlichen
beibehalten. Kein Update und keine Übernahme neuerer CLI-/V2-API-Verträge.
OpenCode bleibt ein optionales externes Backend, keine Pflichtlaufzeit (FR-022).
Eine Referenzfixture darf dessen Session-/Abschlussvertrag modellieren, jedoch keine
echte OpenCode-Ausführung oder echte Providerabnahme vortäuschen.

**Zusätzliche Vertragsfälle vor Green (T010/T035/T089)**: Workspace/ExecutionKind und
unveränderte Modellkennung an ein hosteigenes Backend übergeben; opaque `ses_…` über
zwei Aufträge fortführen; ID vor weiterem externen Fortschritt speichern; Identitätswechsel
abweisen; Exit 0 ohne terminalen Erfolg und Tool-Zwischenschritt nicht als Erfolg werten;
Callbackfehler und Cancel ohne Endbeleg behalten Unknown; nicht unterstützte Fähigkeiten
vor Übergabe ablehnen. Diese Fälle ergänzen, ersetzen aber nicht die zwei nativen
Herstellerverträge, Persistenz-/Prozessproben oder den tatsächlichen Paketkonsum.

## 9. Operationsgebundene Wiederaufnahme nach Unknown

Ein Adapter, dessen ungewisse Ausführung einen nativen Fortsetzungskopf ändern kann,
setzt `ProviderCapabilities.RequiresOperationAwareRecovery=true` und implementiert
`IExecutionRecoveryProvider.ReopenAfterExecutionAsync`. Der Port erhält den gespeicherten
bekannten Bindingbezug und die optionale persistierte Operationskennung. Er liest native
Belege und liefert ausschließlich einen bestätigten kompatiblen Fortsetzungsstand.
Er eröffnet und sendet keine Arbeit. Ein noch lesbarer alter Kopf allein genügt nicht.
Fehlt ein für diese Prüfung nötiger Bezug, bleibt UnsupportedProviderAction/Unknown;
fehlender Port ist kein Anlass zum Rückfall auf gewöhnliches Reopen.

Bei stabilen nativen Conversation-IDs bleibt gewöhnliches Reopen zulässig. Die Auswahl
ist capability-/interfacegebunden, ohne Herstellernamen im Kern. Bestätigtes Ende und
Fortsetzungsstand werden erst mit der ausdrücklichen revisionierten Recoveryentscheidung
veröffentlicht; Inspect allein ändert nichts. Die historische Anfrage wird nicht nachträglich
Succeeded. Close/Closing erfordert keine Wiederöffnung zur Fortsetzung.

## 10. Ende und Fortsetzungsverfügbarkeit

`ProviderReply.ContinuationAvailable` ist eine optionale Aussage des Adapters. `false`
benennt einen bekannten Fortsetzungsblocker, `null` überlässt unveränderte Legacy-/Fehlerpolicy,
`true` ist kein Beweis für ein externes Ende. Common interpretiert keine herstellerspezifischen
ContinuationState-Strings. Nach sicherem Abschluss werden Ergebnis/History/Binding,
ProviderUnavailable-Sperre und sichere Slotfreigabe gemeinsam gespeichert. Native Expiry
blockiert auch Legacy-Produzenten ohne dieses optionale Feld.

Bei Unknown hat RecoveryRequired samt gehaltener Reservierung Vorrang. Ein negatives
Fortsetzungssignal darf fehlende Endgewissheit nicht ersetzen. Umgekehrt bleibt ein nativer
completed-Head auch bei lokaler OutputLimitExceeded nutzbar; allein ein lokaler Textfehler
rechtfertigt keinen Rückfall auf einen veralteten Kopf. Der erste sicher fehlgeschlagene
Interaktionsaufruf ohne completed-Head kann seinen bekannten Root bewahren, aber keine
neue Wurzel oder weitere Annahme stillschweigend erlauben.
