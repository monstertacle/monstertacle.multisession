# Data Model: Agent-Multisession

**Datum**: 2026-10-27 | **Basis**: [spec.md](spec.md)

**Erneute Designprüfung**: Constitution v2.0.0; konkretisierte Dispatch-/Recovery-/
Annahme-/Pagingprojektionen gemäß [contracts/orchestration-details.md](contracts/orchestration-details.md).
Ihre Validierung und Zustandsübergänge werden in TDD-01/TDD-03/TDD-04 zuerst durch
fachlich fehlschlagende Tests erfasst; Persistenzverhalten folgt erst danach.

## Verantwortungsgrenze

Gespeichert werden Verwaltung, Zuordnung und beobachtete Ein-/Ausgaben von Sitzungen.
Der lokale Verlauf ist ein Nachweis, keine Aufforderung, Provider-Kontext selbst nachzubauen.
Werkzeuge, Agent-Schleifen, Zusammenfassungen und Modell-Kontextbudgets sind kein eigenes Datenmodell.

Ein Zustandsspeicher gehört genau einem Vertrauensbereich und genau einer aktiven
Manager-Instanz. Ein zweiter Besitzer desselben Speichers wird abgewiesen. Getrennte
Instanzen verwenden getrennte Speicher. Globale veränderliche Register sind unzulässig.

## Entitäten

### ProviderRegistration und AgentProfile

| Feld | Bedeutung und Prüfung |
| --- | --- |
| ProviderId | Nicht leere, ordinal verglichene technische ID; je Instanz eindeutig |
| AdapterId / ContractVersion | Ausdrücklich registrierte Implementierung und Vertragsversion |
| Adapterinstanz / Capabilities | Explizit registrierte Ausführung und typisierte Fähigkeiten; keine Auswahl über Produktnamen |
| Endpoint (nur HTTP-Adapteroption) | Absolute freigegebene HTTPS-Adresse; HTTP nur explizit für lokale Tests; kein Pflichtfeld generischer Backends |
| CredentialReference | Referenz auf hostseitigen Resolver; kein Geheimnis im Sitzungszustand |
| AgentProfileId | Eindeutige benannte Agent-Konfiguration |
| ModelId | Vom Host gewählte opaque Modell-/Deploymentkennung; keine erfundene Modellliste |
| ExecutionKind | Explizit AgentTask oder ModelResponse; muss von Adapterfähigkeiten unterstützt sein |
| SettingsRevision | Unveränderlicher Snapshot kontextrelevanter Einstellungen ohne Zugangsdaten |

Provider-Registrierungen sind normale vom Host zusammengestellte Objekte, kein eigener
DLL-Download-, Discovery- oder Plugin-Marktplatz. Gleiche IDs mit verschiedener Bedeutung
sind ein Konfigurationsfehler. Geheimnisse werden nur beim tatsächlichen Aufruf aufgelöst.

### Session

| Feld | Bedeutung und Prüfung |
| --- | --- |
| SessionId | Lokal erzeugte UUID; unveränderliche öffentliche Identität |
| ProviderId / AgentProfileId / ModelId | Bei Erstellung festgehaltene Zuordnung |
| SettingsSnapshot / SettingsRevision | Für Wiederaufnahme erforderliche zulässige Einstellungen |
| ExecutionContext | Eingefrorener optionaler absoluter WorkspaceDirectory und kanonischer WorkspaceKey; gemäß Adapterfähigkeiten prüfen |
| ProviderBinding | Versionierter providerdefinierter Sitzungsbezug, siehe unten |
| BindingRevision | Ab 0 monoton erhöhter bestätigter Fortsetzungsstand; kein Ersatz für opaque native Kennungen |
| Lifecycle | Opening, Open, Closing, Closed |
| BlockReason | None, RecoveryRequired, ProviderUnavailable, StorageFailure |
| NextSequence | Monotone nächste Annahmenummer innerhalb der Sitzung |
| CreatedUtc / UpdatedUtc | Eindeutige UTC-Zeitpunkte |
| ClosedUtc | Nur für Closed gesetzt |

Lifecycle und Blockierung sind getrennt: Eine offene Sitzung kann durch ungewisse Arbeit
blockiert sein, ohne als geschlossen oder erfolgreich wiederhergestellt zu gelten.
Ein anderer Provider oder ein anderes Modell erfordert eine neue Sitzung.

