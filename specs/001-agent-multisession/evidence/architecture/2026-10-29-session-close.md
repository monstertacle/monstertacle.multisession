# Sitzung ohne implizite externe Löschung schließen

Gemessen 2026-10-29T13:09:17Z, Ausgang 42fc164.
SessionCatalog.cs SHA256 f75f44b2fa4d7623c936c112461342a0d94599b1cde270af7c9f48d7f2f5f1b4;
SessionTransactions.cs 31fd30261c2ac062e2d3509408fa0dc339962ecea286efa40b5bbb7a9fbda58b;
CompletionTransactions.cs a4b85d1e4878e25a156f9f1f347513e158c869a439958e15403b1cbc1e1b559a;
RecoveryDecisionTransactions.cs 45562573ff131a29533c63850843c458c1f61a319693954f520e320d0b8cc0fa;
QueuedCancellationTransactions.cs 7d58b211946bf54b61c1120674c6ba24c60283940e5f3fbc2e397441640fe92a.

## Bestand und Entscheidung vor Code

T031/T037/T072/T079/T082. SessionCatalog.CloseSessionAsync ist ein Stub; RuntimeSessions
prüft Lifecycle und delegiert. Keine passende Storefacette für atomaren Close. Vorhandene
Canceltransaktion kann Queued finalisieren und Running-Intent persistieren; Dispatcher
beobachtet Intent und bestätigt Ende begrenzt oder hält Unknown. RecoveryClose kennt bereits
Closing/Closed und darf keine unbekannte Reservation freigeben. Completion lässt Lifecycle
unverändert; es braucht Closefortschritt auch dann, wenn der Closecaller inzwischen abbricht.
SessionTransactions.Bind öffnet bisher jeden Zustand wieder, auch Closing/Closed; dies ist
vor neuer Closefunktion verbindlich zu sperren. Öffnungsunsicherheit hat eigene Fälle ohne
Dummy-Request. Kein nativer Close-/Deleteport existiert oder wird heimlich ergänzt.

Neue kleine ISessionClosureStore-Facette: atomare Schließabsicht und erneute Closeprüfung,
verpflichtend im bestehenden Storevertrag. ISessionStore bleibt bei zehn eigenen Methoden.
Begin setzt Closing, finalisiert Queued über bestehenden Code und speichert Running-Intent
in derselben Transaktion. Opening erhält einen Opening-Recoveryfall; ein später Bind-Commit
darf Closing/Closed nicht wieder öffnen. Native I/O ausschließlich im aktiven Dispatcher.

Gemeinsame reine SessionClosurePolicy entscheidet Closing/Closed und Blockierung aus
getrennter aktiver/ungewisser Arbeit. Autoritative SQLprüfung berücksichtigt Queued/Running,
unfreigegebene Reservation, terminale Unknown-Anfragen und offene Recoveryfälle. Nur ohne
alle Hindernisse Closed, ClosedUtc einmalig; unbekannte Arbeit bleibt RecoveryRequired.
Bestätigte Completion prüft Close nach atomarer Release; Unknown hält Closing. Bestehende
Recovery-ConfirmEnded-Logik wird an derselben Regel ausgerichtet, historische Outcomes bleiben.

Public SessionClosure wartet innerhalb DefaultShutdownTimeout10s mit TimeProvider/Caller-
token auf lokale Finalisierung, beendet aber bei Unknown sichtbar als Closing. Wiederholter
Close idempotent, fehlende ID NotFound. Nach Callerabbruch bleibt einmal gespeicherte
Schließabsicht erhalten; Completion/Recovery aktualisieren Lifecycle ohne aktiven Closecaller.
Keine Inhalte oder Providerdaten löschen. Explizites Delete/Retention folgt getrennt T064.
Kontrollierte Storeprojektion nutzt dieselbe reine Regel; keine native Simulation behaupten.

Zuerst ausführbare Tests am echten SQL: Idle/Queued schließen und ClosedUtc erhalten,
Annahme danach verweigern, unbekannte ID, Rücknahme vor Commit, echter Teilwritefehler
rollt Close samt Queued zurück. Running wird bestätigt Closed oder Unknown/Closing samt
erhaltenem Slot; unabhängige Sitzung arbeitet weiter. Öffnungsunsicherheit bleibt Closing,
spätes Bind kann nicht wieder öffnen. Callerabbruch nach Intent verhindert automatischen
Abschluss nicht. Case-ConfirmEnded darf anschließend Closing zu Closed bringen. Für neue
Ports nur verhaltenslose Signaturen vor tatsächlichem Red, danach minimales Green und
bestehende Recovery/Stop/Callback/Crashregression sowie vollständiger Guard/Coverage.
