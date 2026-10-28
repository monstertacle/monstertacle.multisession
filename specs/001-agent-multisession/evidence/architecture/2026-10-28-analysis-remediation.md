# Vorabprüfung und Entscheidung: Analysebefunde beheben

Datum: 2026-10-28. Nutzerauftrag nach dem lesenden Analysebericht: „okay bitte lösen“.
Umfang: die zehn Befunde U1–U4, I1–I3, A1–A2, D1 in Planung/Verträgen/Tasks beheben.
Keine C#-Implementierung, kein Schema, kein Testresultat und keine Freigabe wird dabei
geändert. Constitution v2.0.0, 87-%-Gate und OpenCode 1.19.0 bleiben unverändert.

## Erneut untersuchter Bestand vor Entwurf

SHA256 der aktuellen Dateien, relativ zum Projektroot:

| Datei | SHA256 |
| --- | --- |
| specs/001-agent-multisession/spec.md | `2275351F0FE648E84D1445CD36B0A322484A68389438B76199BBB324A6F020C5` |
| specs/001-agent-multisession/plan.md | `75412E8F259CBBECD7A2ECB060101B5091E176C7B9265A6FF82284F15840A389` |
| specs/001-agent-multisession/tasks.md | `2FCE33B3AF24A12A95E19AA33A1D3542E9460F053AA7BDD88E8C00ABC32B53D8` |
| specs/001-agent-multisession/contracts/public-api.md | `023E6F9B030F121C093826CDC4F50F58F3176793BE28E2A75C8CDFE9B7E9BA0D` |
| specs/001-agent-multisession/data-model.md | `E83600F7B31F2B24463EECF6C687EA66D0FB380D60FE1D830FC13CB7E948A0A6` |
| src/Monstertacle.Multisession.Common/Contracts/ISessionStore.cs | `68C0497D26EA52EF921EF5BD6248AAF43353ECA207E946503777EAB11FB78E3E` |
| src/Monstertacle.Multisession.Common/Contracts/ISessionQueryStore.cs | `468FDF83D8695A00F56F88D400403874D4E97DD4366F4C362AB01FF56BB23437` |
| src/Monstertacle.Multisession.Common/Contracts/RecoveryDecision.cs | `5554BFB36DB85D930B53EAEE91BA571954F8604DCEE178430CC3ECA19441E99A` |
| src/Monstertacle.Multisession.Common/Sessions/SessionCatalog.cs | `7144CB426C075687E05D0FF419469FFDB13F61D5C7FD2642C635FD54675F3915` |
| src/Monstertacle.Multisession.Common/Requests/RequestAdmission.cs | `6A50C0A1828EDB94CA13565E8B20E9006A004DFE1C77127C9B0A68C811D08A43` |
| src/Monstertacle.Multisession.Common/Scheduling/RequestScheduler.cs | `7FF2178AC5379B7713260475AF45EA90368F50A16985332BEF114B56912183E3` |
| tests/Monstertacle.Multisession.UnitTests/Fixtures/InMemorySessionStore.cs | `1795BAB0F96D78C3CEEAA23DDB15D372462938B9CB47B841D765273F8583B79B` |

### Befunde, Kontrollfluss und Verantwortlichkeiten

- SessionCatalog.CreateSessionAsync speichert lokal vor Open; BlockUncertainOpeningAsync
  erhält SessionId ohne Request. IRecovery/RecoveryDecision sind dagegen requestgebunden.
  Ein RequestSnapshot ohne Revision kann ExpectedRevision nicht liefern. Die Recovery-
  Dienste, Entscheidungen und Listen sind noch nicht produktiv implementiert.
- RequestAdmission.SubmitAsync übergibt SubmitRequest an CommitAcceptanceAsync; nur der
  Store vergibt aktuell RequestId. Nach unbekanntem Commit fehlt damit ein deterministischer
  Nachschlageschlüssel. PublicError beschreibt externe Gewissheit, nicht Annahmegewissheit.
- RequestScheduler.TryReserveAsync delegiert an TryReserveHeadAsync; im Fake werden
  Running/Reservierung gemeinsam gesetzt. Rückgabe enthält weder Input noch gebundenen
  Ausführungssnapshot. ISessionQueryStore bietet bisher nur öffentliche Statusprojektionen.
  Ein Dispatcher/produktiver Completionpfad oder SQLite-Store fehlt tatsächlich.
