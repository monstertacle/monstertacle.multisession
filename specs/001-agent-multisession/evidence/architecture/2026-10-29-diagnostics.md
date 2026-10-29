# Datensparsame Diagnosen: erster Schnitt T074/T081

Ausgang9a3775c, Quellenmessung2026-10-29T14:48:16Z. SessionRuntime SHA256
ec066b447641b78c76cde1abf2df6a53227e5062c3e72ea9823bd9a91092ec7f,
RuntimeRequests 99669e084ca255966cb143e04f1319681a50837abab435162d627da5078b8b96,
RuntimeLifecycle 92e9b41b3ee86732df32b9e29097ee328b9864da7c3de47f474b27e6d1cf32f5,
RequestDispatcher a489606eec82d8060d4a2a26916763e91e917be444a1f89100b9da72930d7763.

## Bestand und Verantwortlichkeiten

Common referenziert bereits Logging.Abstractions; bislang weder Diagnoseklasse noch Hooks.
RuntimeRequests hat den eindeutig bestätigten Annahmezeitpunkt nach RequestAdmission;
RequestDispatcher.CommitAsync bestätigt erst nach Store.CommitCompletionAsync. PreserveAsync
kann MarkUncertain verlieren: ein bloßer lokaler Fehler ist kein bestätigter Endzustand.
ProviderReply enthält Output/Binding, ExecutionReservation einen WorkspaceKey: keines davon
gehört in Logs. Öffentlicher PublicError hat nur endliche Codes und lokale IDs.
RuntimeLifecycle besitzt bereits8Felder; neue Loggerzustände dort vermeiden. Dispatcher
liegt nahe Kopplungsgrenze40. Konstruktoren bleiben höchstens6Parameter.

## Entscheidung und Testplan vor Erweiterung

SessionDiagnostics als explizit hosteigenes IDisposable mit optionalem ILogger, eigener
Instanzkennung und eigener ActivitySource. Runtime/Dispatcher dürfen es optional benutzen;
Host entsorgt erst nach Runtimeende. Keine Logger-/Listenerexception überschreitet die
Diagnosegrenze; getrennte Senken dürfen trotz jeweils anderer fehlerhafter Senke arbeiten.
Datenpositivliste: Instanz/Sitzung/Request/Dispatch, Übergang, Gewissheit, Sekunden, öffentliche
Fehlerkategorie. Weder beliebige Fehlertexte noch Exceptionobjekte/Providerdaten übergeben.
Nur Enum.IsDefined-Werte zu endlichen Kategorien normalisieren. Keine Inhalte in Namen,
Tags, Formatstrings oder Exceptions. Ereignisactivities heißen accepted/completed, keine
rückdatierten Ausführungsspans. Dauer wird am Aufrufer monoton gemessen und als Zahl gemeldet.

Zuerst ausführbare No-op-Signatur plus Datenschutz-/Strukturtests wirklich rot beobachten;
danach Positivliste implementieren. Hooks separat mit echten Runtime-/Dispatchtests rot.
Bekannter Commit muss trotz Logger-/Activityfehler sein Receipt/Result behalten. Unknown
nur nach erfolgreich bestätigtem MarkUncertain melden, nicht nach verlorenem Storewrite.
BCL-Metriken/aktuelle Queue-/Reservierungsstände und komplette US3-Matrix bleiben nach
besonderem nachfolgenden Testschritt offen; T074/T081 erst nach gesamter Abnahme schließen.

## Metrikschnitt vor Umsetzung

