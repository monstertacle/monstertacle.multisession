# Research: Agent-Multisession

**Datum**: 2026-10-27 | **Basis**: [spec.md](spec.md)

Die folgenden Entscheidungen lösen die technischen Auswahlfragen der Phase 0.
Es wurden offizielle Dokumentation, Release-Metadaten und Paketmetadaten gelesen.
In der ursprünglichen Recherche keine Pakete installiert, Builds oder Produkt-/Herstellertests ausgeführt, Zugangsdaten
gelesen oder kostenpflichtigen Provider-Aufrufe getätigt. Dokumentation ist kein Laufzeitnachweis.

## R-01 — Laufzeit und Plattformmatrix

**Decision**: C# 14, .NET 10 LTS, `net10.0`; SDK 10.0.403 exakt in der
`global.json` mit `rollForward: disable` und ohne Prereleases festhalten.
Erste Abnahmematrix: Windows 11 25H2 x64, Windows Server 2025 x64 und Ubuntu 24.04 LTS x64.
Arm64, andere Distributionen, Netzwerkdateisysteme und AOT sind keine Freigabezusicherung.

**Rationale**: Unterstützte gemeinsame Laufzeit; die abgefragten offiziellen
Release-Metadaten nennen SDK 10.0.403 und Runtime 10.0.14, veröffentlicht am 2026-10-25.
Eine konkrete Matrix begrenzt die Portabilitätsabnahme, ohne Windows mit Windows Server
oder alle Linux-Systeme mit einem einzigen Linux-Test gleichzusetzen.

**Alternatives considered**: .NET 8 mit kürzerer verbleibender Supportperspektive;
ungepinnter SDK-Rollforward; zusätzliche Ziel-Frameworks ohne aktuellen Bedarf.

**Genehmigte Nacharbeit (2026-10-27)**: Statt des ursprünglich geplanten 10.0.403
ausdrücklich das lokal vorhandene 10.0.402 verwenden. `dotnet --info` bestätigt
10.0.402 und 10.0.303, nicht 10.0.403. `global.json` pinnt jetzt 10.0.402;
keine SDK-Installation oder implizites Roll-forward wurde vorgenommen.

**Genehmigte Aktualisierung (2026-10-28)**: SDK 10.0.402 ist nicht mehr installiert;
vorhanden sind jetzt 10.0.303 und 10.0.403. Der Projektverantwortliche hat
„10.0.403 nutzen“ ausdrücklich bestätigt. Der Pin wird ohne Roll-forward auf
10.0.403 aktualisiert; keine Installation und keine Änderung von OpenCode 1.19.0.
Frühere 10.0.402-Prüfergebnisse bleiben als historische Nachweise unverändert.