- Vorhandene Tests: SessionOpeningTests prüft ungewisse Eröffnung/Cancellation/Storefehler;
  RequestAdmissionTests prüft Commitbarriere und erhaltenen Receipt nach Caller-Cancel;
  RequestSchedulerTests prüft atomare Reservierung und Unknown-Belegung. Sie belegen
  keine Recoveryrevision, Dispatchnutzlast oder unbekannte Commitantwort.
- T015 vermischt lokale CI-Konfiguration mit externer CI-Abnahme, obwohl spätere
  Codearbeit im Phasengraph davon abhängt und kein Repository eingerichtet werden darf.
- Pagingoperationen sind Stubs; bisher keine verbindlichen Größen-/Cursorregeln.
  Mehrdeutige Baseline-/Status-/Rechercheformulierungen sind Dokumentationsbefunde.

## Architekturentscheidung

U1: Bestehende Reservierungsrückgabe erhalten; separater lesender GetReservedDispatchAsync
liefert einen in derselben Reservierungstransaktion eingefrorenen ReservedDispatch.
Keine nachträgliche Rekonstruktion aus aktuellen Hostobjekten oder öffentlichen Snapshots.
U2/U3: Ein RecoveryTarget unterscheidet SessionOpening und RequestExecution. Öffentliche
RecoveryCaseSnapshot enthält das Target und eine persistierte CAS-Revision. Keine Dummy-
Requests; Sitzung ohne sicher wiederaufnehmbares Binding bleibt blockiert oder wird nach
belegtem Ende geschlossen. Lesen/Inspect allein ändert weder Revision noch Reservierung.
U4: AdmissionStatus ist von ExecutionCertainty getrennt. Eine vor Commit erzeugte lokale
RequestId wird unverändert als RequestAcceptance an den Store gereicht und bei unbekanntem
Ausgang als Kandidatenkennung gemeldet; kein Idempotenz-/Deduplizierungsversprechen.
A2: Explizite Seitengröße 1–100, empfohlene Hostgröße 50, versionierter bereichsgebundener
Keyset-Cursor bis 2048 UTF-8-Bytes, InvalidInput statt stiller Korrektur. Livesichten, keine
behauptete Snapshotisolation über mehrere Seiten.
I1: Lokale Konfiguration bleibt T015; externe Läufe/Branchchecks werden T111 und blockieren
nur die Freigabe. Die übrigen Vertragslücken erhalten T112–T115 mit Red vor Green.
A1/I2/I3/D1: Begriffe und Status synchronisieren, dauerhafte Nachweise von temporären
Rechercheunterlagen abgrenzen, FR-024/025 nach Verantwortung/Inhaltsintegrität trennen.

Die Alternative bloßer Dispatcherzugriffe auf Fake-Dictionaries bricht die Storegrenze;
ein Dummy-Request für Open-Recovery erfindet einen Auftrag. Ein globaler vollständiger
Store-Snapshot oder generisches CRUD wäre unnötig. Neue Daten sind schmale fachliche
Projektionen der vorhandenen Anforderungen, keine neue Agent-Laufzeit.

Kompatibilität: Änderungen betreffen den noch nicht ausgelieferten Entwurfsvertrag v1.
Bestehende C#-Signaturen bleiben **in diesem Dokumentationsschritt** unverändert; die
Differenz ist ausdrücklich als offene TDD-Arbeit T112–T115 ausgewiesen. Kein bestehender
Store wird konvertiert, keine historischen Nachweise werden rückwirkend korrigiert.
Implementierungsrelevante Fragen für diesen Dokumentationsschnitt sind entschieden.

## Validierung und vorgesehene Tests

Dokumentation: vorhandene FR-/SC-IDs und Taskcheckboxen erhalten, neue Task-IDs eindeutig
ergänzen; alle Taskverweise und relativen Markdownlinks prüfen; widersprüchliche alte
Formulierungen im aktuellen Kern entfernen. Keine Tests für unveränderten Produktcode
als neu ausgeführt behaupten. Vor jeweiligem C#-Schnitt erneut aktuellen Deep-Dive prüfen.

- T112: fehlende/unveränderte Dispatchnutzlast, eingefrorene BindingRevision, fremde/
  freigegebene Reservierung, konkurrierender Completion-CAS vor Green.
- T113: Open-Fehler ohne Request auffindbar, Revision lesen, zwei Entscheidungen mit
  gleicher Revision genau einmal anwenden, Close ohne Endbeleg hält Fall offen.
