# Kontrollierter Stop finalisiert wartende Anfragen

Gemessen 2026-10-29T12:56:31Z, Ausgang 7dd8bec.
RuntimeLifecycle.cs SHA256 6fb533541927a966a991a39ec8d3f194a3a0192fd9cfc84f6044548ebf178290;
RuntimeRequests.cs 99669e084ca255966cb143e04f1319681a50837abab435162d627da5078b8b96;
SqliteQueryStore.cs 27b902f20a0dfdfe8b20103076b74c142d5ca07c908ff1b89c966f4d54cf2181;
RequestCancellationPolicy.cs 09912e861dc139d9e8224fa227df3e014471684c0f9e9894853fbfea567a9dcd.

## Untersuchte bestehende Zuständigkeiten

T072/T078/T082. RuntimeLifecycle.StopAsync sperrt RuntimeRequests vor erstem Await und
stoppt danach die autoritative Storeannahme. _stop unterbricht aktive Sends und Workers;
bestehende Cancel-/Endbeleglogik finalisiert deren lokales Ergebnis. Wartende Anfragen
werden bislang unverändert gelassen und Store/Lease danach disposed. Dies verletzt den
kontrollierten Stopvertrag, während Crash weiterhin Queued erhalten muss. Queryports
liefern begrenzte, stabile Keysetseiten; CancellationStore finalisiert Queued atomar samt
Result/History/Intent. Diese Mechanismen reichen aus; keine weitere breite Storemethode
oder SQL-Duplikation nötig. Config-/Recoveryblockierung darf Queued-Cancel nicht verhindern.
RuntimeOwnershipTests sichern Zweitbesitz/Fatalfehler; Startup-/Crashproben bleiben getrennt.

## Entscheidung vor Code

RuntimeShutdown erhält nur den vorhandenen ISessionStore: nach Annahmestopp und aktivem
Stop-Token alle Session-/Requestseiten begrenzt lesen, nur Queued über vorhandene atomare
CancellationStore-Facette finalisieren. Keysetordnung bleibt bei Stateänderung unverändert.
Kein Provider-I/O und keine Transaktion über die gesamte Seiteniteration. Jeder bestätigte
Einzelabschluss ist dauerhaft; bei Fehler bleibt Stop sichtbar fehlgeschlagen, nicht
rückwirkend alles erfolgreich. Abbruch/Frist begrenzt jede Abfrage/Mutation per WaitAsync.
Die bereits bestehenden Storebusy-/Rollbackgrenzen bleiben verbindlich. Neue Annahme ab
Stop-Eingang ist gesperrt; Inflight-Commit kann als vorher begonnene Operation gewinnen.

Stopbudget wird vor Zustandswechsel/erster I/O erzeugt und gilt auch für Cleanup. Laufende
Workers werden auch bei Storefehler gestoppt, keine endlose Hintergrundannahme. Cleanup
ist eine einzige geteilte Aufgabe: Store schließen, dann Lease freigeben. Bei Callerfrist
kann Cleanup kontrolliert weiterlaufen, Lease bleibt bis Storeabschluss gehalten. Nie
früher freigeben oder Daten löschen. Kein erneuter Start nach Stop, wiederholter Stop/
Dispose idempotent; öffentliche Leseoperationen nach abgeschlossenem Stop Lifecyclefehler.

Tests zuerst mit echtem SQLite: 101 wartende Einträge plus unabhängige Sitzung, darunter
Konfigurationsblockierung, nach Stop alle Cancelled/NotStarted ohne Sends und nach Reopen
erhalten; Request-Keyset über Seitengrenze. Offener unkooperativer Send plus wartende
Follower endet lokal Unknown und hält genau seinen Slot, Follower werden sicher abgebrochen.
Neue Annahme nach Stop-Aufruf scheitert. Reale monotone Stopmessung unter Default10s.
Ein echter SQL-Triggerfehler im Cancel muss Stop fehlschlagen lassen, unveränderte letzte
Anfragezustände und bereinigten Fehler zeigen. Kontrollierter verzögerter Cleanup prüft
Deadline und Lease-Reihenfolge separat. Erst fachliches Red, dann minimales Green,
anschließend bestehende Startup-/Crash-/Cancel-/Callback-/Ownershipregressionen plus
Full-Guard/Coverage. Keine neue Freigabepolitik; Session-Close bleibt getrennt T079.

