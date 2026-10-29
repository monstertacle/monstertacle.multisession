# US1 — laufender Implementierungsnachweis

**Stand**: 2026-10-28. Keine abgeschlossene Story oder Produktfreigabe.
Tests nutzen kontrollierte eigene Adapter und einen partiellen In-Memory-Store,
keine native Plattformlease, kein SQLite und keine echte OpenCode-/Herstellerinstanz.
OpenCode-Referenz bleibt 1.19.0. SDK 10.0.403 ist ausdrücklich genehmigt.

## Vorab-Deep-Dive: Auswahlfehler und optionaler Workspace

Dieser Abschnitt wurde **vor der folgenden Produktcode-Erweiterung** geschrieben,
nach Eingang der verschärften AGENTS-/Constitution-Regel. Frühere Zyklen unten sind
als bereits ausgeführte Arbeit dokumentiert, nicht als rückdatierte Vorabanalyse.

### Quellstand und Belege

| Datei / Symbol | SHA256 des untersuchten Stands |
| --- | --- |
| `src/Monstertacle.Multisession.Common/Sessions/SessionCatalog.cs`, CreateSessionAsync/ValidateExecution | `7D28C0FEB00F5F183F46ABF76204473D943E46BBC5173E1A7EDD69C8FD98473E` |
| `src/Monstertacle.Multisession.Common/Contracts/LocalDirectoryPath.cs`, IsValid | `D4395D38805B3013B53981972E34B42B9693B27E26C94DC3F2D8FD6A8E579614` |
| `src/Monstertacle.Multisession.Common/Contracts/ProviderRegistration.cs` | `5AFB2B10F58F25FEA6B5AF5388D49BC36DE21791954B851E51E362BC1B64AED8` |
| `src/Monstertacle.Multisession.Common/Contracts/AgentProfile.cs` | `586F4926862E6114D281550E2FA25D5E79C51E7F10DA48A5188A2A34B51ABD5C` |
| `tests/Monstertacle.Multisession.UnitTests/SessionCapabilityTests.cs` | `47DC89C64DF3CE0883C4514AE3BB87070CC93CD74636AC908ADDE44775E99D14` |

### Architektur, Aufrufer und Kontrollfluss

- `SessionCatalog` implementiert `ISessions`; der bisherige `SessionRuntime` ist noch
  ein verhaltensloses Gerüst und kein fertiger Produktionsaufrufer dieses Katalogs.
  Tatsächliche bisherige Aufrufer: SessionRoutingTests, SessionOpeningTests und
  SessionCapabilityTests; Registrierungen werden von den Testhosts explizit übergeben.
- `CreateSessionAsync` löst zuerst Profil und Registrierung aus ordinalen FrozenDictionarys
  auf, prüft Fähigkeiten, erzeugt lokale UUID und eingefrorenen ProviderSessionContext,
  schreibt danach über ISessionStore und ruft erst dann den ausgewählten Adapter auf.
- Die direkten Indexer an Profil/Registrierung werfen derzeit rohe KeyNotFoundException.
  Das erfüllt nicht den vorhandenen öffentlichen Fehlervertrag
  `SessionOperationException.Failure`/`PublicError` (NotFound bzw. InvalidConfiguration).
  Es gibt keinen Fallback; diese Eigenschaft muss erhalten bleiben.
- ValidateExecution prüft einen Workspace bisher nur bei RequiresWorkspace=true.
  Ein ausdrücklich angegebener relativer oder Netzwerkpfad kann daher bei einem
  anderen Adapter ungeprüft in den Sitzungssnapshot gelangen. Das widerspricht dem
  optionalen, aber absoluten Workspacevertrag in provider-contract.md §8.
- LocalDirectoryPath.IsValid ist die bereits getestete gemeinsame syntaktische Regel
  für lokale absolute Pfade. Wiederverwenden statt eine zweite Pfadregel einzuführen.
  Reparse-/Symlink-/Mount-/Berechtigungsprüfungen bleiben native spätere Aufgaben.
- Store- und Adapterverträge müssen für diese Änderung nicht erweitert werden;
  fehlerhafte Auswahl muss vor UUID-/Store-/Open-Seiteneffekten enden. Keine Änderung
  von persistierten Formaten, Quellen des Zielhosts oder bestehenden nativen Sitzungen.

### Entscheidung und Testplan

Ordinale TryGetValue-Auflösung statt Indexer: unbekanntes Profil → NotFound,
nicht registrierte Profilverbindung → InvalidConfiguration; jeweils NotStarted,
ohne Ausweichadapter oder externe Öffnung. Workspace prüfen, wenn er gefordert
**oder ausdrücklich angegeben** ist. Kein Workspace bleibt für entsprechende
Adapter zulässig. Keine Hersteller-/Modellnamensverzweigung.

Zuerst SessionCapabilityTests.RejectsUnresolvedHostSelection (drei Fälle) und
RejectsMalformedOptionalWorkspace (zwei Fälle) tatsächlich Red ausführen. Danach
nur diese Regeln ergänzen, gesamte Unit-Regression und Codemetr/Format erneut prüfen.
Regressionsrisiken: versehentlich case-insensitive Auswahl, Ablehnung eines zulässig
fehlenden Workspace, vorzeitiger Store-/Provideraufruf, Verlust vorhandener Cancel- und
Unknown-Behandlung. Die vorhandenen Routing-/Opening-Tests decken diese bestehenden
Pfade mit ab. Keine offene Architekturfrage für diesen begrenzten Änderungsschnitt.
Vollständige Registrierungsvalidierung, Paging, Schließen/Löschen und echte Persistenz
bleiben separate offene Tasks, kein behaupteter Bestandteil dieser Erweiterung.

