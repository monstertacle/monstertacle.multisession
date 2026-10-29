# Implementation Plan: Herstellerübergreifende Agent-Multisession

**Branch**: `001-agent-multisession` (logische Feature-ID); initialer Repositorybranch `main`.
| **Date**: 2026-10-27 | **Spec**: [spec.md](spec.md)

**Input**: `specs/001-agent-multisession/spec.md`

**Aktueller Ausführungsabgleich2026-10-29T23:04:34Z:** Runtime, beide nativen
Provideradapter, SQLite/Recovery, beide Plattformleasen, Diagnostik, ValidationHost
und Paketkonsum sind implementiert; einzelne frühere Statusabsätze darunter
beschreiben den damaligen Planungsstand. T112–T115 sind inzwischen ausgeführt,
keine NotImplemented-Gerüste im Produktcode. Maßgeblich für aktuelle Aufgaben
ist tasks.md, für technische Nachweise evidence/acceptance-audit.md/build-native.json,
für fehlende Abnahme evidence/release.md/remaining-acceptance.md. Keine Produktfreigabe:
fehlende Win11-/Live-/Integrator-/historische Q06-Belege bleiben gesperrt;
intermittierender Server-Recoverybesitz wird separat T125 untersucht.

**Status**: Phase 0 und Phase 1 dokumentiert; inkrementelle Implementierung läuft,
keine Produktfreigabe. Aktuelle Teilnachweise und Grenzen: tasks.md sowie
evidence/tdd-foundation.md, tdd-us1.md, tdd-us2.md und coverage.md.
Nach FR-027 und Constitution v2.0.0 erneut geprüft: generische Backendgrenze, verbindliches
TDD, mindestens 87 % Zeilenabdeckung und offizieller Codemetr-Guard. Genehmigt sind
Toolversion und Standardgrenzwerte, keine Debt-Baseline; finale Q-01–Q-06 bleiben offen.

**Zielhostabgleich**: R-13/R-14 und provider-contract.md §8 konkretisieren den tatsächlich
untersuchten Agent-Backend-/Sessionvertrag: explizite Fähigkeiten/ExecutionKind,
Workspace-Snapshot, awaitable Beobachtungen für opaque native Session-/Operations-ID,
terminaler Reply und getrennte Gewissheit. T010/T035/T089 müssen diese Vertragsfälle
vor Green erfüllen. Der Entwurfsabgleich ist abgeschlossen, tatsächlicher Zielhost-/
Paketkonsum bleibt offen. Keine Änderung des Zielhostcodes oder Bestandsmigration.
OpenCode **1.19.0** bleibt auf ausdrücklichen Nutzerentscheid die feste Referenz;
kein Upgrade, keine neuere V2-API und keine zwingend vorgeschaltete OpenCode-Laufzeit.
Aktualisierung 2026-10-28: R-15 prüft SharpClaw-Commit `7854a12` gegen den ursprünglichen
Analysecommit. Die relevante Backend-/Sessiongrenze ist unverändert; neue Kontextcache-,
Output- und Fleetdienste bleiben im Host. Deep-Dive und Testplan vor Request-Annahme:
`evidence/architecture/2026-10-28-sharpclaw-refresh-request-admission.md`.

## Summary

Wiederverwendbare C#-/NuGet-Bibliotheken verwalten parallele Provider-Sitzungen mit
getrennter Identität, dauerhaft angenommener FIFO, endlichen Limits und konservativer
Recovery. Kontextverdichtung, Werkzeuge und Agent-Schleifen verbleiben vollständig
beim Provider. Eine Instanz gehört einem Vertrauensbereich; der Host kontrolliert den Zugriff.

Ein Common-Kern enthält die Fachlogik. Separate Windows-/Linux-Pakete übernehmen
Speicherrechte und exklusiven Besitz. SQLite-Persistenz und zwei native HTTP-Provider
sind getrennte Pakete. Ein späterer Konsolen-/Dienst-/Daemon-Host stellt Lebenszyklus,
Konfiguration und Anfrageeingang bereit; dieser Plan implementiert keinen Dienst.
SharpClaw ist ausdrücklich als langfristiger konsumierender Host vorgesehen. Die
Abhängigkeitsrichtung bleibt `SharpClaw -> NuGet-Bibliotheken`, niemals umgekehrt.
Seine tatsächliche Produktmigration oder Zustandsübernahme ist eine separate spätere Lieferung.