Zusatzanalyse vor Fehlerkorrektur, gemessen 2026-10-29T13:03:15Z. Zwischenstand
RuntimeLifecycle.cs SHA256 b6fca644bfaba1e34bdbda0bda5de978088d8ec555f6d83dede8e6c50bfad76c,
RuntimeShutdown.cs 783f9deaccf3ba3f8c6300e2253ef68afe361b272fc3b74f1d9b287e98ad5aa7.
Erste vier Stop-/Cleanup-Redfälle wurden grün. Weitere tatsächliche rote SQLregression:
Wiederholter Stop nach fehlgeschlagener Finalisierung kehrt wegen Stopped früh erfolgreich
zurück (repeated-failed-stop-red). Geschlossene Ressourcen sind kein dauerhafter Erfolg.
Zudem würde allein geteilter Cleanup die vorangehenden parallelen Seiteniterationen nicht
serialisieren; erste Freigabe könnte zweite Storeabfrage überholen. Entscheidung: der
bestehende RuntimeShutdown besitzt begrenzte Stop-Serialisierung, geteilte Cleanupaufgabe
und letzten Stopfehler. Lifecycle delegiert darüber, jeder wartende Caller behält sein
Budget. Nach abgeschlossenem fehlerhaftem Stop wird derselbe bereinigte Fehler erneut
sichtbar; Dispose darf bereits geschlossene Ressourcen idempotent freigeben. Retry bei noch
nicht beendetem Worker/Cleanup bleibt möglich, ohne doppelte Leasefreigabe. Keine zusätzliche
breite Storefacette oder Lockerung des Feldlimits.

Vollregression im Lauf 3da07397cf89458ca2afbbc172b19dcf fand einen alten Availabilityfall,
der Dispose des Runtimehosts als Queued-erhaltenden Crash benutzte. Der verbindliche
kontrollierte Stop muss gerade diese Queued finalisieren. Testkorrektur: zwei explizite
Fälle, einmal direkte Storegrenze ohne Runtime-Stop mit erhaltener Queued-ID und einmal
kontrollierter Stop mit Cancelled und ausschließlich neuer expliziter Folgeanfrage. Beide
müssen dieselbe ursprüngliche native Session behalten und lokale Konfigurationsblockierung
lösen. Kein Abschwächen der Recoveryregel und keine neue Produktfunktion; Fehlbeleg bleibt
im genannten Qualitätslauf. Echte Kindprozess-Crashmatrix unverändert zusätzlich ausführen.

## Windows-CI: Einzelcommits überschreiten Stopbudget

Gemessen 2026-10-29T13:23:16Z. Lauf 40879343516, Kopf42fc164: Windows-Integration
StopsAcceptanceAndFinalizesAllQueuedPages schlägt beim Stop mit StorageUnavailable fehl,
während Linux grün war. Fehlerbeleg artifacts/tdd/stop-ci-failure.log und CI-TRX;
keine native Windows-Freigabe für diesen Kopf. Der Ablauf hat 102 separat haltbare
Cancelcommits unter derselben 10-s-Frist. 27s gesamte Testdauer ist ausdrücklich kein
isolierter Stopmesswert; das CIprotokoll allein belegt keine Hardwareursache.
RuntimeShutdown.cs SHA256 490c7db9c1d7d0fbb326b8ed3dc23856c2732b2ee73e900b232d2fe0577008a9,
QueuedCancellationTransactions.cs 7d58b211946bf54b61c1120674c6ba24c60283940e5f3fbc2e397441640fe92a,
CancellationTransactions.cs f79cc78e00b79301d74ca0a370e47be733f1d6397fc18c1eefc35b1df1f8991d.

Entscheidung vor Änderung: bestehende schmale CancellationStore-Facette um atomaren
Queued-Sammelabbruch einer Sitzung ergänzen. RuntimeStop liest weiterhin begrenzte
Sessionseiten, aber führt pro Sitzung einen Commit aus. Bestehende reine Abbruchregel
und QueuedCancellationTransactions wiederverwenden; innerhalb der SQLtransaktion in
Sequence-Keysetseiten mit PageLimits.MaximumSize lesen und pro Seite schreiben, statt
alle Inputs zugleich zu laden. Kein Netzaufruf im Lock, keine Lockerung von synchronous/
Journal/10-s-Budget, keine neue breite Storemethode. Callerabbruch vor Commit rollt den
Sitzungssammelabschluss zurück; ein späterer bestätigter Commit bleibt final. Auch der
bestehende Close-/Recovery-Close-Code benutzt diese begrenzte Transaktion. T078/T082
decken diesen konkreten Folgeauftrag einschließlich nativeCI-Nachprüfung ab.