## Bereits tatsächlich ausgeführte Zyklen vom 2026-10-28

Einheitlicher Testbefehl: `dotnet test tests/Monstertacle.Multisession.UnitTests/Monstertacle.Multisession.UnitTests.csproj
-c Release --no-restore --filter FullyQualifiedName~<Testklasse> --logger trx
--results-directory artifacts/tdd/<Zyklus>-<red|green>`.
Jedes genannte Artefaktverzeichnis enthält TRX und `sources.json` mit vor dem Lauf
ermittelten SHA256 der beteiligten Produkt-/Testdateien. Nicht vorhandene Commit-IDs
wurden nicht erfunden. Lokale Artefakte sind ignoriert; relevante Ergebnisse stehen hier.

| Zyklus | Konkreter Red-Grund | Anschließendes Green / Artefaktpräfix |
| --- | --- | --- |
| Context-Snapshot; ProviderContextTests.FreezesSettingsAtSessionCreation | Erwartet original, tatsächlich changed nach Änderung des Hostdictionarys; 1 fehlgeschlagen / 1 bereits grün | 2/2; `2026-10-28-context` |
| Lokale Sitzung; SessionRoutingTests.PersistsIdentityBeforeOpeningSelectedAdapter | Assert.NotNull: Katalog lieferte null statt lokaler Sitzung | 1/1; `2026-10-28-routing` |
| Lookup/Isolation/Cancellation; SessionRoutingTests | Zwei eigene Snapshots nicht auffindbar, abgebrochener Read behauptete NotFound statt Cancellation; 3 fehlgeschlagen / 1 Regression grün | 4 Routingfälle und gesamte damalige Unit-Suite 82/82; `2026-10-28-lookup` |
| Fähigkeiten und Pflichtworkspace; SessionCapabilityTests | Nicht unterstützte ExecutionKinds und fünf ungültige Workspaces führten zu keiner erwarteten Abweisung; 8 fehlgeschlagen | 8 neue Fälle und gesamte damalige Suite 90/90; `2026-10-28-capabilities` |
| Öffnung/Binding; SessionOpeningTests | Lifecycle blieb Opening, falsche Bindingidentität/-version wurde akzeptiert, Providerexception blieb unbereinigt; 4 fehlgeschlagen | Ganze damalige Suite 94/94; `2026-10-28-opening` |
| Abbruch-/Storefehler; SessionOpeningTests | Cancel wurde als ProviderUnavailable gemeldet; Commit-/Blockfehler liefen roh durch; 3 fehlgeschlagen / 4 Regression grün | Ganze damalige Suite 97/97; `2026-10-28-opening-failures` |
| Auswahl / optionaler Workspace; SessionCapabilityTests | 3 rohe Auswahlfehler statt typisierter Abweisung, 2 optionale ungültige Pfade akzeptiert; 5 fehlgeschlagen / 8 Regression grün | Gesamte Unit-Suite 102/102; `2026-10-28-selection`; vorab analysiert wie oben dokumentiert |

## Request-Annahme und Lookup (2026-10-28)

Vorab-Deep-Dive und SharpClaw-Abgleich: `architecture/2026-10-28-sharpclaw-refresh-request-admission.md`.
Bezug: T016/T022, FR-005–FR-008, FR-016/FR-018/FR-027; keine vollständige Taskabnahme.
Die folgenden Artefaktpräfixe unter `artifacts/tdd/` enthalten getrennte Red-/Green-
TRX und `sources.json`. Befehlsform wie oben; Green jeweils ohne Klassenfilter als
gesamte damalige Unit-Regression. Red jeweils Exit 1, Green Exit 0.

| Zyklus / Reihenfolge | Tatsächlich beobachtetes Red | Green |
| --- | --- | --- |
| `2026-10-28-admission`; RequestAdmissionTests | 3 Fälle: null statt Receipt, Commitbarriere nicht betreten, nach bekanntem Commit kein Receipt | 105 Unitfälle bestanden |
| `2026-10-28-request-lookup`; RequestAdmissionTests | 2 neue Fehler: gespeicherter Request nicht auffindbar; Cancel als null statt OperationCanceledException | 107 Unitfälle bestanden |
| `2026-10-28-admission-validation`; RequestAdmissionValidationTests | 18 Fälle: Annahmeoperation trotz unbekannter/gesperrter/geschlossener Sitzung betreten; Input/Frist/Schreibschlüssel bzw. ungültige Options nicht abgewiesen | 125 Unitfälle bestanden |

Danach vier zusätzliche **bereits grüne Regressionen**, keine neuen Red-Zyklen:
inklusive UTF-8-Schreibgrenze, gleiche Texte mit getrennten Sessionsequenzen,
instanzweites Queuelimit und Cancellation an einer wartenden Commitbarriere.
Gesamtsuite 129 Unitfälle bestanden; `TestResults/admission-regression.trx` im Unitprojekt.
ExecutionDeadline extrahiert die gemeinsame vorhandene Fristdefinition für Options und
Submit; kein Timer-/Dispatchverhalten dadurch implementiert.

Vollständiger Windowslauf: 129 Unitfälle und 3 Infrastrukturtests bestanden,
11 Coverage-Gate-Tests bestanden, Build ohne Warnungen/Fehler, Format PASS.
Codemetr 0.1.0: 63 Dateien/Typen, 0 Verstöße/Parsefehler/übersprungene Typen, Exit 0.
Sourcehash `60e2231396269599394119dbfe3105328fc5f44f726a339d55e146a3abaed196`,
Artefakte `artifacts/quality/windows/ad21d6f9f7c346a3805a63ed2c26725e/`.
Coverage **181/220 = 82,2727 %**, daher Runner **Exit 1**, keine Freigabe.