- T114: bestätigte Nichtannahme/Commit/Unknown unterscheiden, Kandidatenkennung nach
  verlorenem Receipt auffinden, Caller-Cancel und verspäteter Commit ohne falsches Nein.
- T115: Seitenlimits, Cursorformat/-länge/-bereich, Keysetfortsetzung bei Einfügen/Löschen,
  Fremdinstanz und stabile Reihenfolge vor Green; produktive SQLitefälle in T063.

Diese Tests sind geplant, nicht in diesem Schritt ausgeführt oder als bestanden markiert.

## Durchgeführte Dokumentationsbereinigung und Selbstprüfung

| Befund | Auflösung im Zielentwurf | Noch offene Umsetzung |
| --- | --- | --- |
| U1 | orchestration-details.md §1: gespeicherter Dispatch-Snapshot, Leseschnittstelle, BindingRevision-/Completion-CAS | T112, T025, T051/T059/T060 |
| U2 | §2: SessionOpening-Target ohne Dummy-Request, konservativer Wiederanlauf und beleggebundene Schließung | T113, T037/T052/T062/T079 |
| U3 | §2: Get/List mit persistierter Revision, CAS und doppelte DecisionId ohne Teilwirkung | T113, T052/T062 |
| U4 | §3: AdmissionStatus, vorab erzeugte Kandidatenkennung und typisierter Caller-Cancel | T114, T058/T076 |
| I1 | T015 auf lokale Konfiguration begrenzt, externe CI-/Branchchecks in T111; Graph und Plan unterscheiden lokale Arbeit von Freigabe | T111/T104 bleiben NOT RUN |
| A1 | spec.md/plan.md/aktuelle Checkliste nennen nur Toolversion/Standardgrenzwerte ohne Debt-Baseline | Keine neue Tool-/Regel-/Baselineänderung |
| A2 | §4: konkrete Seiten-/Cursorgrenzen, Live-Keyset-Semantik und vollständige Sortierschlüssel | T115, Query-/SQLiteintegration |
| I2 | spec.md unterscheidet temporäre Rechercheablagen von dauerhaften bereinigten Pflichtnachweisen | Pflichtanalysen vor jeder neuen Erweiterung bleiben erforderlich |
| I3 | Status und Zielstruktur in Spec/Plan/Checkliste auf laufende Teilimplementierung abgestimmt | Weiterhin keine Story-/Produktfreigabe |
| D1 | FR-024 ist Ausführungsdelegation, FR-025 ist Kontext-/Inhaltsintegrität | Bestehende Abnahmefälle bleiben erforderlich |

Zusätzliche Konsistenzkorrekturen: vorhandene Contracts/ExecutionReservation und
Contracts/HistoryEntry statt zweiter gleichbedeutender DTOs in T020/T060; Get-null-
Semantik von NotFound bei verändernden Operationen unterschieden. Nach Produktrefactoring
Pack und Paketkonsum ausdrücklich erneut ausführen, nicht nur alte Pakete erneut testen.

Durchgeführte lesende PowerShellvalidierung, Exit 0:

- 115 eindeutige Task-IDs T001–T115 ohne Lücke, keine unbekannten Taskverweise;
- ursprüngliche 13 Erledigt-Markierungen unverändert, T111–T115 sämtlich offen;
- 27 FR- und 11 SC-Definitionen erhalten;
- relative Markdown-Dateilinks in neun betroffenen Dokumenten gültig;
- Sourcehash des Code-/Build-/Test-/Qualitätsumfangs weiterhin exakt
  `08afb7af04991cf78784e666f5d72be7a5349c0be8f483f8c9556692eef3e269`.

Die Prüfung las Taskdefinitionen/-referenzen per Regex, löste relative Markdownlinks
mit Test-Path auf und berechnete den SHA256 nach Get-SourceHash aus Invoke-Quality.ps1.
Kein Produkt-/Test-/Tool-/Workflowfile wurde geändert. Kein neuer Build, Testlauf oder
Codemetr-Lauf war Bestandteil dieser reinen Dokumentationsänderung; die bisherigen
Qualitätsresultate bleiben historische Ergebnisse für ihren identischen Codeumfang.

Selbstreview: Anforderungen nicht gestrichen, keine Constitution-/Scopeabschwächung,
keine Herstellerverzweigungen oder neuen Produktdienste. Die zehn dokumentierten
Analysebefunde sind im Entwurf adressiert; das ist ausdrücklich keine Behauptung,
dass die geplanten C#-Verträge oder deren Verhalten bereits implementiert/geprüft sind.