Referenzanbindungen sind OpenAI Conversations/Responses und Gemini Interactions `/v1`.
Es werden native Sitzungsbezüge fortgeführt, keine Chatverläufe zur Simulation einer
Sitzung erneut eingespeist. Technische Entscheidungen und Quellen: [research.md](research.md).

## Technical Context

**Language/Version**: C# 14, .NET 10 LTS, SDK 10.0.403 exakt gepinnt; `net10.0`,
Windows-Plattformpaket `net10.0-windows`; keine Prereleases oder impliziten SDK-Upgrades.

**Scripting-/Toolinggrenze**: Gemäß Constitution v2.0.0 ist Python für Produktcode,
Tests, Build-/CI-Skripte, Codegenerierung und agentengestützte Projektarbeiten verboten,
einschließlich temporärer Hilfsskripte und einmaliger Agentenbefehle. C#/.NET sowie bei
Bedarf PowerShell beziehungsweise Shellskripte verwenden; keine Python-Laufzeit als
Projektvoraussetzung einführen. Vom Nutzer am 2026-10-28 ausdrücklich genehmigt.

**Primary Dependencies**: Microsoft.Data.Sqlite 10.0.14;
Microsoft.Extensions.Logging.Abstractions 10.0.14; BCL HttpClient, System.Text.Json,
Channels, TimeProvider und Diagnostics. Kein Agent-Framework, kein obligatorischer
Hosting-Stack und keine automatischen POST-Retries. Metadaten/Lizenzen siehe R-08.

**Storage**: Eine lokale SQLite-Datenbank je Vertrauensbereich; exklusiver Besitzer;
DELETE-Journal mit synchronous=EXTRA und Fremdschlüsseln. Einzelner begrenzter
DB-Arbeitskanal; keine SQL-Transaktion über externe Aufrufe. Credentials nur über Host-Resolver.

**Testing**: xUnit 2.9.3, xunit.runner.visualstudio 3.1.5, Microsoft.NET.Test.Sdk 18.11.0;
deterministische Provider-Fakes, HTTP-Vertragsfixtures, echte Prozess-Kill-/Neustarttests,
Plattform-Rechte-/Locktests, separate explizite Live-Provider-Abnahme.
Für jede neue/geänderte Verhaltensanforderung zuerst ausführbaren fachlich roten Test
beobachten, dann minimale Implementierung, anschließend Refactoring bei grünen Tests.
Mindestens 87 % Zeilenabdeckung des gesamten eigenen Produktivcodes als CI-Gate unter
Windows und Linux. Gemeinsame Bibliotheken und das jeweilige native Plattformpaket
vollständig messen, einschließlich nicht durch Tests geladener Assemblies. Die fremde
Plattformbibliothek ist im Scopeinventar explizit dem anderen nativen Lauf zuzuordnen;
kein Produktprojekt darf in beiden Läufen fehlen. Berichte mehrerer Testsuiten je
Plattform zeilenweise vereinigen, nicht Prozentwerte mitteln. Testcode zählt nicht zum
Coverage-Nenner; generierter Code/Fremdcode nur begründet ausschließen.
Collector, Version, Runsettings, Aggregation und Gate-Implementierung werden in T005/T015
festgelegt und gepinnt; fehlende/leere Berichte oder weniger als 87 % sperren die Freigabe.
Der Projektverantwortliche hat die Änderung von 90 auf 87 % am 2026-10-28 ausdrücklich
genehmigt; Änderungsnachweis `evidence/architecture/2026-10-28-coverage-87.md`.

**Quality Guard**: `Codemetr.CSharp.Tool` 0.1.0 lokal gepinnt, genehmigte Standardregeln
in `codemetr.json`, keine Debt-Baseline. Vollständiger Check vom Root einschließlich
Tests/ValidationHost: `dotnet tool run codemetr-csharp -- check --format json`.
Projektlokaler OpenCode-Skill `codemetr-guard` und `.github/workflows/codemetr-guard.yml`
sind eingerichtet; echte CI-Läufe benötigen weiterhin ein angebundenes Git-Repository.

**Target Platform**: Windows 11 25H2 x64, Windows Server 2025 x64 und Ubuntu 24.04 LTS x64.
Framework-dependent Hosts mit .NET-10-Runtime. Andere Architekturen/Distributionen,
Windows-Dienstinstallation und Linux-Service-Units sind nicht Teil der ersten Lieferung.