Der kontrollierte Store unterstützt jetzt Annahme und Requestlookup zusätzlich zu
Sitzungsschritten, aber keine echte Haltbarkeit oder SQLite-Races. List/Cancel,
AdmissionTimeout-Timer, Scheduler, Dispatcher, Recovery und Runtimekomposition
bleiben unvollständig. Keine Sicherheits-, Haltbarkeits- oder Plattformabnahme.

## FIFO-Auswahl und Reservierungen (2026-10-28)

Vorab-Deep-Dive: `architecture/2026-10-28-fifo-reservations.md`.
Bezug FR-002/FR-008/FR-016; begrenzte Teile T017/T018/T019/T023/T024.
Kein produktiver Dispatcher/Send und kein abgeschlossener Storytask.

Testbefehl je Red:
`dotnet test tests/Monstertacle.Multisession.UnitTests/Monstertacle.Multisession.UnitTests.csproj -c Release --no-restore --filter FullyQualifiedName~<Klasse> --logger trx --results-directory artifacts/tdd/<Präfix>-red`.
Green jeweils gleiches Projekt/Release/no-restore/TRX, ohne Filter, `<Präfix>-green`.
Alle aufgeführten Red-/Green-Ordner enthalten vor dem Lauf erfasste `sources.json`
und die tatsächlich erzeugten TRX. Red Exit 1, Green Exit 0.

| Präfix / Klasse | Tatsächlich beobachtete fachliche Fehler vor Implementierung | Green / damalige gesamte Unit-Suite |
| --- | --- | --- |
| `2026-10-28-fifo-order` / SchedulingTests | 3 Fälle: null statt Sessionkopf; Sequence vor Zeit, ältester Kopf und ordinaler RequestId-Gleichstand fehlen | 132/132 bestanden |
| `2026-10-28-reservation-safety` / WorkspaceReservationTests | 13 Fälle: belegte Slots/Session/Workspace und unzulässige Sitzungen trotzdem ausgewählt; ungültige Limits nicht abgewiesen | 145/145 bestanden |
| `2026-10-28-scheduler-reservation` / RequestSchedulerTests | 7 Fälle: null statt gespeicherter Reservierung, Storebarriere nicht betreten/Token nicht weitergereicht, zwei Scheduler liefern 0 statt 2 Reservierungen, keine Optionsabweisung | 152/152 bestanden |

Der erste Scheduler-Versuch kompilierte wegen CA1822 am unbenutzten Stubzustand nicht;
das ist **kein Red**. Nach reinem Signatur-/Gerüstfix wurde der oben genannte tatsächliche
Lauf mit sieben fachlichen Fehlern beobachtet. Ebenso war xUnit2031 bei einer zusätzlichen
Regression nur ein Analyzerfehler; durch den vorgesehenen Assert.Single-Overload behoben,
keine Unterdrückung und kein behaupteter fachlicher Red-Zyklus.

Sechs zusätzliche bereits grüne Regressionen (`2026-10-28-scheduling-regression`):
ordinale Workspacekeys/nicht schreibender Auftrag (2), leere Queue (1), genau drei
Annahmen/fünf QueueFull bei acht gleichzeitigen Eingängen neben zwei Reservierungen,
genau ein gemeinsamer Writer bei acht konkurrierenden Reservierungen trotz vier Slots,
und zweite Sessionsequenz bleibt Queued während eine unabhängige Sitzung reserviert.
Gesamte Unit-Suite **158/158**. Synchronisation über TaskCompletionSource-Barrieren,
keine zufälligen Sleeps. Das sind Auswahl-/Fakezustandsbelege, keine ausgeführten Sends.

### Implementierung und Selbstprüfung

- QueuedRequest ist eine Projektion bereits angenommener/validierter Queued-Datensätze,
  kein Ersatz für RequestSnapshot oder vollständiges Persistenzmodell. Schreibschlüssel
  nur bei WriteIntent; die vorhandene Annahmevalidierung bleibt maßgeblich.
- ReservationPolicy bildet zuerst kleinste Sessionsequence, filtert danach Open/None,
  aktive Session und ordinal gleiche Schreibkeys; wählt AcceptedUtc/RequestId ordinal.
  Jede unfreigegebene Reservierung zählt, unabhängig von lokalem Ausgang/Gewissheit.
- RequestScheduler delegiert ausschließlich an TryReserveHeadAsync und hält keine
  eigenen Slots. Zwei Scheduler können deshalb keinen getrennten Kapazitätsvorrat bilden.
- Nur der kontrollierte Store erfasst hier AcceptedUtc, setzt atomar Running/InProgress
  und legt Reservierungen an. MarkUncertain konkretisiert allein dessen Testzustand
  mit Interrupted/Unknown und unveränderter Reservierung. Keine produktive Recovery.
- Regressionssuite vor Erweiterung grün; keine verhaltensändernde Umgehung bestehender
  Annahme-/Outputregeln, kein neuer Herstellerzweig, keine Policy im Common mit I/O.
  Tatsächliche SQLite-Atomizität, Release/Completion/Reopen/Dispatcher, Fristen und
  Runtimekomposition bleiben offen. Keine globale Starvationgarantie hinzuerfunden.

