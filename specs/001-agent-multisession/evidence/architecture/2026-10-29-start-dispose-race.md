# Start und Dispose dürfen keine Runtime wiederbeleben

Bestandsprüfung 2026-10-29T20:17:33Z gegen 4c46e07.
RuntimeLifecycle.cs SHA256 f7e69c8570d93fef714d1e50cce886d07bf8de6ceb80be5f4e12d3bb5fd7467d;
RuntimeShutdown.cs 197def2c584cb8c1e99fb90526418fefae65fa75e678118ec5e67aec96a7cf98;
ControlledStoreLifetime.cs 92a9f11e484a73cc7437624e07e1e35a72edf702a20f8678d8e263b7eb14f8b2.
BootstrapPreparationTests, RuntimeOwnershipTests, RuntimeCleanupTests und die
öffentlichen Start/Dispose-Verträge wurden gelesen.

Befund: Start setzt Starting und wartet außerhalb des Gates auf Validierung.
Dispose behandelt `_components is null` wie Created, setzt Disposed und entsorgt
Shutdown. Start kann danach trotzdem Komponenten/Worker erzeugen und Running
setzen. Ein abgeschlossenes Dispose ist damit kein Lifetime-Beleg.

Entscheidung vor Umsetzung: Startup-Abschluss und Dispose-Anforderung in der
bestehenden Shutdown-Komponente verfolgen; keine zusätzliche Lifecycle-Feldlast.
Start darf bei bereits verlangtem Dispose keine Worker freigeben. Dispose wartet
auf den tatsächlichen Startabschluss und bereinigt danach über den vorhandenen
Ownershippfad. Eine bereits laufende Disposeoperation wird für konkurrierende
Aufrufer geteilt. Created bleibt hostbesessen. Keine externen Aufrufe unter dem
Lifecycle-Gate; asynchrone Disposearbeit wird nach Registrierung gestartet.
Fehlgeschlagene Disposes dürfen wie bisher nach tatsächlichem Cleanup erneut
beobachtet werden. Kein neuer Start-/Storetimeout wird erfunden.

Zuerst öffentlicher Barrierentest: Validierung halten, zwei Disposeaufrufe dürfen
nicht abgeschlossen sein und die Lease bleibt gehalten. Nach Freigabe schlägt
Start ausdrücklich fehl, kein Worker entsteht, Store/Lease werden genau einmal
bereinigt. Der alte Start wird im roten Test per eigenem Caller-Token abgebrochen,
damit der reproduzierte Fehler keine Hintergrundworker hinterlässt. Danach
Bootstrap-/Cleanup-/Ownershipregressionen und Full-Guard/Coverage. T072/T078/T083.

2026-10-29T20:22:10Z: Nach 209 grünen Vertragstests meldet der Fullguard einen
Maintainability-Index 14 (Mindestwert 15) für den erweiterten RuntimeLifecycle.
Geprüfte Fassung SHA256
3db50880b7c3115b9eac08f72c97aecaf41915550c6d548ed6ad43d1fdae7553.
Entscheidung vor Refactoring: bereits bestehende Recovery-/Facetten-/Dispatcher-
Komposition in RuntimePreparation bündeln. Lifecycle behält Starting/Running,
Dispose-Fence und Ownership-Cleanup; Prepare erhält Host/Lifetime/Options/Token
explizit und gibt ausschließlich RuntimeComponents zurück. Reihenfolge und
Fehlerpfad bleiben gleich. Keine neue DI oder öffentliche API. Grünstart ist
dokumentiert; anschließend gesamte Vertragssuite und vollständiger Guard erneut.
