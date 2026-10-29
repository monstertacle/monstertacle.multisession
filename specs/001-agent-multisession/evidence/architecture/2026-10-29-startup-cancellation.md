# T076 Abbruch innerhalb bereits begonnener Storevorbereitung

Ausgang5d42d8f. Quellprüfung2026-10-29T16:32:22Z: Queue prüft Lease.IsHeld vor item.Run;
SqliteWork.Run wechselt auf running und prüft Token vor action. SqliteStoreState.Start
prüft Lease erneut, ruft RecoveryTransactions.Prepare aber ohne Token; dessen Run verwendet
CancellationToken.None. Der Caller kann nach Beginn der Queueaktion abbrechen, während
anschließende Konsistenzscans und Recovery trotzdem committen. Tokenregistrierung beendet
bewusst nur waiting-Workitems, keinen bereits begonnenen/evtl. committeten Write.

Vor Code entschieden: Token unverändert über Start→State→Recoverytransaction→alle
Konsistenzscans weitergeben, pro Row/Recoveryeintrag prüfen, vor Transaktionscommit wie
in SqliteTransactions.Run bereits vorgesehen. `_started`/`_accepting` erst nach Erfolg;
abgebrochene nicht committete Vorbereitung darf mit frischem Token erneut starten.
Keine rückwirkende Rücknahme eines bekannten Commits, keine Löschung, keine externe Aktion.
Einzelne native SQLiteanweisungen/JSONrowparsing werden nicht per Prozesskill präemptiert;
Cancel wird an den kooperativen Grenzen erkannt. Andere fachliche Writes behalten ihre
bereits getestete Cancel-/Unknown-/Commitsemantik.

Deterministisches TDDgerüst ohne Produktbackdoor: eigener IStateDirectoryLease-Testadapter
wird erst nach Factoryopen scharfgestellt und cancelt beim zweiten IsHeld des Startpfads.
Erster Check liegt im Queueworker VOR item.Run, zweiter in State.Start NACH running/Token-
Vorkontrolle. Kein geschätzter Delay, kein Reflection/Testconditional im Produkt. Die zwei
Checks dienen nur zum Erreichen dieser bestehenden Abhängigkeitsgrenze, nicht als öffentliche
Zählgarantie. Direkter Start mit tatsächlich laufendem Workitem muss OCE liefern, DBhash
unverändert, Running/History/Casezustand unverändert und Hostlease weiterhin gehalten.
Zweiter Fall startet danach mit frischem Token und führt konservative Recovery genau einmal
aus. Erst beide gegen Bestand rot beobachten, dann minimal Tokenfluss ergänzen.
Quellenhashes gemessen2026-10-29T16:35:15Z: Queue
ed4f1913ccb6e500ff3b5a52b23d68da7d1cc09125e2c569a30b9a03a08e7648,
Worka7f5c8e42f6896463bac650e516d78f7178c3b785deca4fece3cf4ddea019672,
State156d420255c646f08b3917e29c44f77a8be74a13dc8a1e927886eea143dd412c,
QueryStoref69d66d613518447e0ac975ea88bf9d44abcb9189b572cb37dc3ec51967a4a98,
Recovery9e10a83d39f9aebd506fe50ea7854fd09454d4aba45f9f974d0933bdf4058689,
StorageFailurePolicy339fb6a3c9211628cb4f3bbd16f448154ff4bb9e7d5bba939e90bd303e6fbaa8.