Vollständiger Windowslauf `pwsh -NoProfile -File scripts/Invoke-Quality.ps1 -Platform windows`:
**177 .NET-Tests** bestanden (158 Unit, 17 Contract, je 1 Integration/Windowsinfrastruktur),
keine übersprungenen Tests, 11 Coverage-Gate-Tests bestanden, Build 0 Warnungen/Fehler,
Locked-Restore/Format/Audit bestanden. Full-Codemetr 0.1.0: **73 Dateien/Typen**, 0 Verstöße,
Parsefehler oder übersprungene Typen, Exit 0; unveränderte Regeln/Pins, keine Baseline.
Sourcehash `5f6ef71246cb3758190f8cc6debdca98587fbfb3f8168e71cc46ed2e33ad7fc6`,
Artefakte `artifacts/quality/windows/53582390efb54d0e8c568b3d84615c9f/`.
Coverage **233/264 = 88,2576 %**, Runner **FAIL / Exit 1**. Keine Freigabe;
Linux-/CI-/Zielsystem-/Provider-/Paketnachweise und initiale Manifestlücke bleiben offen.

## T112 — reservierter Dispatch-Snapshot (2026-10-28)

Owner: aktuelle Implementierungssitzung; Arbeitsbranch `feature/reserved-dispatch-contract`,
Basiscommit `0cd4ac8`. Vorabanalyse: `architecture/2026-10-28-reserved-dispatch.md`.
Bezug FR-007/FR-018/FR-019 und Orchestrierungsvertrag §1. T112 ist lokal umgesetzt;
keine abgeschlossene US1, kein produktiver Store/Dispatcher und keine Produktfreigabe.

### Tatsächlich beobachtetes Red vor Green

Befehl für die ersten drei Läufe:
`dotnet test tests/Monstertacle.Multisession.ContractTests/Monstertacle.Multisession.ContractTests.csproj -c Release --no-restore --filter FullyQualifiedName~ReservedDispatchContractTests --logger trx --results-directory artifacts/tdd/<Verzeichnis>`.
Alle .NET-Läufe erfolgen über `bash /home/user1/dev/scripts/run-capped.sh --`.
TRX und vor jedem Lauf erfasste `sources.sha256` bleiben in der ignorierten lokalen Ablage.
Die Sourcehashes unten sind SHA256 der jeweiligen Manifestdatei, keine Commit-IDs.

| Reihenfolge / Verzeichnis | Beobachtetes Ergebnis | Manifest-SHA256 |
| --- | --- | --- |
| `reserved-dispatch-red-compiled` | Exit 1; sieben ausgeführte fachlich rote Fälle: fehlender Snapshot (5), fehlender Completionablauf und falscher Konfliktfehlertyp (2) | `6534fb769964f735292bd4cffa4468687b7bf0a8970d62063ad6905f2480fe78` |
| `reserved-dispatch-projection` | Exit 1; vollständige Submission und reine Reads grün (2); Cancel/nicht laufend noch lesbar (2), aktuelle Revision 0 statt 1 (1), Completion noch Stub (2) | `d2e9b344ec15ec82553dd9b20c4d8051155c06368a7104b1c1b4cf287df3baba` |
| `reserved-dispatch-green` | Exit 0; sieben Fälle grün einschließlich Revisionskonflikt ohne Result-/History-/Binding-/Releasewirkung, einmaligem Abschluss und nächster Sequenz/Revision | `7dcfd07351b68e1e59e603d5f3e09b38c63286740fb7cd348cee97d486035aba` |
| `reserved-dispatch-evidence-red` | Filter `FullyQualifiedName~RefusesUnmodelledCompletionWithoutRelease`; Exit 1; zwei Fälle: Unknown/InProgress-Completion wurde fälschlich ohne Exception akzeptiert | `4aece24c74e7bf96ac2ae8f0abf4a52c095f3692dc3041d91d0c8ae11711136d` |
| `reserved-dispatch-final` | Sourcefassung des anschließenden vollständigen nativen Qualitätslaufs; alle neun T112-Fälle grün innerhalb 26 Contractfällen | `c4e7274a77772a68d5aeffae0119ac5372c915c91fdd6f9e0dc82e2ebe7bad9e` |

Der erste Versuch unter `reserved-dispatch-red` scheiterte am Analyzer CA1859 einer
Testvariable; er ist ausdrücklich kein fachlicher Red-Nachweis. Nach Änderung zur
expliziten Schnittstellenkonvertierung folgte der tatsächlich ausgeführte Red-Lauf oben.
Vor Erweiterung und erneut vor Refactoring bestanden jeweils 158 Unitfälle.

### Umsetzung, Refactoring und Grenzen

- Common ergänzt ReservedDispatch, die explizite ISessionQueryStore-Leseoperation und
  SessionSnapshot.BindingRevision; vorhandene Reservierungsrückgabe bleibt unverändert.
- Das bestehende Double erfasst Submission/Session/Sequence/Revision unter demselben
  Gate wie Running/Reservierung. Settings sind bereits durch ProviderSessionContext
  eingefroren. Reads verändern weder Zustand noch Revision und prüfen aktive
  Reservierung, Running und fehlende Abbruchmarkierung zum Lesezeitpunkt.
- Kontrollierte bestätigte Completion prüft offene Reservierung und aktuelle Revision
  vor jeder Teilwirkung; Ergebnis, History, Binding und Freigabe folgen im selben Lock.
  Erstes Opening-Binding hat Revision 0, bestätigte Folgepublikation erhöht sie.
  Mehrmaliges Lesen ist keine Send-/Retryberechtigung. Ein doppelter Abschluss scheitert.
- Das Double modelliert hier ausschließlich bestätigte Completion. Unknown/InProgress
  werden ausdrücklich als noch nicht implementiert abgewiesen und behalten Reservierung
  und unveränderten Request. Produktionsverhalten dazu bleibt T051/T060/T070/T075/T080;
  bereits vorhandenes MarkUncertain wird nicht durch eine unsichere Freigabe ersetzt.
