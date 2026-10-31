# T026/T027: öffentliche Runtime und begrenzte Workerkomposition

Created: 2026-10-29T07:18:21Z
Owner: ausführende Implementierungssitzung, `feature/reserved-dispatch-contract`.
Basis `86ea819`. T112–T115 lokal umgesetzt, Gesamtimplementierung läuft weiter.

Bestandsanalyse vor Entwurf: SessionRuntime besitzt ausschließlich Signaturstubs. Catalog,
Admission, RecoveryService und RequestDispatcher sind getrennt testbar, aber kein öffentlicher
Host verbindet sie oder weckt Worker nach Annahme. Scheduler/Store sind bereits alleinige
Kapazitätsautorität; der Dispatcher verarbeitet ausschließlich frische Reservierungen.
IStateDirectoryLease hat bisher nur Pfad/Dispose, keinen expliziten Ownershipstatus oder
plattformgekapselten Pfadvergleich. Ein generischer Store-Start-/Annahmestopp-Vertrag fehlt.
Es existiert weiterhin nur ein kontrollierter Store; native SQLiteinitialisierung folgt später.

SHA256: SessionRuntime `77ed2805846c3e79cea64793c1ea5769511655f8e6851ed75ab30b74f89e10b9`,
ISessionStore vor T114 `c93809d0b321dd156c4f9b363b91d95b8d9175317e3d704c3507d3e0f8a939c0`
(aktueller Input ist der nach T114 geänderte Storevertrag, beim Schnitt zusätzlich manifestiert),
IStateDirectoryLease `a04890a313e6a9a7bc2747f43a15e153ccc28152337e78fe13ec56f80cbdf25e`,
RequestDispatcher `caabcf0c77e8b4133f7ea6c3a87dacfb3b8aa09c8efb5b3d735282e568980900`.
Der gemeinsame Gate in ControlledStoreState ist bereits Read-/Writegrenze; CreateSession
und der direkte FindSession-Testzugriff werden beim neuen parallelen Host ebenfalls
unter diesen Gate genommen. Das ist verhaltenserhaltende Serialisierung der Testinfrastruktur.

Entscheidung: SessionRuntime bleibt kleine Kompositionswurzel, RuntimeLifecycle besitzt
Zustand/Worker/Lifetime; eigene RuntimeSessions/RuntimeRequests/RuntimeRecovery-Facetten
prüfen Laufzeitstatus und delegieren an vorhandene Fachklassen. Host übergibt Store, bereits
gehaltene Plattformlease, Registrierungen/Profile und optional TimeProvider. Kein Netzwerk-
listener, kein obligatorisches Hostingframework, kein Herstellerzweig. Profile/Registrierungen
werden als Hostkonfiguration eingefroren. IStateDirectoryLease erhält IsHeld/Matches, damit
Common keine betriebssystemspezifischen Pfad-/Besitzregeln errät.

IStoreBootstrap (von ISessionStore geerbt) trennt Start mit der expliziten Lease und
atomaren Annahmestopp von Fachtransaktionen. ISessionStore wird async-disposable. Store-
Start verantwortet Format/Konsistenz und konservativen Wiederanlauf, ohne Provider-I/O;
das Double modelliert nur kontrollierte Opening-/Running-Zustände, native Beweise bleiben
T055/T061. Lease wird erst nach Beendigung der Storebenutzung freigegeben.

Höchstens MaxConcurrentRequests Worker, keine Task pro Queueeintrag. Ein begrenztes
Semaphore weckt nach Annahme/Recovery; zusätzliche endliche Storepolls verhindern verlorene
Wecksignale als Fortschrittsbedingung. Slots bleiben ausschließlich im Store. Erwartete
persistierte Requestfehler isolieren, Speicher-/Infrastrukturfehler als Runtimefehler und
Completion-Fault sichtbar machen. Kein unkontrolliertes Retry eines Dispatch. Completion
ist eine beobachtbare Task, Stop wartet auf eigene Worker. Statusprüfung sperrt Operationen
vor Start und nach Stop; Wiederstart derselben Runtime ist kein impliziter Neuanfang.

Dieser Schnitt prüft Start, Zusammensetzung, parallele Sends, Queries, konservative
Startprojektion und Idle-Stop/Dispose. Vollständiges Queue-/Cancel-/Grace-/Closeverhalten
und native Stop-/Crashabnahmen bleiben eigene zuerst rote Zyklen T070–T082/T061. Ein
früher erfolgreicher Idle-Stop ist keine vollständige Stopfreigabe. Keine offenen Fragen
für diese inkrementelle Vertikale; keine Produktfreigabe.

Zuerst Red: Runtime-Facetten/Start verfügbar, zehn Sitzungen mit je zwei Requests, native
Identitäts-/Inhaltszuordnung, FIFO je Sitzung und überlappende zwei Adapter, bestehende
Queued-Arbeit ohne Hostsignal, alte Running-Reservierung ohne Replay, instanzgetrennte
Stores, Lifecycle-/Options-/Leasefehler und Idle-Dispose. Danach minimal integrieren,
Regression und Full-Guard/Coverage/CI. Neue API-/Signature-Stubs nicht als Red ausgeben.

Refactorentscheidung, gemessen 2026-10-29T07:39:18Z:
Relevante Baseline 225/225 Unit- und 52/52 Contractfälle grün. Full-Guard meldet
Kopplung 43 in RuntimeStartupTests und 41 im kontrollierten Writer. Startprojektion
und Ownership-/Hintergrundfehler sind unabhängige Testverantwortlichkeiten; sie werden
in zwei Klassen mit gemeinsamem Kompositionsfixture getrennt. FindSession benötigt
nur Dictionary.TryGetValue statt der zusätzlichen CollectionExtensions-Abhängigkeit.
Keine Fachänderung, kein Grenzwert-/Scopewechsel. Danach beide Suiten und Full-Guard.

CI-Nachprüfung vor Testkorrektur, gemessen 2026-10-29T07:46:06Z:
Runtime-CI 40837103927 hat unter Windows einen fachlichen Testfehler: ParallelSessionsTests
Zeile 49 erwartet calls.Count=2, tatsächlich 0. ControlledProvider.SendAsync setzt Started
vor Aufruf des Sending-Delegates; Task.WhenAll(Started) ist deshalb keine Zusicherung,
dass dessen calls.Enqueue schon ausgeführt wurde. Dieser Kontrollfluss ist am aktuellen
ControlledProvider/ParallelSessionsTests geprüft. Kein Beleg für fehlerhafte Produktslots.
Entscheidung: eigene TCS-Barriere je Testprovider unmittelbar nach Enqueue, darauf vor
Overlapassertions warten. Allgemeines Fixture-Started bleibt unverändert, keine Sleeps,
Assertions/Limits bleiben streng. CI-Rot ist beobachtet; Contractregression/Fullqualität
und native CI am korrigierten Commit erneut. Kein weiterer Implementierungsblocker.