**Project Type**: Sechs NuGet-Bibliotheken und ein nicht produktiver Konsum-/Abnahmehost.
Keine Webendpunkte, Oberfläche, CLI-Produkt oder Betriebsinstallation.

**Performance Goals**: SC-001–SC-010 gelten unverändert: zehn Sitzungen, korrekte
Zuordnung für 100 Anfragen, nachweislich überlappende unabhängige Arbeit, lokale
Abbruchbeobachtung innerhalb zwei Sekunden mit kooperativen Providern, zehn Sekunden
Abschaltfrist im entsprechenden Test und eine Stunde Dauerabnahme pro Zielsystem.
Keine Zusicherung herstellerseitiger Antwortlatenz oder unbegrenzter Kontextlänge.
SC-011 ergänzt einen eingebetteten Konsumnachweis ohne Änderung am Bibliotheksquellcode
oder zusätzlichen Multisession-Prozess als Voraussetzung für den langfristigen Zielhost.

**Constraints**: Kein eigener Agent-/Tool-Loop, kein Kontext-Replay, keine Verdichtung,
kein stiller Ersatzprovider; persistierte ungewisse Arbeit wird nicht automatisch wiederholt.
Kein Netzwerkdateisystem, Cluster, untrusted Plugin-Sandbox oder internes Mandantenmodell.
Die Paketstruktur ist angelegt; die Übersicht unten beschreibt auch noch geplante Teile.
Vorhandene Gerüste/Teilimplementierungen sind keine abgeschlossenen Produktfunktionen.

**Scale/Scope**: Ein aktiver logischer Host je Store. Startwerte: MaxConcurrentRequests=4,
MaxQueuedRequests=100, MaxInputBytes=1 MiB, MaxOutputBytes=4 MiB, RequestTimeout=600 s,
ShutdownTimeout=10 s und DbWorkQueueCapacity=256. Positive endliche Werte erforderlich;
SQLite-Busy-Timeout=1 s statt des langen Bibliotheksdefaults.
AdmissionTimeout=5 s begrenzt die lokale Annahme; bereits Queued gespeicherte Anfragen
haben keine automatische Wartefrist. Anfrage- und Stop-Fristen werden monoton gemessen.
Größere Werte werden nicht allein durch Konfiguration als lastgetestet angesehen.
Aufbewahrung muss der Host ausdrücklich konfigurieren oder RetainUntilExplicitDeletion wählen.

## Constitution Check

*GATE: vor Phase 0 prüfen und nach Phase 1 erneut bewerten. Entwurfskonformität ist
nicht dieselbe Aussage wie erfüllte Code-/Produktfreigabe.*

Erneuter Prüflauf: Constitution v2.0.0 und FR-027 sind die aktuelle Grundlage.
Maßgeblich ist die verbindliche generische
Agent-/Providergrenze. Die in R-13 festgestellte Vertragslücke wurde im Entwurf durch
provider-contract.md §8 geschlossen; ausführbare Nachweise bleiben offen.
Die vorhandenen Technikentscheidungen wurden gegen TDD erneut bewertet; keine neue
Technikauswahl oder Ausweitung auf eine eigene Agent-Laufzeit ist erforderlich.

| Prinzip / Vorgabe | Vor Phase 0 | Nach Phase 1 | Nachweis im Entwurf |
| --- | --- | --- | --- |
| I: C#/.NET, kein Python, Analyzer, Nullable, async/await | PASS: Stack passt, konkrete Auswahl in Recherche | PASS (Design); kein behaupteter Implementierungsnachweis | SDK-/Paketpins; Scripting-/Toolinggrenze; Cancellation und endliche Fristen im Vertrag |
| II: SOLID, DRY, KISS, YAGNI, generische Backends | Kern ohne Herstellerabhängigkeit vorgesehen | PASS (Design): Agent-/Zielhostvertrag explizit abgebildet, Ausführung offen | Common ohne Rückabhängigkeit; provider-contract.md §8; T010/T035/T089 |
| III: Pflichtprüfung mit Codemetr | PASS für Recherche ohne Code; Freigabe vorbehalten | Tool und Standardregeln genehmigt; Produktprüfung Q-01 offen | R-09, evidence/codemetr-approval.md, lokaler Skill und CI-Guard |
| IV: TDD, ≥87 % Line Coverage, Windows/Linux, CI | PASS (Design): Red vor Verhalten und Coverage-Gate vorgesehen | PASS (Design); tatsächliche Teilnachweise siehe evidence/coverage.md, keine Produktfreigabe | TDD-Strategie, T005/T015/T104, quickstart.md und Q-06 |
| V: Sicherheit und Diagnose | PASS: ein Vertrauensbereich, Host-Auth | PASS (Design) | Private Store-Rechte, keine Secrets im Zustand/Log, Audit-Gate |
| Kultur, Zeit, Pfade, unterstützte Plattformen | PASS: keine entgegenstehende Vorgabe | PASS (Design) | UTC, ordinal IDs, explizite Matrix, Plattformadapter |
| Dokumentation und Änderungskontrolle | PASS | PASS (Design) | Forschungsentscheidungen mit Alternativen, versionierte Verträge |