**ProviderBinding** enthält einen optional erst bei der ersten Übergabe bekannten nativen
Sitzungsbezug und einen separaten, versionierten Fortsetzungsstand. Ein unveränderlicher
Sitzungsbezug ist nicht dasselbe wie ein sich nach jeder Antwort ändernder Fortsetzungstoken.
Nur der Adapter interpretiert diese Daten. Keine Zugangsdaten, ausführbaren Typnamen oder
beliebige vom Modell gelieferte Deserialisierungsanweisungen speichern.

Fehlt der externe Bezug nach einer möglicherweise ausgeführten ersten Anfrage, wird die
Sitzung blockiert. Es wird nicht einfach eine neue native Sitzung angelegt.

### Request

| Feld | Bedeutung und Prüfung |
| --- | --- |
| RequestId | Lokal erzeugte UUID; genau einer Sitzung zugeordnet |
| SessionId / Sequence | Fremdschlüssel und eindeutige Annahmereihenfolge je Sitzung |
| Input | Unveränderte Textnachricht; nicht leer; Größenlimit in UTF-8-Bytes |
| WorkspaceKey | Optionaler, vom Host deklarierter Arbeitsbereichsschlüssel |
| Timeout | Endliche Ausführungsfrist; beginnt beim tatsächlichen Dispatch |
| State | Queued, Running, Succeeded, Failed, Cancelled, TimedOut oder Interrupted |
| ExecutionCertainty | NotStarted, InProgress, ConfirmedEnded oder Unknown |
| AcceptedUtc / StartedUtc / EndedUtc | Lebenszykluszeitpunkte |
| CancellationRequestedUtc | Getrennte Absicht, kein Beweis der externen Beendigung |
| DispatchId | Persistierte UUID des einzigen erlaubten Dispatch-Versuchs |
| BindingRevision | Fortsetzungsstand, gegen den die Anfrage gestartet wurde |

Nur Queued zählt als wartend; Reservierungen einschließlich ungewisser Ausführungen
zählen bis zur Klärung als belegte Ausführungskapazität. Abgewiesene Anfragen erhalten
keine Annahmebestätigung und keinen Platz in der dauerhaften Warteschlange.

Annahme erfolgt erst nach erfolgreichem Commit. Ein Aufruferabbruch nach Commit macht
die Annahme nicht rückwirkend unwirksam. Bei verlorener Bestätigung bleibt die Anfrage
abfragbar; es gibt keine automatische Deduplizierung eines neuen Einreichens.

Zulässige Kombinationen: Queued/NotStarted; Running/InProgress; Succeeded/ConfirmedEnded;
Failed, Cancelled oder TimedOut mit NotStarted, ConfirmedEnded oder Unknown entsprechend
dem tatsächlichen Übergabestatus; Interrupted zunächst mit Unknown. InProgress bedeutet
laufender lokaler Dispatchversuch, nicht bereits bewiesene Annahme beim Hersteller.
Nach einem lokalen Endzustand mit Unknown bleibt die Reservierung bestehen. Eine spätere
Recovery darf die Gewissheit auf ConfirmedEnded ändern, nicht den historischen Endzustand
zu einem behaupteten Erfolg machen. Sitzungs-Lifecycle und BlockReason sind keine Anfragezustände.

### RequestResult und HistoryEntry

| Feld | Bedeutung und Prüfung |
| --- | --- |
| RequestId | Ein öffentliches finales lokales Ergebnis je Anfrage |
| Outcome | Succeeded, Failed, Cancelled, TimedOut, Interrupted |
| FailureCode | Stabile Kategorie; optional providerbezogener, bereinigter Code |
| ExecutionCertainty | Ergebnis der tatsächlichen Beendigungsbeobachtung |
| Output | Vom Provider gemeldete endgültige Textausgabe, nicht selbst generierte Ersatzantwort |
| OutputComplete | Wahr nur bei vollständig im zulässigen Größenlimit erfasster Ausgabe |
| ProviderReference | Nicht geheime externe Belegkennung, sofern verfügbar |
| CompletedUtc | UTC-Zeitpunkt |
| HistoryOrdinal / HistoryId (HistoryEntry) | Monotone sitzungsbezogene Einfügeordnung und lokale UUID je Historieneintrag |