- Nach grünem Stand meldete Codemetr zunächst Storekopplung 41 bei Grenze 40.
  Abbruchcollection und Historyaufnahme wurden in ControlledDispatchState gekapselt.
  Danach voller Guard und Regression grün; keine Schwellen-/Pin-/Scopeänderung.
- ContractTests kompilieren die drei bestehenden kontrollierten Fixturequellen per Link;
  keine Produktabhängigkeit auf Testassemblies und keine zweite Storeimplementierung.

### Native Gesamtprüfung des geänderten Quellstands

`pwsh -NoProfile -File scripts/Invoke-Quality.ps1 -Platform linux` über run-capped:
**Exit 0**. Artefakte: `artifacts/quality/linux/b83d5853697d45c5aa01d3a26f4d8883/`.
Qualitätsscope-Sourcehash aus run.json:
`617d3f67adf713058c7a60950846899f93893a58ddc4fd23ac3af55e2db4417f`.

- Locked-Restore, Release-Build, Format-/Analyzerprüfung und transitiver NuGet-Audit bestanden;
  Build 0 Warnungen/Fehler. Ein erster Formatlauf fand zwei Zeilenumbrüche im neuen
  Initializer; korrigiert, der vollständige finale Lauf bestand.
- TRX maschinell gezählt: **186 Tests bestanden**, 158 Unit, 26 Contract, je 1 Integration-
  und Linux-Infrastrukturtest; 0 fehlgeschlagen/nicht ausgeführt. Zusätzlich 13 bestandene
  Coverage-Gateverhaltensfälle (aus Log gezählt).
- Gesamter nativer Produktumfang: **241/271 = 88,9299 %**, Mindestwert 87 % erfüllt;
  8 Reports, gemeinsame vier Bibliotheken plus Linux. Windows ist der fremden nativen
  Plattform zugeordnet, nicht still aus beiden Läufen entfernt.
- Full-Guard `dotnet tool run codemetr-csharp -- check --format json`, CLI 0.1.0:
  **76 Dateien/Typen**, 0 Verstöße/Parsefehler/übersprungene Typen, Exit 0;
  `artifacts/tdd/reserved-dispatch-codemetr-final.json`.
- Lokaler Host: Debian GNU/Linux 13, PowerShell 7.6.9, SDK 10.0.403. Dieser native
  Linuxlauf ist kein Ubuntu-24.04-, Windows-, GitHub-CI-, Rechte-/Crash-/Liveprovider-
  oder Paketabnahmenachweis. Finale Q-Gates bleiben offen.

Der Linuxrunner scheiterte zunächst vor der Prüfung an der vorhandenen versteckten
.editorconfig. Mechanische Metadatenkorrektur `Get-Item -Force` in der festen Rootliste;
Vorabanalyse/Reproduktion: `architecture/2026-10-28-linux-quality-file-discovery.md`.
Der vollständige Lauf oben validiert die korrigierte Erfassung ohne Änderung der Gates.

### Abgleich der Befunde und Folgearbeit

| Gegenstand | Disposition / zuständige Folgeaufgabe |
| --- | --- |
| Fehlende Dispatchprojektion und Completion-Revisionsprüfung | T112 umgesetzt und durch die neun neuen Fälle geprüft |
| Zu hohe Kopplung im erweiterten Double | Gekapselt; vollständiger Codemetr-Guard bestanden |
| Unmodellierte Completion konnte im neuen Double freigeben | Zwei beobachtete Red-Fälle, ausdrücklicher Stubguard, finale Regression grün |
| Linuxrunner findet versteckte Rootdatei nicht | `-Force` ergänzt; originaler vollständiger Linuxrunner bestanden |
| Echte SQLite-Atomizität / Crash / Operations-ID-Sicherung | Bestehende T051/T059/T060, Owner: ausführende US5-Implementierungssitzung; atomare Reservierung/Completion und Crashfälle sind deren Abnahme |
| Dispatcher und tatsächliche Sends | Nächster begrenzter Auftrag T025 mit passenden Red-Fällen T016–T019, Owner: nächste US1-Implementierungssitzung; keine Wiederholung alter DispatchId, nur neue Eingabe, einmaliger bestätigter Abschluss |
| Paging / native Recovery-/Lifecyclezustände | Bestehende T113/T115/T062/T070/T075/T077/T080; jeweilige US5-/US3-Sitzung, Abnahme gemäß dortigen konkreten Fällen; kein stiller Zusatzumfang zu T112 |
| Externe native CI / Produktfreigabe | Bestehende T111/T104 und Q-01–Q-06, Owner: freigebende Projektverantwortliche und jeweilige ausführende Sitzung |

T112 verändert weder historische Nachweise noch die dokumentierte anfängliche Manifestlücke.
Keine neuen offenen T112-Abnahmeblocker. Andere Storytasks bleiben unverändert offen.

## T025 — Dispatcher (2026-10-29)

Owner: aktuelle Implementierungssitzung, Branch `feature/reserved-dispatch-contract`.
Basiscommit `4f01cb1`; Vorabanalyse `architecture/2026-10-29-dispatcher.md` wurde vor
Entwurf/Implementierung geschrieben. T025 lokal abgeschlossen, keine vollständige US1-
oder Produktabnahme. Die Runtimekomposition T026 und echte Persistenz bleiben offen.

### Beobachtete Red–Green-Zyklen