Unbekanntes Ratifikationsdatum bleibt unverändert als Governance-TODO in der Constitution.
Es wird kein Datum erfunden. Codemetr-Werkzeug und Standardgrenzwerte wurden vom
Projektverantwortlichen ausdrücklich genehmigt; Nachweis in `evidence/codemetr-approval.md`.
Das ist keine bereits bestandene Prüfung des erst zu implementierenden Produktcodes.

Verantwortlich für die Governance-Entscheidungen ist der Projektverantwortliche:
Er bestätigt das ursprüngliche Ratifikationsdatum, soweit ermittelbar, vor der abschließenden
Annahme der Constitution-Dokumentation. Bis dahin bleibt der vorhandene TODO sichtbar.
Er hat CLI 0.1.0 mit Standardregeln ohne Debt-Baseline bestätigt. Am 2026-10-28
wurde SDK 10.0.403 ausdrücklich freigegeben, da 10.0.402 lokal nicht mehr installiert ist.
Constitution v1.6.0 bildet diese Entscheidung ab; keine Ausnahme und kein Datum werden erfunden.
Bekannte Dokumentations-TODOs sind von nicht erfüllten Q-01-/Produktfreigabegates getrennt.

Q-01 bleibt ein finaler Freigabenachweis; vorhandene lokale Codemetr-Ergebnisse stehen in
den jeweiligen Arbeitsnachweisen. Sie sind weder eine CI-Abnahme noch eine allgemeine
Codefreigabe. Jede weitere Erweiterung benötigt ihre aktuelle Bestandsanalyse.

## Project Structure

### Documentation (this feature)

```text
specs/001-agent-multisession/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── public-api.md
│   ├── provider-contract.md
│   └── orchestration-details.md
└── checklists/
    ├── requirements.md
    └── multisession.md
```

`tasks.md` liegt vor und steuert die inkrementelle Umsetzung einschließlich der
Ergänzungen T111–T115 aus der Analysebereinigung vom 2026-10-28.

### Source Code (repository root)

Zielaufbau: Projekte und Teile des Common-Kerns sind vorhanden; noch nicht implementierte
Bestandteile bleiben in tasks.md offen. Die Übersicht ist kein Fertigstellungsnachweis:

```text
Monstertacle.Multisession.slnx
global.json
Directory.Build.props
Directory.Packages.props
src/
├── Monstertacle.Multisession.Common/
│   ├── Sessions/
│   ├── Requests/
│   ├── Scheduling/
│   ├── Recovery/
│   └── Contracts/
├── Monstertacle.Multisession.Storage.Sqlite/
├── Monstertacle.Multisession.Windows/
├── Monstertacle.Multisession.Linux/
├── Monstertacle.Multisession.Providers.OpenAI/
└── Monstertacle.Multisession.Providers.Gemini/
tests/
├── Monstertacle.Multisession.UnitTests/
├── Monstertacle.Multisession.ContractTests/
├── Monstertacle.Multisession.IntegrationTests/
└── Monstertacle.Multisession.PlatformTests/
examples/
└── Monstertacle.Multisession.ValidationHost/
```