HistoryEntry ordnet Eingabe, Ausgabe und Betriebsereignisse über SessionId, RequestId
und Sequence zu. Eine Ausgabe oberhalb MaxOutputBytes wird mit Failed/OutputLimitExceeded
ohne gekürzten Erfolgsinhalt erfasst; vorhandene Historie bleibt unverändert. Für diesen
Fehler bleibt Output leer und OutputComplete=false. Bei regulär vollständiger Ausgabe
gilt OutputComplete=true. Kann der externe Abschluss nicht bestätigt werden, ist zusätzlich
Unknown mit erhaltener Reservierung erforderlich. Eine Provider-Kontextgrenze ist ebenfalls
ein Fehler, keine Berechtigung, vorhandene Einträge zu verkürzen oder zu verdichten.

Die lokale Historie umfasst die über diese Bibliothek beobachteten Inhalte. Bereits
herstellerseitig existierende oder durch dessen interne Werkzeuge entstandene Inhalte
werden nicht als vollständig lokal vorhanden behauptet.

### ExecutionReservation

| Feld | Bedeutung und Prüfung |
| --- | --- |
| DispatchId / RequestId | Eindeutige Zuordnung zum tatsächlich reservierten Versuch |
| SessionId | Höchstens eine unfreigegebene Reservierung je Sitzung |
| WorkspaceKey | Höchstens eine unfreigegebene schreibende Reservierung je Schlüssel |
| ReservedUtc | Vor externem Start gesichert |
| ReleaseReason / ReleasedUtc | Erst nach bestätigtem Ende oder ausdrücklicher Klärung |

Für einen schreibenden Auftrag ist WorkspaceKey verpflichtend; die Bibliothek ermittelt
nicht selbst aus Modelltexten, auf welche Dateien ein Agent zugreifen könnte. Der Host
liefert konsistente kanonische Schlüssel für gemeinsame Arbeitsbereiche. Ein ungeklärter
Auftrag hält die zugehörige Reservierung auch nach einem Neustart.

### ReservedDispatch und RequestAcceptance

ReservedDispatch ist der transaktional mit der Reservierung gespeicherte Ausführungssnapshot
aus Reservation, Session, Submission, Sequence und BindingRevision. GetReservedDispatchAsync
liest ihn ohne Provider-I/O; Freigabe/fehlender Auftrag berechtigt nicht zum Send. Prüfung,
Bindingsfortschreibung und Completion-CAS stehen in orchestration-details.md §1.

RequestAcceptance trägt Kandidaten-RequestId und unveränderten SubmitRequest. Die ID wird
vor Commit erzeugt, Sequence und Queueplatz weiterhin atomar vom Store vergeben. AdmissionStatus
ist separate Annahmegewissheit, kein Anfragezustand und keine ExecutionCertainty. Fehler,
Caller-Cancel, bestätigter Commit und Nachschlagen folgen orchestration-details.md §3.

### RecoveryCaseSnapshot und RecoveryDecision

RecoveryTarget identifiziert SessionOpening/SessionId oder RequestExecution/RequestId.
RecoveryCaseSnapshot trägt dieses Target, SessionId, optionale RequestId/DispatchId,
CreatedUtc, persistierte Revision ab 1, Fallstatus und bereinigte Klärungsinformationen.
Native Bezüge sind optional; eine ungewisse Eröffnung benötigt keinen Dummy-Request.
Erzeugungs-/CAS-/Inspectregeln und Aufbewahrung aufgelöster Fälle stehen in
orchestration-details.md §2; die Listenordnung in §4.

| Feld | Bedeutung und Prüfung |
| --- | --- |
| DecisionId / Target | Eindeutige Entscheidung für eine ungewisse Eröffnung oder Ausführung |
| ExpectedRevision | Schutz gegen veraltete oder konkurrierende Entscheidungen |
| Kind | ConfirmEnded oder CloseSession |
| Explanation / EvidenceReference | Pflichtbegründung ohne Geheimnisse |
| DecidedUtc | UTC-Zeitpunkt |