Bestand b80e47c. SessionDiagnostics meldet bereits nach bestätigten Writes, Dispatcher
Kopplung39: dort keine weitere Mess-/Storeverantwortlichkeit ergänzen. BCL-Metriken in
separate SessionMeasurements, mit hosteigenem Meter, zwei Sekundenhistogrammen und
Ergebniszähler; Labels nur definierte endliche Outcome-/Certainty-/Fehlerkategorien.
Keine Instanz-/Session-/Request-/Dispatch-/Workspace-/Providerkennungen als Labels.
Jeder synchrone Listenerfehler getrennt abfangen und über FailureCount sichtbar machen.
ObservableGauges lesen ausschließlich einen atomar veröffentlichten RuntimeStatistics-
Snapshot, niemals synchron Store-/Provider-I/O. Unbekannter Stand gibt keine Messung,
kein erfundenes Null. Aktive Reservierungen bedeutet alle belegten Slots, Unknown ist
Teilmenge davon. Neue readonly Storefacette liest Queue/Reservations/Unknown als einen
konsistenten SQLsnapshot; keine fachlichen Writes oder Schemanummeränderung.
Primärquelle https://learn.microsoft.com/en-us/dotnet/core/diagnostics/metrics-instrumentation
empfiehlt Histogramme für Zeiten, schnell gelesene Cachewerte in Observablecallbacks und
synchronisierten Zugriff. Erst separate Metriktests gegen fehlende Instrumente rot,
dann Port-/SQLtests gegen verhaltenslose Signatur rot. Runtime-Statistikrefresh separat
mit beobachtetem Red; kein unawaiteter Hosttask ohne Stop-/Disposegrenze. Alle neuen
Messungen bleiben für Steuerungsentscheidungen unmaßgeblich.
Messung2026-10-29T14:59:09Z: SessionDiagnostics80705420e29fc12a2625cb5fce584d142cf54fefb06f68642cc0778b83a36d3f,
RuntimeLifecycle2abd5163c991872403bc1550da2d9c6d489f55ee038789fb15086483f477a9fa,
SqliteQueryStore02309f558810f91c809ed1210c181e64ab998d5b016cf1c01d9069100f287fea,
ControlledQueryStore0e98242d99e01bf05b53ab832e72b80e014c0562b4769bc0bc1ad8c06dc6183e.
Schema Reservations behält freigegebene Zeilen: Gauges müssen ReleasedUtcISNULL filtern;
Unknown kommt aus zugehöriger Request.Certainty, nicht aus Anzahl Recoveryfälle.

Runtime-Refreshentscheidung vor Produktcode: eigener RuntimeStatisticsMonitor in
RuntimeComponents als sechste Komponente (Lifecycle bleibt8Felder). Nur bei vorhandenem
SessionDiagnostics wird ein eigener beobachteter Task gestartet. Anfangssample, dann
1sPause; GetRuntimeStatistics per await plus Monitorstop-Token-WaitAsync. Keine fachliche
Workerzählung/Signalslotänderung. Readfehler ziehen veralteten Cache zurück, zählen
Diagnosefehler, werfen nicht in fachliche Worker. Cleanup cancelt/awaitet Monitor vor
Store-/Leasefreigabe. Ignoriert ein Test-/Drittstore das Token, beendet WaitAsync nur den
Monitorawait; keine weiteren Reads nach Stop. SQLitequeue bleibt selbst verantwortlich
für ihre eigenen begrenzten Reads und Dispose. Native QueueOrder-Stateindex wird für
Queued genutzt; aktive Reservationindizes und Request-PK für Unknownjoin.
Neue Monitor-Redfälle: kein Sample ohne Hook, fehlende Fehlerisolation, fehlendes
Stopawait/Cachewithdrawal; separater Stopfall mit absichtlich tokenignorierendem Read.

Codemetr nach Metrikschnitt meldet Kopplung43>40 im kontrollierten QueryStore. Bestehender
Teststore mischt Bootstrap-/Disposeownership mit Query-/Lifecycleprojektionen. Vor reiner
Zerlegung bestehen16Diagnose-/Monitor- und3SQLstatistikfälle. Ownership/Bootstrap werden
als gemeinsame abstrakte Basisklasse herausgezogen; bestehende Übergänge und Locks bleiben
wörtlich erhalten. InMemory→Completion→Query→Lifetime ergibt Tiefe3 innerhalb bestehendem
Limit3; alle Consumer verlinken denselben Lifetimequelltext. Guardgrenzen unverändert,
keine neue Produktfunktion; vollständige Vertragsregression nach Refactoring erforderlich.
