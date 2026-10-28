# T112: reservierter Dispatch-Snapshot

Created: 2026-10-28T22:55:09Z
Owner: aktuelle Implementierungssitzung; Branch `feature/reserved-dispatch-contract`.
Basis: Commit `0cd4ac8`, FR-007/FR-018/FR-019, Orchestrierungsvertrag §1.
Dieser Nachweis entsteht vor Implementierung. Kein SQLite-/Provider-/Crashnachweis.

## Aktueller Code und Kontrollfluss

- `RequestAdmission.SubmitAsync` validiert und übergibt den unveränderlichen SubmitRequest
  an ISessionStore. Nur der kontrollierte InMemorySessionStore speichert ihn derzeit.
- `RequestScheduler.TryReserveAsync` delegiert an TryReserveHeadAsync. Der kontrollierte
  Store wählt unter `_admissionGate` mittels ReservationPolicy den zulässigen FIFO-Kopf,
  speichert ExecutionReservation und setzt RequestSnapshot auf Running/InProgress.
  RequestSchedulerTests prüfen Kapazität, Barrieren, Cancellation und Unknown-Sperren.
- ISessionQueryStore liest bisher nur öffentliche DTOs, keine vollständige Submission
  samt reserviertem Kontext. Ein Dispatcher existiert noch nicht. Die SQLite-Bibliothek
  enthält keine Storeimplementierung. CommitCompletionAsync ist im Double ein Stub.
- SessionCatalog persistiert lokale Identität vor Open und danach Binding. SessionSnapshot,
  SubmitRequest, ProviderBinding und SessionExecutionContext sind immutable Records;
  ProviderSessionContext friert Settings bereits per FrozenDictionary ein.
  Daher genügt das Speichern dieser Snapshots bei Reservierung; kein Replay oder Cloneframework.
- Einziger ISessionStore-Implementierer ist InMemorySessionStore. Aufrufer sind SessionCatalog,
  RequestAdmission, RequestScheduler und deren Unit-Tests. Common bleibt infrastrukturfrei.

## Untersuchte Sourcehashes (SHA256)

| Datei unter Common, sofern nicht anders angegeben | Hash |
| --- | --- |
| Contracts/ISessionQueryStore.cs | 468fdf83d8695a00f56f88d400403874d4e97dd4366f4c362ab01ff56bb23437 |
| Contracts/ISessionStore.cs | 68c0497d26ea52ef921ef5bd6248aaf43353eca207e946503777eab11fb78e3e |
| Contracts/SessionSnapshot.cs | 4dbb3ff3698f3d3a334552b3e8f85f1e8a3e81e7aacb832b768eed2ebf139800 |
| Contracts/SubmitRequest.cs | 176bfeb12d76b32253efb76ded5eab88ac059c64043e3dcb0956812858035faa |
| Contracts/ProviderSessionContext.cs | ca2862deacc7c312a5bc5a8741d007047ecf295fbcdd327d4dd760ec36bcd64c |
| Scheduling/RequestScheduler.cs | 7ff2178ac5379b7713260475af45ea90368f50a16985332bef114b56912183e3 |
| Requests/RequestAdmission.cs | 6a50c0a1828edb94ca13565e8b20e9006a004dfe1c77127c9b0a68c811d08a43 |
| Sessions/SessionCatalog.cs | 7144cb426c075687e05d0ff419469ffdb13f61d5c7fd2642c635fd54675f3915 |
| UnitTests/Fixtures/InMemorySessionStore.cs | 1795bab0f96d78c3ceeaa23ddb15d372462938b9cb47b841d765273f8583b79b |

## Entscheidung, Erweiterungspunkt und Testplan

ReservedDispatch als immutable DTO und explizite Query ergänzen; TryReserveHeadAsync behält
seine Rückgabe. SessionSnapshot erhält die geplante BindingRevision (initial 0). Der
kontrollierte Store speichert Snapshot und Revision gemeinsam mit Reservierung/Running;
spätere bestätigte Bindings erhöhen die Revision. Erstes Opening-Binding bleibt Revision 0.
Eine kleine kontrollierte Dispatchzustandsklasse bündelt Snapshot, Abbruchmarkierung und
History statt zusätzliche unabhängige Dictionaries im bereits umfangreichen Store zu häufen.
Beide Testsuiten kompilieren dieselben Fixturequelldateien über Links; keine Referenz von
Produktcode auf Tests oder von ContractTests auf die UnitTests-Assembly.

Zuerst verhaltenslose Signaturen/Query-/Completionstubs, dann fachliches Red in
ReservedDispatchContractTests. Fälle: vollständige unveränderte Submission einschließlich
UTF-8/Whitespace/Timeout/Workspace, eingefrorene Zuordnung und Settings trotz Hoständerung,
Fortsetzungsrevision 0 und folgende Revisionen, unbekannte/freigegebene Kennung ohne
Sendberechtigung, cancelmarkierte/nicht laufende Anfrage ohne Projektion, mehrmaliger
Read ohne Mutation, Cancellation, Konflikt ohne Result-/History-/Binding-/Releasewirkung,
einmalige erfolgreiche Completion. Completion im Double modelliert nur bestätigte finale
Ergebnisse; andere Produktionszustände bleiben spätere Tasks.

Kein Provider-I/O. Alle kontrollierten Dispatch-/Bindingoperationen verwenden dasselbe Gate.
Risiken: veränderliche Hostdaten, Lost Updates, Freigabe vor Revisionsprüfung, Doppelabschluss,
Versehensänderung des Schedulers. Bestehende Unit-Regression bleibt Pflicht. Vor Green keine
implementierungsrelevante offene Architekturfrage. Echte atomare SQLite-Reservierung und
Completion folgen T051/T059/T060, Dispatcher T025, Lifecycle/Cancel T070/T077, Paging T115/T063.

## Refactorentscheidung nach erstem Green

7 neue Vertragsfälle und 158 Unitfälle sind grün. Der volle Codemetr-Check meldet
für InMemorySessionStore Kopplung 41 bei Grenze 40. Die neue Dispatchzustandsklasse
exponiert derzeit HashSet und List direkt. Die Abbruchabfrage/-markierung und Aufnahme
der History werden dort gekapselt; der Store behält den gemeinsamen Transaktionslock.
Damit bleiben Zustandsverantwortung und konkrete Collectiontypen beim Dispatchzustand.
Keine Regel-/Scope-/Pinänderung. Anschließend dieselben Tests und voller Guard erneut.