ExpectedRevision stammt aus dem öffentlich gelesenen RecoveryCaseSnapshot. Eine Entscheidung
ändert Fall, Gewissheit und erlaubte Freigabe gemeinsam oder gar nicht; veraltete Revision
und bereits verwendete DecisionId ergeben RevisionConflict. BindingRevision und Fallrevision
sind verschiedene Zähler mit verschiedenen Verantwortlichkeiten.

ConfirmEnded bestätigt, dass keine externe Arbeit mehr läuft. Der historische Endzustand
Interrupted bleibt erhalten; Gewissheit und Reservierungsfreigabe werden nachvollziehbar
ergänzt. Dies ist kein nachträglicher Erfolgsnachweis. Vor Folgeanfragen muss der Adapter
denselben Provider-Bezug wieder aufnehmen können; sonst bleibt die Sitzung blockiert.

CloseSession verhindert weitere Ausführung in der Sitzung. Die Schließung allein löst
keine ungewisse Arbeitsbereichs-/Kapazitätsreservierung: Auch dafür ist ein gesicherter
Beendigungsnachweis oder die ausdrückliche Betreiberbestätigung erforderlich.
Wiederholung ist nur eine neue, ausdrücklich eingereichte Anfrage mit neuer Kennung.
Eine Recovery-Entscheidung CloseSession bedeutet daher Closing, solange eine solche
Reservierung offen ist. Closed wird erst nach deren belegter Klärung und dem Ende aller
weiteren offenen Anfragen erreicht. Das Umbenennen eines Zustands darf keine Sperre umgehen.

### StoreMetadata

SchemaVersion, StoreId, InstanceTrustScopeId, CreatedUtc und Migrationsversion identifizieren
den Speicher. Ein unbekanntes neueres Schema wird vor Schreibzugriff abgewiesen.
Ein absichtlich neuer Speicher wird ausdrücklich initialisiert. Ein fehlender erwarteter
Speicher darf nicht durch automatische Create-if-missing-Verwendung ersetzt werden.

Version 1 initialisiert ausschließlich ein ausdrücklich neues Format und öffnet dasselbe
bekannte Format erneut. Es gibt noch keine In-place-Migration oder automatische
Formatkonvertierung. Abweichende Schemata/Binding-Versionen führen zu einem Fehler,
bevor bestehende Nutzdaten oder Metadaten verändert werden. Auch ein fehlgeschlagener
Start darf keinen Ersatzstore veröffentlichen. Spätere Formatänderungen benötigen
eine separate Planung mit Backup-, Crash-, Rückkehr- und Abnahmeregeln.

## Zustandsübergänge

| Ausgang | Ereignis | Ergebnis / atomare Wirkung |
| --- | --- | --- |
| Keine Anfrage | Annahme bei offener Sitzung und freiem Wartelimit | Queued + Eingabe + Sequence committen, dann bestätigen |
| Queued | Abbruch vor Dispatch | Cancelled / NotStarted; kein Provider-Aufruf |
| Queued | Zulassung mit freier Kapazität und freiem Workspace | Running/InProgress + DispatchId + Reservierung in einer Transaktion |
| Running | Bestätigter erfolgreicher Abschluss | Succeeded + Ausgabe + neuer Binding-Stand + Reservierungsfreigabe in einer Transaktion |
| Running | Bestätigter Fehler / Abbruch / Timeout | Entsprechender Endzustand + ConfirmedEnded + Freigabe |
| Running | Verbindungsausfall, lokaler Abbruch ohne Beendigungsnachweis | Failed/Cancelled/TimedOut + Unknown; Reservierung und Sitzung gesperrt |
| Running beim Wiederanlauf | Kein sicher gespeichertes Ergebnis | Interrupted + Unknown; kein automatisches Replay |
| Endzustand / Unknown | ConfirmEnded mit passender Revision | Gewissheit und Reservierung ändern; Ergebnis nicht als Erfolg umdeuten |
| Open | CloseSession | Closing; neue Annahme sperren, wartende abbrechen, laufende kontrolliert stoppen |
| Closing | Keine offenen oder ungewissen Ausführungen | Closed; Historie gemäß Aufbewahrungsregel erhalten |