**Structure Decision**: Plattformbibliotheken, Storage und Provider referenzieren Common;
Common referenziert keine konkrete Implementierung. Der konsumierende Host verbindet
das benötigte Plattformpaket, Storage und die ausgewählten Provider ausdrücklich.
Es entstehen keine leeren Linux-/Windows-Pakete und kein DI-/Reflection-Framework.
Die gemeinsame Bibliothek ist mit Fakes ohne SQLite oder Plattformcode prüfbar.
Testprojekte und ValidationHost erhalten IsPackable=false; nur die sechs Bibliotheken
werden als Produkt-NuGet-Pakete gepackt.

**Gemeinsamer Ablauf**:

1. Host liefert Options, Providerregistrierungen, CredentialResolver, Storage und die
   zur Plattform passende Zustandsverzeichnis-/Besitzimplementierung.
2. Start nimmt exklusiven Store-Besitz, prüft Schema und Recovery und startet begrenzte
   Arbeitskanäle. Kein HTTP-Listener wird implizit geöffnet.
3. Annahme commitet Eingabe, Sitzungssequenz und Queuelimitprüfung atomar, dann bestätigt sie.
4. Scheduler wählt FIFO-Köpfe ungesperrter Sitzungen; gleiche Sitzung und gleicher schreibender
    Workspace werden serialisiert. Ältester zulassungsfähiger Kopf wird bevorzugt.
    Bei gleichem AcceptedUtc entscheidet RequestId ordinal. Blockierte Köpfe verhindern
    keine unabhängige zulässige Arbeit; eine feste globale Wartezeit wird nicht garantiert.
5. Running/Reservierung wird vor externer Übergabe persistiert. Provider erhält nur neue
    Eingabe plus seinen zuletzt bestätigten nativen Fortsetzungsstand.
6. Ergebnis und neuer Binding-Stand committen gemeinsam; sichere Reservierung wird freigegeben.
7. Bei ungewissem Ausgang bleiben Sitzung/Workspace und Ausführungsslot reserviert.
   Unabhängige Sitzungen dürfen freie übrige Slots nutzen; belegte Kapazität wird nicht erfunden.

Schließen ist eine lokale Lebenszyklusentscheidung, kein implizites Löschen von Providerdaten.
Beim Stop werden keine neuen Aufträge angenommen. Innerhalb der Frist wird offene Arbeit
abgeschlossen; verbleibende wartende Anfragen werden ausdrücklich mit Cancelled/NotStarted
beendet. Laufende Anfragen werden kontrolliert gestoppt beziehungsweise als ungewiss gesichert.
Nach einem ungeplanten Neustart werden dagegen die noch Queued gebliebenen Anfragen fortgesetzt.
Kein angenommener Auftrag verschwindet ohne Zustand. Details: [data-model.md](data-model.md).
Sind alle Slots durch ungewisse Arbeit reserviert, bleibt weitere Arbeit wartend;
Fehlerisolation ist keine Erlaubnis, das Ausführungslimit oder Workspace-Sperren zu umgehen.

Für das erste Speicher-/Binding-Format ist keine automatische Migration geplant.
Ein inkompatibles Format wird vor Änderungen abgewiesen; ein fehlender erwarteter Store
wird nicht neu initialisiert. Spätere Formatänderungen erfordern einen eigenen
Migrations-/Rückkehrplan. Es wird hier kein unzureichend spezifizierter Updater implementiert.

## Konkretisierte Orchestrierungsverträge und Umsetzungsvoraussetzungen

Verbindliche Details: [contracts/orchestration-details.md](contracts/orchestration-details.md).
ReservedDispatch samt Query ist in T112 und der Dispatcher in T025 lokal umgesetzt;
Nachweise in evidence/tdd-us1.md. Die Recovery-/Annahme-/Pagingänderungen T113–T115
bleiben offen und werden erst nach fachlichem Red ergänzt. T009/T010 belegen nur
ihren damaligen Signaturumfang, keine vollständige Implementierung der Ergänzungen.

| Befund | Architekturentscheidung | Voraussetzung vor Green |
| --- | --- | --- |
| U1 | ReservedDispatch wird atomar mit Reservierung gespeichert; GetReservedDispatchAsync liefert Eingabe/Timeout/Sitzung/BindingRevision ohne Rekonstruktion; Completion prüft die Revision | T112 vor T025 und T051/T059/T060 |
| U2/U3 | RecoveryTarget für SessionOpening oder RequestExecution; öffentlich lesbarer RecoveryCaseSnapshot mit persistierter CAS-Revision; keine Dummy-Requests | T113 vor T052/T062 und dem betreffenden Teil von T037/T079 |
| U4 | Vorab erzeugte Kandidatenkennung in RequestAcceptance; AdmissionStatus getrennt von externer Gewissheit, bestätigter Commit liefert Receipt | T114 vor T058/T076 |
| A2 | Gemeinsame Seitengrenzen und bereichsgebundene Keysetcursor; HistoryOrdinal/HistoryId und eigene Recoveryfallsnapshots | T115 vor Pagingteilen T026/T037/T063 |