Quellen: [.NET-10-Release-Metadaten](https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json),
[SDK-Auswahl](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json),
[Windows-Unterstützung](https://learn.microsoft.com/en-us/dotnet/core/install/windows),
[Ubuntu-Unterstützung](https://learn.microsoft.com/en-us/dotnet/core/install/linux-ubuntu-install).

## R-02 — Paketgrenzen und KISS

**Decision**: Sechs Bibliotheken mit echten Verantwortlichkeiten:

- `Monstertacle.Multisession.Common`: öffentliche Verträge, Sitzungskatalog, dauerhafte
  Annahmeregeln, Dispatch-Zulassung, FIFO, Recovery und strukturierte Diagnosen.
- `Monstertacle.Multisession.Storage.Sqlite`: transaktionaler lokaler Zustand.
- `Monstertacle.Multisession.Windows`: Vorbereitung/Prüfung privater Zustandsverzeichnisse,
  Windows-ACLs und exklusiver Speicherbesitz; Ziel `net10.0-windows`.
- `Monstertacle.Multisession.Linux`: entsprechender Vertrag mit Unix-Rechten und
  Linux-Pfad-/Besitzprüfung; Ziel `net10.0`, expliziter Linux-Laufzeitguard.
- `Monstertacle.Multisession.Providers.OpenAI`: native Conversations/Responses-Anbindung.
- `Monstertacle.Multisession.Providers.Gemini`: native Interactions-Anbindung.

Alle konkreten Bibliotheken hängen von Common ab, niemals umgekehrt. Common und
Storage erzwingen keinen Windows-, Linux-, Provider-, Hosting-, SSH- oder UI-Stack.
Keine zusätzlichen Abstractions-/Hosting-Pakete, kein dynamischer Assemblyloader und
kein eigener Agent-Framework-Unterbau. Facetten für Sitzungen, Anfragen und Recovery
vermeiden eine überladene öffentliche Klasse und unbegründete Universalabstraktionen.

**Rationale**: Gemeinsame Fachlogik ist unabhängig testbar. Plattformpakete enthalten
konkrete Sicherheits-/Besitzaufgaben, nicht duplizierte Sitzungslogik oder leere Marker.
SQLite und Provider können mit eigenen Fremdabhängigkeiten getrennt konsumiert werden.

**Alternatives considered**: Eine einzige Bibliothek mit sämtlichen nativen Abhängigkeiten;
duplizierte Windows-/Linux-Kerne; je Domänenbegriff ein eigenes Paket; fertiger Dienst statt Bibliothek.

## R-03 — Native Provider-Sitzungen statt eigener Sitzungsfeatures

**Decision**: Zwei unterschiedliche Referenzverträge über BCL-`HttpClient`:

| Vertrag | Eröffnung und Fortsetzung | Eigene Zuständigkeit |
| --- | --- | --- |
| OpenAI `/v1/conversations` + `/v1/responses` | Conversation-ID erstellen; jede neue Nachricht an dieselbe Conversation senden | Conversation-ID und getrennte Response-ID sichern |
| Gemini Interactions `/v1/interactions` | Erste Nachricht eröffnet Interaktion; Folgeanfragen referenzieren letzte bestätigte `previous_interaction_id` | Lokale SessionId, erster nativer Bezug und wechselnden Fortsetzungskopf unterscheiden |

Für Google erfolgt die native Bindung erst mit der ersten Nachricht. Eine lokal eröffnete
Sitzung mit leerem Binding ist daher zulässig, solange noch keine Anfrage versendet wurde.
Ein fehlender Bezug nach möglicherweise erfolgter Übergabe ist dagegen ungewiss und blockiert.

Nur neue Nachrichten und explizite kontextrelevante Einstellungen übergeben; keine lokal
rekonstruierte Unterhaltung, Tokenzählung, Zusammenfassung oder Werkzeugschleife.
Bei Google müssen unter anderem system instructions und generation settings je Aufruf
erneut übermittelt werden; dies ist Konfiguration und kein Transcript-Replay.

**Rationale**: Die APIs besitzen native gespeicherte Zustände und unterscheidbare Operationen.
Die aktuelle offizielle Google-Dokumentation bezeichnet Interactions als GA seit Juni 2026;
implementiert wird die stabile `/v1`-Referenz, nicht eine Mischung älterer Beta-Beispiele.

**Alternatives considered**: Stateless Chat-Completions-/Messages-Protokolle mit lokalem
Replay oder eigener Agent-Schleife verletzen die Umfangsentscheidung. Separate CLI-Runtimes
wären als spätere Adapter möglich, benötigen aber zusätzliche Prozess-, Versions- und
Plattformverträge und sind für diese erste Lieferung nicht erforderlich.

Quellen: [OpenAI Conversation state](https://developers.openai.com/api/docs/guides/conversation-state),
[Create conversation](https://developers.openai.com/api/reference/resources/conversations/methods/create),
[Gemini Interactions overview](https://ai.google.dev/gemini-api/docs/interactions-overview),
[Gemini `/v1` reference](https://ai.google.dev/api/interactions-api-v1).

## R-04 — Erfolg, Abbruch und ungewisse Übergabe

**Decision**: Background-Operationen mit `store:true`, native Operationskennungen früh
sichern und deren Zustand abfragen. Nur bestätigtes `completed` ohne erforderliche
clientseitige Aktion ist Erfolg. `requires_action`, `incomplete` und reine HTTP-2xx-Antworten
sind kein erfolgreicher Abschluss einer verwalteten Anfrage.

OpenAI: Response über `GET /responses/{id}` prüfen; Background-Abbruch über
`POST /responses/{id}/cancel`. Google: `GET /interactions/{id}` beziehungsweise
`POST /interactions/{id}/cancel`; eine bestätigte Abbruchanforderung ist noch kein Endzustand.
Die Bibliothek konfiguriert selbst keine Provider-Werkzeuge. Clientseitig angeforderte
Werkzeugaktionen werden als UnsupportedProviderAction gemeldet und nicht ausgeführt.

Lokaler Timeout oder Verbindungsabbruch ist kein Beendigungsbeweis. Bekannte Operations-ID
erlaubt reines Nachlesen, keinen erneuten Send. Fehlt sie trotz möglicher Annahme, bleibt
die Anfrage Unknown. Nach Host-Neustart erfolgt keine automatische Fortsetzung eines
unterbrochenen Auftrags; die Recovery-Schnittstelle kann seinen Zustand ausdrücklich prüfen.

**Rationale**: Lokaler Commit und Herstelleraufruf bilden keine gemeinsame Transaktion.
Konservative Reservierungen vermeiden doppelte externe Effekte.

**Alternatives considered**: Automatische POST-Retries, Ersatzhersteller, Erfolg aus
Prozess-/HTTP-Status ableiten, clientseitige Tools als Providerfunktion nachbauen.

Quellen: [OpenAI Background](https://developers.openai.com/api/docs/guides/background),
[OpenAI Function calling](https://developers.openai.com/api/docs/guides/function-calling),
[Gemini Background](https://ai.google.dev/gemini-api/docs/background-execution).

## R-05 — Grenzen nativer Speicherung und kompatibler Zugänge

**Decision**: Endpunkt, API-Vertragsversion, Modell, hostseitige Credentials und HttpClient
sind adapterbezogen konfigurierbar. Kompatibilität bedeutet den tatsächlich benötigten
Sitzungsvertrag, nicht nur ähnliche Chat-Nachrichten. OpenAI-kompatible Chat Completions
allein erfüllen den Conversations/Responses-Vertrag nicht.

OpenAI Conversation-Objekte unterliegen laut Dokumentation nicht der gewöhnlichen
30-Tage-Response-TTL. Google dokumentiert Interactions-Aufbewahrung von einem Tag im
Free-Tier und bis 55 Tagen im Paid-Tier, mit konfigurierbaren Paid-Fristen.
Diese Providerregeln sind keine lokale Garantie und kein unbegrenztes Modellkontextfenster.
Abgelaufene oder gelöschte native Zustände blockieren Fortsetzung; keine neue Sitzung
unter alter Identität und kein Replay aus lokalem Verlauf.

**Rationale**: Der Nutzer will Sitzungen verwalten, nicht deren Funktionen nachbilden.
Lokale Persistenz erhält Nachweise und Warteschlangen, ersetzt aber verlorenen Providerzustand nicht.

**Alternatives considered**: Beliebige OpenAI-kompatible Endpunkte zusichern; alle
Herstellerfeatures katalogisieren; fehlenden nativen Zustand aus Chatverlauf rekonstruieren.

Quellen: dieselben offiziellen Sitzungs- und Retention-Dokumentationen wie R-03/R-04.

## R-06 — Dauerhafte FIFO und Persistenz

**Decision**: `Microsoft.Data.Sqlite` 10.0.14; lokaler Store, ein aktiver Besitzer,
kurze serialisierte Transaktionen; `journal_mode=DELETE`, `synchronous=EXTRA`, Foreign Keys
aktiv, keine Shared Cache-Verwendung. Eine eigene begrenzte Datenbank-Arbeitswarteschlange
isoliert die synchron arbeitenden SQLite-Aufrufe vom Aufrufer und erlaubt keine unbegrenzte
Task-Erzeugung. Keine SQL-Transaktion bleibt über einen Provider-Aufruf offen.

Annahme mit monotoner Sitzungssequenz und Queue-Kapazität transaktional committen.
Dispatch mit Reservierung vor externem Aufruf persistieren. Ergebnis, Fortsetzungsstand
und sichere Freigabe gemeinsam committen. Channels dienen nur als begrenzte Wecksignale;
die Datenbank bleibt maßgeblich, auch bei einem verlorenen Wecksignal nach Commit.

**Rationale**: Ziel sind zunächst zehn Sitzungen, kein verteilter Broker. Ein serialisierter
Store macht WAL-/Checkpoint-Komplexität unnötig. Auch Async-Methoden von Microsoft.Data.Sqlite
führen laut Dokumentation synchron aus. DELETE/EXTRA ist eine bewusste Alternative zu WAL/FULL;
Haltbarkeit hängt zusätzlich vom tatsächlichen Dateisystem und Datenträger ab.
Der Busy-Timeout wird explizit auf eine Sekunde begrenzt; der längere Standardwert darf
die lokale Abbruchbeobachtung und endliche Abschaltfrist nicht verdeckt überschreiten.

**Alternatives considered**: In-Memory-Queue verliert angenommene Arbeit; mehrere JSON-Dateien
bilden keine gemeinsame Transaktion; PostgreSQL/Broker erzwingen einen Dienst; WAL/FULL
ist bei später nachgewiesenem Parallel-Lesebedarf eine gesonderte Änderung.

Quellen: [Transactions](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/transactions),
[SQLite Async limitations](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/async),
[Database errors](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/database-errors),
[SQLite synchronous](https://sqlite.org/pragma.html#pragma_synchronous),
[Channels](https://learn.microsoft.com/en-us/dotnet/core/extensions/channels).

## R-07 — Speicherbesitz, Rechte und Vertrauensbereich

**Decision**: Host gibt absolutes privates Zustandsverzeichnis vor. Windows prüft/restringiert
ACLs für die ausführende Identität; Linux Verzeichnisse 0700 und Dateien 0600.
Ein während der Laufzeit offenes exklusives Lockfile verhindert einen zweiten kooperierenden
Besitzer. Pfadaliase, Links, UNC-/Netzwerkpfade und bestehende fremde Ablagen werden ausdrücklich
geprüft beziehungsweise abgewiesen; keine Sperre wird allein anhand einer PID-Datei gelöscht.

Ein Vertrauensbereich je Instanz. Keine eigene Authentifizierung, Mandantenverwaltung,
DPAPI-Schlüsselablage oder Sandbox. Geheimnisse kommen bei Bedarf aus einem Host-Resolver.
Provider-Endpunkte werden aus vertrauenswürdiger Host-Konfiguration übernommen, nicht aus
Modelltexten; Authentifizierungsheader werden nicht an andere Hosts weitergeleitet.

**Rationale**: Echte Plattformaufgabe ohne Betriebssystemabhängigkeit im Kern. Lokale Rechte
schützen nicht vor beliebigem Code desselben Benutzers oder einem bösartigen Provider-Plugin.
Die tatsächliche Lock-Semantik ist auf beiden Plattformen durch separate Prozessproben zu belegen.

**Alternatives considered**: DI-Singleton als vermeintliche Prozesssperre; systemd als
Bibliotheksvoraussetzung; voreilige eigene Verschlüsselungs-/Credential-Infrastruktur.

Quellen: [FileStream.Lock](https://learn.microsoft.com/en-us/dotnet/api/system.io.filestream.lock),
[UnixFileMode](https://learn.microsoft.com/en-us/dotnet/api/system.io.unixfilemode),
[DirectorySecurity](https://learn.microsoft.com/en-us/dotnet/api/system.security.accesscontrol.directorysecurity).

## R-08 — Versionierte Abhängigkeiten und Tests

**Decision**: Zentrale spätere Paketversionierung mit Lockfiles und gesperrtem Restore.
Die folgenden konkreten Paketversionen und Lizenzangaben wurden über offizielle NuGet-
Index-/Nuspec-Metadaten gelesen; Einführung und transitive Prüfung erfolgen erst bei Umsetzung.

| Paket | Version | Lizenz | Verwendung |
| --- | --- | --- | --- |
| Microsoft.Data.Sqlite | 10.0.14 | MIT | ausschließlich Storage.Sqlite |
| Microsoft.Extensions.Logging.Abstractions | 10.0.14 | MIT | strukturierte Kern-Diagnosen ohne Host-Vorgabe |
| Microsoft.NET.Test.Sdk | 18.11.0 | MIT | Testentwicklung |
| xunit | 2.9.3 | Apache-2.0 | Testentwicklung |
| xunit.runner.visualstudio | 3.1.5 | Apache-2.0 | VSTest-Ausführung |

Provider verwenden HttpClient/System.Text.Json aus .NET 10; keine zusätzlichen Hersteller-SDKs
oder automatischen Retry-Middleware-Pakete. Test-Agents und kontrollierte HttpMessageHandler
ersetzen Live-Zugänge in deterministischen Tests. Reale Provider- und Plattformabnahme bleiben
separate Gates; fehlende Zugangsdaten bedeuten NOT RUN, niemals PASS.

**Rationale**: Kleinere Abhängigkeitsfläche, kein eigener Agent-Loop, dieselben Verträge
für Test- und reale Adapter. Kompatibilität der gesamten Test-Paketkombination wird nicht
aus der bloßen Existenz ihrer NuGet-Metadaten abgeleitet.

**Alternatives considered**: Floating-Versionen; zweites Testframework; komplettes
Agent-Framework nur für zwei schmale native HTTP-Verträge.

Quellen: offizielle Flatcontainer-Nuspecs unter
`https://api.nuget.org/v3-flatcontainer/<paket>/<version>/<paket>.nuspec` für die obigen Pakete.
Vor Freigabe zusätzlich tatsächliche native SQLite-Version, transitive Lizenzen und
Vulnerabilities prüfen; kein Audit-/Sicherheitsnachweis ist bereits erbracht.

## R-09 — Genehmigter offizieller Codemetr-CLI-Guard

**Decision**: Der Projektverantwortliche hat den offiziellen CLI-Guard
`Codemetr.CSharp.Tool` 0.1.0 mit Standardgrenzwerten ohne Debt-Baseline ausdrücklich
genehmigt. Constitution v1.6.0 benennt diesen Prüfer statt einer unbestätigten
Marketplace-Erweiterungsidentität. Das Tool ist lokal gepinnt und wiederhergestellt.
Der Guard aus `codemetr/skills`, Commit `b3d1f948e536fe18903ed9e4fcad5962ec0e0a88`,
Plugin 0.2.0, ist als OpenCode-Projektadaption `codemetr-guard` mit MIT-Lizenz eingebunden.
Skill und Tool haben unabhängige Versionen; ein Skill ist keine automatische Durchsetzung.

Für Q-01 sind Genehmigung und Setup in `evidence/codemetr-approval.md` dokumentiert.
Die tatsächliche Prüfung jeder geänderten eigenen Klasse einschließlich Tests/Host mit
Quellstandbezug und erneuter Prüfung nach Refactoring bleibt vor Codefreigabe zwingend.
Full-Check: `dotnet tool run codemetr-csharp -- check --format json` vom Projektroot;
CI unter Windows/Linux, Exit 1/2 und leere Messung blockieren. Keine Produktprüfung behauptet.

Genehmigte CLI-Defaults, explizit in `codemetr.json` festgehalten:
Typzeilen ≤600, Konstruktorparameter ≤6, öffentliche Methoden ≤10, Instanzfelder ≤8,
Member-Komplexität ≤25, Maintainability Index ≥15, Kopplung ≤40, Vererbungstiefe ≤3,
Memberparameter ≤7 und Verschachtelung ≤4. Benannte Klassen dürfen nach diesen Defaults
unter anderem nicht auf Manager, Helper oder Processor enden.

Das Paket dokumentiert analyze/check/baseline und maschinenlesbare Berichte. Syntax und
vollständige Messabdeckung sind am gepinnten Paket zu prüfen; aktueller Repository-main
belegt nicht dieselbe veröffentlichte Version. Partialtypen und unaufgelöste externe
Referenzen sind explizite Messgrenzen. Kein Debt-Baseline-Import, um neue Befunde grün zu machen.

**Rationale**: Metriken unterstützen Review, ersetzen aber weder Tests noch einen
identifizierten Pflichtprüfer. Unverifizierte Werkzeuge dürfen nicht als bestanden gelten.

**Alternatives considered**: CLI ohne Zustimmung als Marketplace-Erweiterung ausgeben;
beliebige IDE-Metriken verwenden; reine Zahlenprüfung als SOLID-/Verhaltensnachweis behandeln.

Quellen: [NuGet 0.0.1](https://www.nuget.org/packages/Codemetr.CSharp.Tool/0.0.1),
[Publisher-Quellen](https://github.com/codemetr/Codemetr.CSharp),
[Skills-Marketplace](https://github.com/codemetr/skills).

Nacharbeit: [NuGet 0.1.0](https://www.nuget.org/packages/Codemetr.CSharp.Tool/0.1.0),
[gepinntes Guard-Skill](https://github.com/codemetr/skills/blob/b3d1f948e536fe18903ed9e4fcad5962ec0e0a88/plugins/codemetr-csharp/skills/guard/SKILL.md).
`dotnet tool restore` und `--help` erfolgreich ausgeführt. Der Root-Check liefert bei
derzeit null C#-Dateien Exit 0; das ist nur ein Setupnachweis, keine Codefreigabe.

## R-10 — Bewusste Abnahmegrenzen

**Decision**: Designentscheidungen sind getroffen; keine technische Auswahlfrage bleibt
als unaufgelöster Platzhalter stehen. Nicht ausgeführte Nachweise werden als Gates geführt:
Q-01 Codemetr-Freigabe, Q-02 reproduzierbarer Restore/Build/Audit, Q-03 plattformübergreifende
FIFO-/Crash-/Lock-/Rechteproben, Q-04 reale native Sitzungen beider Hersteller,
Q-05 NuGet-Konsum ohne Source-Referenzen. Das unbekannte Ratifikationsdatum der Constitution
bleibt ein manuell zu bestätigendes Governance-TODO und wird nicht erfunden.

**Rationale**: Ein Plan darf zukünftige Prüfarbeit festlegen, aber keinen grünen Build,
Live-Zugang oder eine freigegebene Governance-Baseline behaupten.

**Alternatives considered**: Entwurfsprüfung mit Produktfreigabe gleichsetzen;
fehlende Live-Zugänge über Fake-Ergebnisse verbergen.

## R-11 — Langfristiger konsumierender Zielhost

**Decision**: SharpClaw soll die Bibliothek langfristig im eigenen Prozess als
NuGet-Abhängigkeit nutzen. Diese explizite Produktanforderung wird über die vorhandene
öffentliche Host-/Providergrenze und einen isolierten Konsumnachweis berücksichtigt.
Es entsteht weder eine umgekehrte Bibliotheksabhängigkeit noch ein obligatorischer
zusätzlicher Multisession-Service. Keine erneute Übernahme temporärer Rechercheunterlagen.

**Rationale**: Der gewünschte produktive Konsument soll keine Bibliotheksinternas
oder Quellcodekopien benötigen. Er kann eigene Provideradapter registrieren und
Lebenszyklus sowie Konfiguration weiter selbst verantworten. FR-027 und SC-011
prüfen diese Voraussetzung ohne vorschnelle Eingriffe in die konsumierende Anwendung.

**Alternatives considered**: Anwendungsspezifischer Fork, direkte Source-Referenzen,
Zwang zu einem separaten Service oder sofortige vollständige Bestandsmigration.
Die tatsächliche Integration und Übernahme bestehender Sitzungen sind ein eigenes
späteres Vorhaben; es wird kein Kompatibilitätsnachweis aus Dokumentation allein abgeleitet.

## R-12 — Erneute Planung mit verbindlichem TDD

**Decision**: Constitution v1.6.0 ist die verbindliche Grundlage. Bestehende
Anforderungen einschließlich FR-027 werden testgetrieben umgesetzt: ausführbaren
Verhaltenstest zuerst fachlich rot beobachten, minimale Implementierung bis Green,
danach Refactoring mit grünen Regressionstests und erneuter Codemetr-Prüfung.
Der Plan ordnet TDD-01–TDD-07 den Anforderungen und Erfolgskriterien zu.

**Rationale**: Dies ist eine ausdrückliche Nutzer-/Governance-Entscheidung, keine neue
Technikoption. Die vorhandenen Provider-Fakes, kontrollierten HTTP-Verträge und echten
temporären SQLite-/Prozessproben ermöglichen Red ohne produktive Credentials.
Ein nicht kompilierender Test, eine fehlende Bibliothek oder ein fehlender API-Key
ist kein fachlicher Red-Nachweis. Echte Hersteller-, Paket- und Zielplattformabnahme
bleiben zusätzliche Nachweise nach deterministischem Green.

**Alternatives considered**: Tests erst nach kompletter Implementierung; nur ein grüner
CI-Lauf am Ende; Live-Providerfehler als scheinbarer Red-Ersatz; künstlich rote Tests
für rein verhaltenserhaltende Refactorings. Diese Alternativen erfüllen die Vorgabe nicht.

Q-06 hält Testkennung, Befehl, Quellstand/Hash, beobachteten Red-Grund und anschließendes
Green fest. Die vorangegangenen Technikentscheidungen bleiben nach erneuter Bewertung
geeignet; ein weiterer Anbieter-/Frameworkvergleich ist durch TDD nicht nötig.
Die Quelle dieser Entscheidung ist die aktualisierte Projekt-Constitution und der
ausdrückliche Nutzerauftrag. In diesem erneuten Planungslauf wurden keine Testquellen,
Produktivdateien oder künstlichen Red-/Green-Protokolle erzeugt und keine Tests ausgeführt.

## R-13 — Nachgeholter Abgleich mit dem aktuellen Zielkonsumenten

**Quellstand**: SharpClaw `origin/main`, Commit
`71d3cbe731ff5f94a113aaf82584daad8eccbedd`, am 2026-10-27 per Fast-forward
aktualisiert. Sauberer Arbeitsbaum vor und nach dem Update. Nur versionierte Quellen,
Dokumentation und Tests gelesen; keine Instanzdaten, Anmeldungen oder Sitzungslogs.
Keine SharpClaw-Quellen geändert und keine SharpClaw-Tests oder Agenten ausgeführt.

**Befunde**:

- Der Host konsumiert `IAgentBackend.ExecuteAsync(RunContext, prompt, observe,
  cancellationToken)`. CLI-Protokolle implementieren `ICliProtocol` und werden über
  `CliBackend` mit einem gemeinsamen Prozessrunner ausgeführt; SDK-Backends können
  unmittelbar `IAgentBackend` implementieren. Der aktuelle Pluginvertrag ist Version 3.
- Die Registry wählt registrierte Backends über technische IDs und Polymorphie;
  unbekannte/doppelte IDs werden abgewiesen. Gebündelt sind Codex, Grok und Claude.
  Im geprüften C#-Quellstand wurde kein eigener Gemini-/OpenAI-REST-Adapter gefunden.
  Codex ist ein Agent-CLI-Protokoll, nicht gleichbedeutend mit der Responses-Modell-API.
- Das externe OpenCode-Protokollplugin übergibt das konfigurierte `provider/model`
  unverändert und verwendet für Fortsetzung die gespeicherte opaque `ses_…`-Kennung.
  Es kennt keine OpenAI-/Gemini-Verzweigung. Der Quellstand pinnt ausdrücklich OpenCode
  **1.19.0**; das ist kein Nachweis für Kompatibilität mit der aktuellen OpenCode-V2-API.
- Sitzungen binden Backend, Workspace, Modell und native ID. Der Host speichert
  Attempted vor Ausführung, beobachtete native IDs vor weiterer Nutzung und verweigert
  einen unerwarteten Identitätswechsel. Abschluss erfordert terminales Erfolgssignal
  zusätzlich zu Exitcode 0; Tool-Zwischenschritte sind kein Abschluss.
- Die vorhandenen OpenCode-Tests beschreiben identische Session bei Folgeauftrag,
  Ablehnung falscher Version, fehlender/wechselnder ID und falscher Erfolgssignale.
  Diese Tests wurden gelesen, nicht ausgeführt. Der vorhandene RunHost nutzt
  Linux-/systemd-Prozessmechanismen; er ist keine Windows-Abnahme der neuen Bibliothek.

**Auswirkung auf den bisherigen Entwurf**: FR-027 verlangt einen echten
Einbettungsfähigkeitsnachweis. Ein beliebiger Testadapter plus zwei Modell-REST-Adapter
belegen nicht, dass der Ausführungs-, Ereignis-, Workspace- und Fortsetzungsvertrag
eines vorhandenen Agent-Backends darstellbar ist. Reine Modellanfrage und Agentauftrag
dürfen nicht gleichgesetzt werden; die Bibliothek darf die fehlende Werkzeug-/Agent-
Laufzeit gemäß Umfangsvorgabe auch nicht selbst nachbauen.

**Offene Entwurfsprüfung vor fachlicher Implementierung**: Die generische Adaptergrenze
muss Agent-Backends neben Modellzugängen tatsächlich darstellen können, insbesondere
Workspace, stabile native ID, Abschluss-/Fehlersignal, Abbruch und ungewisse Übergabe.
Den bestehenden ISessionProvider-/ProviderReply-Entwurf dagegen abgleichen und passende
Vertragsfälle vor Green ergänzen. Direkte OpenAI-/Gemini-Anbindungen sind damit keine
bereits bestätigte Integrationslösung für den Zielhost. Die Wahl einer zwingenden
OpenCode-Laufzeit würde zudem FR-022 ändern und wird hier nicht still vorgenommen.

Die Abhängigkeitsrichtung bleibt Zielhost → Bibliothek. Keine Quellcodekopien,
projektübergreifenden Source-Referenzen, Bestandsmigration oder Änderungen laufender
nativer Sitzungen. Die bisherige Aussage vollständiger Implementierungsbereitschaft
ist bis zu diesem Vertragsabgleich ausdrücklich eingeschränkt.

## R-14 — Generischer Vertrag statt Gleichsetzung von Agent und Modell

**Decision**: `ISessionProvider` bleibt eine transportneutrale Adaptergrenze. Ergänzt
werden explizite Fähigkeiten und ExecutionKind, eingefrorener Workspace-Kontext,
awaitable Session-/Operationsbeobachtungen und ein bestätigter terminaler Reply.
Konkrete Abbildung und zuerst zu schreibende Vertragsfälle stehen in
provider-contract.md §8 und public-api.md §9. HTTP-Endpointkonfiguration bleibt beim
HTTP-Adapter; sie ist kein Pflichtfeld hosteigener CLI-/SDK-Backends.

Der Projektverantwortliche hat **OpenCode 1.19.0 ausdrücklich beibehalten**.
Diese konkrete Referenz ersetzt für den Referenzadapter jede unbestätigte Annahme
neuerer V2-Verträge. OpenCode bleibt optional, da FR-022 keine Pflichtlaufzeit erlaubt.
Die bereits geplanten nativen Modelladapter sind explizit Modellzugänge, kein Ersatz
für einen zugesicherten Agentauftrag. Eigene Tools/Compaction bleiben ausgeschlossen.

**Rationale**: Eine generische Registrierungsgrenze kann sowohl bestehende Agent-
Backends als auch Modellprotokolle aufnehmen, ohne im Kern Produktnamen zu prüfen
oder eine Agent-Laufzeit nachzubauen. Stabile native Identität und wechselnde
Fortsetzung bleiben getrennt; Beobachtungscallbacks bewahren die Persistenzgrenze.

**Alternatives**: OpenCode als einzige Pflichtlaufzeit (verletzt FR-022); Modellantwort
als Agentabschluss behandeln (verletzt Constitution II); Sourceabhängigkeit zum
Zielhost (verletzt FR-027). Tatsächliche Tests/Konsumabnahmen bleiben ausdrücklich offen.

## R-15 — Erneuter Abgleich nach Änderungen im Zielhost (2026-10-28)

Aktueller geladener SharpClaw-Stand: `7854a122218382156789d520b0a00f2060b1f8f3`.
Der Vergleich seit R-13 bestätigt unveränderte Backend-/Session-/Pluginverträge und
weiterhin OpenCode 1.19.0. Neu sind insbesondere explizite Fleet-Recovery,
aufgabenspezifische Beobachtung, deklarierte Ausgabepfade und hashgebundene unveränderliche
Kontextquellen. Diese Funktionen bleiben Hostverantwortung, keine neue Multisession-
Abhängigkeit oder Lizenz zum Nachbau einer Agent-Laufzeit. Vorbereitete Eingaben werden
unverändert angenommen, nicht durch erneutes Lesen mutierbarer Kontextdateien ersetzt.
Commit, Dateihashes, Deep-Dive-Befunde, Regressionsrisiken und nächster begrenzter
Testplan sind in `evidence/architecture/2026-10-28-sharpclaw-refresh-request-admission.md`
vor Umsetzung dokumentiert. Quellanalyse ist keine neue Binär-/Live-/Migrationsabnahme.