Ein ausdrücklich abgebrochener, noch nicht versendeter Auftrag darf sicher als NotStarted
enden. Nach dem persistierten Dispatch-Marker wird konservativ Unknown angenommen, wenn
nicht sicher belegt ist, dass der Provider keinen Auftrag angenommen hat. Dadurch kann
ein Crash unmittelbar vor dem echten Versand eine manuelle Klärung verlangen; diese
Übervorsicht ist die bewusste Alternative zu möglichen doppelten Seiteneffekten.

## Speicherung und Transaktionsgrenzen

Separate Tabellen für Metadata, Sessions, Requests, Results, History, Reservations,
RecoveryCases und RecoveryDecisions; ReservedDispatch wird an den reservierten Request/
Dispatch gebunden gespeichert. Eindeutigkeit für `(SessionId, Sequence)`, RequestId und aktive
Sitzungs-/Arbeitsbereichsreservierungen wird durch Datenbankconstraints abgesichert.
Dieses Dokument definiert das Zielschema; konkrete SQL-Umsetzung und Integration bleiben
offene Tasks. Bereits vorhandene C#-Gerüste enthalten noch nicht alle oben genannten Felder.

- Annahme, Kapazitätsprüfung und Sequenzvergabe erfolgen gemeinsam transaktional.
- Nur der erste wartende Auftrag einer ausführbaren Sitzung darf zugelassen werden.
- Datenbanksperren werden nie über einen Provider-Netzwerkaufruf gehalten.
- Nach Antwort müssen Ergebnis und Fortsetzungsstand gemeinsam committen; ein fehlender
  Commit darf beim Neustart nicht als erfolgreiche Fortsetzung interpretiert werden.
- Provider-Aufruf und lokaler Commit sind keine verteilte Transaktion. Die Lücke wird
  durch persistierten Dispatch und Unknown behandelt, nicht durch Exactly-once-Versprechen.
- Ein nur im Speicher vorhandener Channel ist ein Wecksignal, niemals die Anfragequelle.

## Wiederanlauf und Aufbewahrung

Start: exklusiven Speicherbesitz erwerben, Schema prüfen, Konsistenz prüfen, alte Running-
Aufträge als ungewiss markieren, Reservierungen erhalten, Provider-Bindungen prüfen,
danach ausschließlich ungesperrte Queued-Aufträge wieder zulassen.

Kontrollierter Stop ist davon getrennt: Er sperrt neue Annahme, endet nach einer
endlichen Frist und speichert verbliebene Queued-Anfragen als Cancelled/NotStarted.
Ein Absturz vor dieser Speicherung lässt sie Queued und damit wiederaufnehmbar.
Ein bereits gespeicherter Endzustand wird nach Neustart nicht erneut gestartet.

Queued-Anfragen haben keine automatische Ablaufzeit. Ihre Anfragefrist beginnt erst
mit Dispatch; der Host kann sie vorher ausdrücklich abbrechen. Auf die lokale Annahme
gilt stattdessen die separate endliche AdmissionTimeout-Frist. Bei freier Ausführungskapazität
wird der älteste zulassungsfähige Sitzungskopf bevorzugt; bei identischem AcceptedUtc
entscheidet RequestId ordinal. Blockierte Köpfe werden nicht als ausführbar behandelt.
Keine feste globale Wartezeit oder Starvation-Freiheit wird zugesichert.

Aufbewahrung benötigt eine explizite hostseitige Frist oder die ausdrückliche Einstellung
RetainUntilExplicitDeletion. Kein stiller Default zur Inhaltslöschung. Nur geschlossene
Sitzungen ohne wartende, aktive oder ungeklärte Arbeit sind löschbar. Löschung entfernt
lokale Inhalte aus den Bibliotheksabfragen, nicht automatisch externe Providerdaten,
Backups oder physische SQLite-Seiten. Sichere Datenträgerlöschung ist keine Zusicherung.

## Grenzen für Version 1

- Textnachrichten und endgültige Textantworten; keine eigene multimodale Verarbeitung.
- Ein aktiver Besitzer pro lokalem Store; keine Netzwerkdateisysteme oder aktiven Cluster.
- Konfigurierbare endliche Queue-, Anfrage-, Ausgabe- und Zeitlimits.
- Keine eingebauten Tools, Promptverdichtung, Transcript-Replay oder Featureverhandlung.
- Öffentliche Fehler enthalten keine Tokens, Prompts oder rohe Hersteller-HTTP-Bodies.