Befehlsform: `dotnet test tests/Monstertacle.Multisession.UnitTests/Monstertacle.Multisession.UnitTests.csproj -c Release --no-restore --filter <Filter> --logger trx --results-directory artifacts/tdd/<Ordner>`.
Jeder .NET-Lauf über `bash /home/user1/dev/scripts/run-capped.sh --`.
Vor Laufstart erfasste `sources.sha256` und originale TRX bleiben in der ignorierten
lokalen Ablage. Manifest-SHA256 bezeichnet die Manifestdatei, keine Commit-ID.

| Zyklus / Filter | Tatsächliches Red vor Umsetzung | Green / Manifest-SHA256 Red → Green |
| --- | --- | --- |
| Core, `FullyQualifiedName~RequestDispatcherTests` | `dispatcher-core-red-valid`: sechs fehlgeschlagene Assertions (Send-/Completionbarriere nicht betreten, keine Reservierung, keine Fortsetzung, Queryfehler verschluckt), leere Queue bereits grün | `dispatcher-core-green`: 7/7, Exit 0; `273e375be9f0b8378ddfeb2e58e8dfa84a763cfbec323dd1ab795bb579856808` → `ba902207b75cdbcd9d688cbcaccad2475d8e462891b2c379b2b789f5a1461e3d` |
| Callback/Antwort, `FullyQualifiedName~DispatchSafetyTests\|FullyQualifiedName~DispatchObservationTests` | `dispatcher-safety-red`: 14 Fehler; Callbackcommit fehlt/Fehler als Erfolg behandelt, Zwischenstatus/Unknown/Identitätswechsel freigegeben, Ausgabegrenze nicht integriert, rohe Provider-/Registrierungsexceptions | `dispatcher-safety-green`: 21/21 einschließlich 7 Core-Regressionsfällen; `74189761a20b09e879ff8625157a8625cafeeea39023f2debb08977c96e05fa3` → `744b387b5d5e7ac2b997303e36596f4f5160bab124399dd607a2247e0a15501c` |
| Deadline, `FullyQualifiedName~DispatchDeadlineTests` | `dispatcher-deadline-red-compiled`: 4 Fehler/2 Regressionen grün; kein Timer, Cancel als ExecutionFailed, nicht kooperativer Send kehrt auch nach Caller-Cancel nicht zurück (5-s-Testwächter) | `dispatcher-deadline-green`: 6/6, Exit 0; `26c8438b08181f6629e71026e60b82b8b9f019c6d7d77b8a6d788a73d72d67bd` → `aa7cdeb4f6489f8c14f536ea802a53268c6eac3ef0a480a8d7f904e79513a2ac` |
| Storegrenzen, `FullyQualifiedName~DispatchStoreFailureTests` | `dispatcher-store-red`: 2 Fehler/3 bereits grüne Regressionen; roher Reservierungsfehler, Cancel im Snapshotread als StorageUnavailable statt Cancelled/NotStarted | `dispatcher-store-green`: 5/5, Exit 0; `72ce404b886e7ef596c8536002d12ac52f05ad42489e23d1be6924900e8aaa77` → `55bd3649c69fba3dc6bc8c741c43a27ae1d760bfa219cb81b1bbbcd081d40811` |

Kein fachliches Red waren der erste Core-Compileversuch (IDE0005), ein zunächst falsch
gebundener Workspace im Test und der erste Deadline-Compileversuch (CA1001 am eigenen
Timerfixture). Diese wurden vor den gültigen obigen Läufen korrigiert. Keine Änderung an
fachlichen Validierungsregeln oder Unterdrückung von Analyzern, um Tests ausführbar zu machen.

Vor Erweiterung 158 Unitfälle grün. Nach ersten drei Zyklen plus Lastregression 187 Unitfälle
grün. Folgende zusätzliche Fälle waren **bereits grüne Regression**, kein erfundener Red:
100 eindeutig markierte Requests/Ergebnisse über zehn Sitzungen und zwei überlappende
Provider, SC-005-Ausgangslage mit zwei laufenden Sends plus acht Eingängen und Queue-Limit
3 (genau 3 Annahmen/5 QueueFull), verlorenes Acknowledgement nach erfolgreichem Commit,
RevisionConflict, Ausfall von MarkUncertain sowie unawaitete/späte Callbacks und lazy
Native-ID ohne bestätigte Eröffnungsbeobachtung. Finale Unit-Suite: 195/195.

### Implementierung und Selbstprüfung

- RequestDispatcher erwirbt frische Reservierungen selbst und liest nur ReservedDispatch;
  keine öffentliche Methode zum Resenden alter IDs und kein unbegrenztes Wiederholungsregister.
  False bedeutet kein zulässiger Dispatch; true bestätigt ein gespeichertes lokales
  Endergebnis, auch ein bestätigtes Fehlerergebnis. Storefehler sind Exceptions, keine Queueleere.
- Providerwahl ist ordinal und am Konstruktor fixiert. ProviderCall enthält ausschließlich
  neue Eingabe sowie den eingefrorenen Modell-/Workspace-/Settingskontext und das festgelegte
  Outputlimit. Fehlende/inkompatible Registrierung ergibt Failed/NotStarted ohne Ersatzadapter.
- DispatchObservationSink bestätigt Beobachtungen erst nach dispatchgebundenem Storecommit,
  hält Fehlversuche/pending Callbacks fest und sperrt spätere Callbackwrites. Ein Adapter,
  der Callbackfehler ignoriert, kann damit keinen Erfolg erzwingen. Native SessionEstablished-
  Identität darf eine bestehende Identität nicht austauschen. Beobachtungen veröffentlichen
  im Double keinen neuen bestätigten Fortsetzungskopf.