T112 und T025 sind lokal nachgewiesen. Für die nächste Runtimevertikale T026 gelten
ein aktueller Deep-Dive und passende noch fehlende Red-Fälle; Pagingteile setzen
T113/T115 voraus. Die bestehenden Storytasks bleiben bis
zu ihrer vollständigen Abnahme offen; einzelne vorgezogene Teilfälle schließen keine Story.
T112–T115 sind fachliche Ergänzungszyklen, kein gemeinsamer Block, der vor jeder weiteren
Arbeit vollständig abgeschlossen werden muss. Jeder Schnitt benötigt nur seine relevanten
Abhängigkeiten und Tests. Native SQLite-/Provider-/Recoverybelege folgen getrennt.

T015 umfasst lokale Workflow-/Runnerkonfiguration und synthetische Gate-Verhaltenstests.
Reale Windows-/Linux-CI und erforderliche Branchchecks sind T111 und T104: verpflichtende
**Freigabegates**, aber keine Voraussetzung für lokale testgetriebene Weiterentwicklung.
Ohne Repositoryanbindung bleiben sie NOT RUN; ein Repository wird nicht eigenmächtig
angelegt. Gleiches gilt für ausstehende Live-/Zielsystem-/Integratorfreigaben.

## TDD-Umsetzungsstrategie

**Voraussetzung vor jeder Erweiterung**: Zuerst den aktuellen Bestandscode per
Code-Deep-Dive und Architekturanalyse nach Constitution §II prüfen, dann den konkreten
Erweiterungsentwurf und dessen Red–Green–Refactor-Zyklen festlegen. Betroffene Module,
relevante Aufrufer/Abhängigkeiten, Verantwortlichkeiten, Verträge, Daten-/Kontrollflüsse,
Zustände/Persistenz, vorhandene Tests, Wiederverwendung und Regressionsrisiken untersuchen.
Den Nachweis mit Quellstand/Sourcehash, Datei-/Symbolbelegen, Befunden, offenen Fragen,
Architekturfolgen, gewähltem Erweiterungspunkt und Testplan unter
`specs/001-agent-multisession/evidence/architecture/<aenderung>.md` führen; `<aenderung>`
durch eine eindeutige Änderungskennung ersetzen. Ohne Nachweis oder bei ungeklärten
implementierungsrelevanten Architekturfragen keine Implementierung beginnen. Alte
Analysen müssen am aktuellen Code und Umfang erneut geprüft werden; kleine Erweiterungen
sind nicht ausgenommen. Fehlenden Bestandscode ausdrücklich dokumentieren, vorhandene
Gerüste/Verträge trotzdem untersuchen. Diese Vorgabe ist noch kein ausgeführter Deep Dive.

Jede Zeile beschreibt eine testgetriebene vertikale Umsetzungseinheit, keine Aufforderung,
erst alle Bibliotheken fertig zu implementieren und danach Tests hinzuzufügen.
Jede Einheit wird weiter in kleine Red–Green–Refactor-Zyklen zerlegt. Vor Beginn sind
nur minimale Test-/Projektgerüste und verhaltenslose Signaturen zulässig.
Fehlendes SDK, nicht restaurierte Pakete, Compilerfehler oder null entdeckte Tests
zählen niemals als erwarteter fachlicher Red-Nachweis.

| Einheit / Bezug | Zuerst ausführbare Tests | Erwarteter fachlicher Red-Grund | Minimales Green, danach Refactor |
| --- | --- | --- | --- |
| TDD-01 Sitzungen und Zuordnung; FR-003–FR-007, SC-002 | SessionRoutingTests: Kennungen, feste Zuordnung, getrennte Kontexte, Folgeanfrage, ungültige/geschlossene Sitzung | Neue Sitzung ist nicht auffindbar, falsche Zuordnung oder unzulässiger Send wird nicht verhindert | Nur Sitzungskatalog und Routing gegen kontrollierte Store-/Provider-Fakes; anschließend Verantwortlichkeiten aufteilen |
| TDD-02 FIFO, Parallelität und Arbeitsbereiche; FR-002, FR-008, FR-016, SC-001/SC-005 | SchedulingTests: angehaltene Fakes, gleiche Sitzung serialisiert, unabhängige überlappen, Limits 2/3, gemeinsamer Workspace | Zweiter unzulässiger Send startet, zulässige unabhängige Arbeit bleibt blockiert oder Queue überschreitet Grenze | Nur begrenzte Zulassung und dauerhafte Reihenfolgeregel implementieren; kein Message-Broker |
| TDD-03 Dauerhafte Annahme und Recovery; FR-018–FR-021, SC-009 | PersistenceRecoveryTests: echter temporärer Store, Prozess-Kill vor/nach Commit, verlorenes Wecksignal, ungewisser Dispatch, fehlender Store | Bestätigte Anfrage fehlt nach Wiederöffnung, ungewisse Anfrage wird erneut gesendet oder falsche Reservierung freigegeben | Transaktionen und persistierte Dispatchgrenzen einzeln ergänzen; kein Netzaufruf unter DB-Lock |
| TDD-04 Fehler, Lebenszyklus und Diagnose; FR-009–FR-013, FR-015, FR-026, SC-003/SC-004 | LifecycleTests: fremder Providerfehler, wartender Cancel, lokaler Timeout mit Unknown, Stopfrist, Instanztrennung, bereinigte Diagnosen | Unabhängige Anfrage endet falsch, Cancel löst Send aus, unbestätigtes Ende gilt als sicher oder Daten vermischen sich | Nur Zustandsübergänge und sichere Beobachtung ergänzen; keine Produktivzugänge voraussetzen |
| TDD-05 native Providerverträge; FR-003/FR-022/FR-024/FR-025, SC-008/SC-010 | ProviderContractTests: kontrollierte HTTP-Antworten, native IDs/Chain-Head, nur neue Nachricht, requires_action, Kontextgrenze, Inspect/Cancel | Falscher Bezug/Body, Historie wird replayt, Kontext selbst verändert oder clientseitige Aktion als Erfolg behandelt | Je Adapter nur dokumentierten nativen Vertrag implementieren; echte Herstellerabnahme zusätzlich nach deterministischem Green |
| TDD-06 Plattformbesitz und Rechte; FR-014/FR-023, SC-007 | PlatformOwnershipTests: echte Zielsysteme, zweiter Prozess, Pfadalias, private Rechte, falsche Plattform, kaputter Store | Zweiter Besitzer wird zugelassen, falsche Rechte akzeptiert oder nicht unterstützte Plattform still verwendet | Je Plattform nur den gemeinsamen Besitz-/Rechtevertrag erfüllen; keine Sitzungslogik duplizieren |
| TDD-07 eingebetteter Konsument; FR-001/FR-017/FR-027, SC-006/SC-011 | EmbeddedConsumerTests: separates konsumierendes Testprojekt, eigener Provideradapter, zwei Sitzungen, Folgeanfragen, Status, Abbruch und Stop | Öffentlicher Vertrag routet den Konsumablauf nicht korrekt oder erfordert internes Sonderwissen | Nur fehlendes öffentliches Verhalten ergänzen; später echter Paket-Smoke ohne Source-Referenzen und Integrator-Zeitnachweis |

Alle übrigen Akzeptanz- und Negativfälle der Spezifikation werden innerhalb der passenden
Einheit ebenfalls vor der jeweiligen Verhaltensänderung getestet. Insbesondere Retention,
Schema-/Speicherfehler und Datenschutz sind keine nachgelagerten optionalen Testaufgaben.
SC-007-Dauerbetrieb, Live-Provider und tatsächlicher NuGet-Paketkonsum ergänzen die
deterministischen Red-/Green-Schritte, ersetzen sie aber nicht.

Der Arbeitsnachweis speichert pro Zyklus FR-/SC-Bezug, Testkennung und -befehl, geprüften
Quellstand/Hash, beobachteten fachlichen Red-Grund sowie Green- und Regressionsergebnis.
Ohne Git ist ein dokumentierter Sourcehash zulässig; eine nicht vorhandene Commit-ID wird
nicht erfunden. Nachträglich erzeugte Testlogs dürfen keine frühere Testausführung vortäuschen.