- DispatchReplyPolicy prüft terminales Ergebnis und kompatibles Binding und verwendet
  ProviderReplyPolicy für die schon vorhandene UTF-8-Ausgabegrenze. Kein eigener Kontext,
  keine Tools oder Historienwiederholung. Ausgabeüberlauf wird leerer Fehler, kein gekürzter Erfolg.
- Externe Wartezeit verwendet Submission.Timeout, TimeProvider-Timer und verknüpften
  Caller-/Deadline-Token. Ein nicht kooperativer asynchroner Adapter wird lokal nicht
  unbegrenzt abgewartet. Exception, Abbruch und unbestätigte Antwort sichern konservativ
  Unknown und behalten Sitzung/Workspace/Slot; ein spätes Ergebnis wird nicht committed.
- Ein nach dem Send bekanntes finales Ergebnis verwendet für den Abschluss die endliche
  Storefrist statt des Caller-Tokens. Bei verlorenem Commit-Acknowledgement kein zweiter
  Send und kein blinder MarkUncertain-Overwrite. RevisionConflict bleibt ohne Teilcommit.
  Cancel im Snapshotread liegt nach Reservierung, aber vor jedem Send und wird daher als
  Cancelled/NotStarted abgeschlossen. Reservierungsfehler ohne sichere lokale Kennung
  meldet StorageUnavailable/Unknown, keine erfundene Nichtannahme und keinen Retry.
- Der kontrollierte Store wurde nur um benötigte Observation-/NotStarted-Pfade und Hooks
  erweitert. Callback-/Commit-Gates sind Testbarrieren, keine SQLite-Haltbarkeit. Bestehende
  T112-Vertragsregression bleibt grün. Timerfixture erzeugt keinerlei native Wartedauer.
- SOLID/KISS: Reservierung bleibt beim Scheduler/Store, Ausführung beim Dispatcher,
  Callbackbestätigung und Replyregeln besitzen eigene kleine Verantwortlichkeiten.
  DRY: vorhandene Ausgabe-/Options-/Reservierungsregeln werden wiederverwendet. Kein
  Herstellerzweig, keine neue Produktabhängigkeit, kein SDK-/Toolpin- oder Scopewechsel.
- Nach 187 grünen Unitfällen meldete Codemetr Testkopplung 41 bei Grenze 40. Hostsetup
  unverändert in DispatchLoadHost gekapselt; Regression und voller Guard danach grün.

### Finale lokale Prüfungen

`pwsh -NoProfile -File scripts/Invoke-Quality.ps1 -Platform linux` über run-capped:
**Exit 0**. Originalberichte `artifacts/quality/linux/00ee4f74a76e4f64be51e24f8308be76/`.
Sourcehash aus run.json: `790529a286d607b236aa25aabba02462a5f17a4ed289d3ca61143404349f8916`.
Manifest der abschließenden TDD-Fassung: `artifacts/tdd/dispatcher-final/sources.sha256`,
SHA256 `67284079818b2686f64170bb6b0abe473cf31b47e75c51eee35e4bb5ba62b439`.

- Locked-Restore, Release-Build, Format/Analyzer und direkter/transitiver NuGet-Audit
  bestanden; Build 0 Warnungen/Fehler. SDK/Toolpins und Gates unverändert.
- Maschinell aus den vier TRX gezählt: **223 Tests bestanden**, davon 195 Unit, 26 Contract,
  je 1 Integration-/Linux-Infrastrukturtest; 0 fehlgeschlagen/nicht ausgeführt.
  Zusätzlich 13 Coverage-Gateverhaltensfälle bestanden (Logzählung).
- Gesamter nativer Produktumfang: **376/400 = 94,0 %** Zeilenabdeckung, Mindestwert 87 %
  erfüllt. 8 Reports; vier gemeinsame Produktbibliotheken plus Linuxbibliothek.
- `dotnet tool run codemetr-csharp -- check --format json`, CLI 0.1.0: **88 Dateien/89 Typen**,
  0 Verstöße/Parsefehler/übersprungene Typen, Exit 0; `artifacts/tdd/dispatcher-codemetr-final.json`.
- Debian GNU/Linux 13/PowerShell 7.6.9/SDK 10.0.403. Kein Ersatz für Windows-/Ubuntu-
  Zielsystem-/native Rechte-/Crash-/Liveprovider-/Paketabnahmen oder finale Q-Gates.

### Befundabgleich und nächste Arbeit

| Gegenstand | Disposition / Owner und Abnahme der Folgearbeit |
| --- | --- |
| Fehlender Dispatcher, Callbackbestätigung, Ergebnisintegration | T025 lokal umgesetzt, obige Red–Green-Zyklen und finale Regression |
| Timeout/Cancel konnte Sendwartezeit offen lassen | Beobachtetes Red, begrenzte WaitAsync-/Tokenbehandlung, vier grüne kooperative/nicht kooperative Fälle |
| Roher Reservierungsfehler und falsch klassifizierter Read-Cancel | Zwei Red-Fälle, bereinigter Fehler bzw. Cancelled/NotStarted, fünf Storegrenzenfälle grün |
| Zu hohe Kopplung der Lasttests | Hostfixture gekapselt, voller Guard bestanden |
| Runtimekomposition, öffentliche Queries/Paging und automatischer Worker | Nächste US1-Sitzung, T026/T027; aktueller Deep-Dive, fehlende Red-Fälle und öffentliche Runtimeabnahme; Paging erst nach T113/T115 |
| Persistente Callback-/Completion-/Reservierungsregeln und Crash | US5-Sitzung, bestehende T051/T059/T060; echte SQLite-Transaktionen, Prozess-Kill und Reopen-Abnahme |
| Vollständige Cancel-/Recovery-/Stop-/Diagnoseautomaten | US3-/US5-Sitzung, T062/T070–T085; deren Zustands-/Fristen-/Stop-/Datenschutzabnahme, nicht durch diesen lokalen Dispatchzyklus erledigt |
| Native CI / Freigabe und reale Provider-/Paketabnahme | Projektverantwortliche und Freigabesitzung, T104–T111/Q-01–Q-06; erforderliche externe Nachweise separat |