Q-06 sperrt die Freigabe, wenn ein neuer Verhaltenspfad nur nachträglich grün getestet
wurde oder Red nur durch Infrastrukturfehler behauptet wird. Reines Refactoring startet
mit grüner Suite und endet mit grüner Suite plus erneuter Codemetr-Prüfung; kein künstlicher
Fehler und keine neue fachliche Funktion allein zur Erfüllung der TDD-Schablone.

Die vorhandene Taskliste modelliert Test/Red vor Implementierung/Green und Refactor.
Neue Ergänzungszyklen erhalten dieselben Abhängigkeiten und aktuelle Vorabanalysen.

## Complexity Tracking

Keine genehmigungspflichtige Constitution-Abweichung. Die bewussten Entscheidungen:

| Entscheidung | Aktueller Bedarf | Warum die einfachere Alternative nicht genügt |
| --- | --- | --- |
| Common plus zwei Plattformpakete | Private Dateirechte und exklusiver Speicherbesitz auf beiden Zielsystemen | Nur Linux-Mechanik verletzt Portabilität; Plattformcode im Kern erschwert unabhängige Tests |
| Separates Storage-Paket | Transaktionale Annahme, FIFO und Crashzustände | In-Memory-Queue oder mehrere JSON-Dateien erfüllen die Haltbarkeitsgrenze nicht |
| Zwei separate Providerpakete | Zwei reale unterschiedliche native Sitzungsverträge und unabhängige Konsumwahl | Universeller Chat-Adapter würde native Sitzungsemantik vortäuschen oder eigenen Kontext nachbauen |
| Persistierter Dispatch/Fence | Ungewisse externe Wirkung nach Crash | Automatisches Replay kann dieselbe Arbeit doppelt auslösen |

Nicht ergänzt: eigener Agent-Loop, generischer Tool-Executor, Token-/Kontextdienst,
SSH-/Remote-Gateway, Prozesssupervisor, produktiver Diensthost oder zusätzlicher Message-Broker.

## Phase 0 — Research

Abgeschlossen und erneut bewertet als Dokumentationsarbeit: R-01–R-12 in [research.md](research.md) lösen
Stack, Paketgrenzen, Providersemantik, Speicherung, Besitz und Validierungsstrategie.
Verifizierte Fakten und noch auszuführende Nachweise sind dort getrennt.

## Phase 1 — Design & Contracts

- [data-model.md](data-model.md): Identitäten, Zustände, Reservierungen, Commit- und Crash-Grenzen.
- [contracts/public-api.md](contracts/public-api.md): öffentliche Bibliotheksoperationen,
  Hostpflichten, Fehler und Lebenszyklus.
- [contracts/provider-contract.md](contracts/provider-contract.md): native Sitzungsadapter,
  konkrete Referenzprotokolle und deren Grenzen.
- [contracts/orchestration-details.md](contracts/orchestration-details.md): Dispatchdaten,
  Recoverytargets/-revisionen, Annahmegewissheit sowie Listen-/Cursorgrenzen (T112–T115).
- [quickstart.md](quickstart.md): spätere ausführbare Nachweise einschließlich SC-Zuordnung.

Post-Design-Check unter Constitution v2.0.0 siehe Constitution-Tabelle; Agentvertragsentwurf abgeglichen, vollständige Verhaltensabnahme offen.
Q-01 tatsächliche Codemetr-Produktprüfung (Tool/Regeln genehmigt), Q-02 Build/Audit/Coverage, Q-03 Plattform-/Crash-Verhalten,
Q-04 Live-Provider, Q-05 Paketkonsum und Q-06 tatsächliche Red–Green–Refactor-Nachweise sind vor Codefreigabe
am finalen Lieferstand offen. Lokale Teilprüfungen sind in den aktuellen Nachweisen
dokumentiert; dieser Plan behauptet weder eine abgeschlossene Story noch Produktfreigabe.

Ergänzung FR-027: Q-05 umfasst auch den hostähnlichen Konsumnachweis aus SC-011 mit
einer vom Konsumenten registrierten Providerimplementierung. Das ist kein Eingriff
in ein externes Anwendungsrepository und keine Zusicherung vollständiger Bestandsmigration.