Keine offenen T025-Abnahmeblocker. Andere Storytasks werden durch erfolgreiche lokale
Regressions-/Lastfälle nicht pauschal geschlossen. Keine Änderung historischer Belege.

## T026/T027: öffentliche Runtimekomposition

Updated: 2026-10-29T07:41:18Z
Vorabanalyse: architecture/2026-10-29-runtime-composition.md, Basis 86ea819.
SpecKit-Prerequisites erneut erfolgreich; Checklisten unverändert vollständig geprüft.

Alle folgenden dotnet-Aufrufe liefen über run-capped, Release/no-restore.
Zuerst ParallelSessionsTests: zwei ausführbare fachliche Fehler (Runtimefacetten/Start
noch Stubs), danach 2/2 grün. Keine Compiler-/Analyzerfehler als Red gewertet.
Manifest-SHA256 Red `1bf7be4432194f346869359ab7f942df5873ea89aa9fbbbb6267c791acb1b0a0`,
Green `33ef04b083fd1a6c1f0be57aa4b2f9c3581b76b57e9c1a3ebd5e483ca645d95f`.
Befehl: `dotnet test tests/Monstertacle.Multisession.ContractTests/Monstertacle.Multisession.ContractTests.csproj -c Release --no-restore --filter FullyQualifiedName~ParallelSessionsTests`.
Zehn Sitzungen, je zwei geordnete Ergebnisse, zwei herstellerübergreifend überlappende
Sends unter Limit zwei; lokale Identitäten, Modelle, Inhalte und History isoliert.

RuntimeStartupTests: vier fehlende Verhaltenspfade rot, ein bereits grüner Queued-Fall
nur Regression. Fehlende Opening-/Running-Recovery, Storebesitz und Annahmestopp danach
mit öffentlicher Komposition 7/7 grün. Manifest Red
`e8da62c3ee4c4a1a6bc53fc3191f4d52f82dba31650a30b0f74c22bfb6b457d9`, Green
`5ac650017772db76d9a1d0040b565eaabc38fd311148789fb826a506f754ab1b`.
Zusätzlicher Hintergrundfehlerfall: Assertion erwartete Unknown, bekam NotStarted.
Nach bereinigter konservativer Fehlermappingkorrektur gesamte Vertragssuite 52/52 grün.
Manifest Red `4f0863b850b2b69f2f676f229d49ae78519df83b4743c2d01f97ee7f99ef1bce`,
Regression `a7f09156a7194119d1a628d0a7b89acfb1b3cd687c154dc80768bf1ee23f5119`.
Die Ownership-/Admissionstop-/Workerfehlerfälle liegen nach grünem Refactor in
RuntimeOwnershipTests; gemeinsame Konfiguration im RuntimeTestComposition-Fixture.

SessionRuntime komponiert kleine Facetten/Lifecycle und begrenzte Worker. Store bleibt
alleinige Reservierungsautorität; Wakeup plus endlicher Poll verhindert Signallücken.
IStoreBootstrap/Leasestatus sind explizite Host-/Storegrenzen. Storestart im Double
blockiert alte Running/Opening konservativ, ohne Send/Open/Reopen. Native Persistenz
und Rechte folgen T055–T066/T087–T093. Fehler der Hintergrundarbeit sind über Completion
beobachtbar; Resources werden nach Workerende freigegeben. Keine globale Registry.

Finaler lokaler Qualitätslauf Exit 0:
`artifacts/quality/linux/001dbaee9abd4abe8c56095e8deed37f/`, Sourcehash
`c182c6e2a5813ff671b4e34a546042bc607a5f7a3e3781d2e6ca703c70fef627`.
Maschinell aus vier TRX: 279/279 Tests, 225 Unit/52 Contract/je 1 Infrastrukturtest;
keine Fehler/Skips. 13 Coveragegatefälle, Locked-Restore, Build, Analyzer/Format und
transitiver Audit grün. 674/706 Produktzeilen = 95,4674 %, Mindestwert 87 %.
Voller Codemetr-Guard Exit 0, 129 Dateien/131 Typen, keine Verstöße/Parsefehler/Skips;
Originalbericht artifacts/tdd/runtime-codemetr-final.json. SDK/Toolpins/Scope unverändert.
Debian 13 ist kein Nachweis aller Zielsysteme. T114-CI auf Windows/Ubuntu erfolgreich
(quality 40834562210, codemetr 40834562137); neuer Runtime-CI-Nachweis folgt nach Push.

T026-Komposition und T027-Ablauf umgesetzt. Vollständiger Stop/Cancel/Close und Diagnose
bleiben T070–T085 mit Owner ausführende Gesamtimplementierungssitzung: zuerst deren
Red-Fälle, danach dauerhafte Queuefinalisierung/native Endbestätigung, begrenzte Stopfrist,
Regression an SQLite. Idle-Stop ist keine vollständige Stopabnahme. T021–T024 und die
zugehörigen Tests werden im US1-Gesamtabgleich einzeln gegen ihre Abnahme geprüft;
keine pauschale Storyfreigabe. Nächste konkrete Arbeit: T030/T036 Konfiguration und
US2-Adapter, danach SQLite, Lifecycle und Plattform/Konsum gemäß implementation-progress.md.
