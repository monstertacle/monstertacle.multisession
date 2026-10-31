# Stopfrist bei blockierenden Abbruchcallbacks

Gemessen: 2026-10-29T21:18:49Z. Ausgangscommit: 7e33651.
RuntimeLifecycle.cs SHA256: 059795a62b2b97065dcd2850f29cc8ea6b015b92ad724d9345d66208545bedf6.
RuntimeShutdown.cs SHA256: ea1d4b99f7c18dbffff658ea6fd2d8a6da49a48c9f50201bd5d6912f125a7fc7.

Anforderungsbezug: T072/T078, begrenzter Stop und Ressourcenbesitz bis zum tatsächlichen Ende lokaler Arbeit.

## Bestand und Kontrollfluss

RuntimeLifecycle.StopAsync sperrt neue Operationen, erstellt die monotone Deadline und serialisiert CompleteStopAsync. Dort ruft StopWorkers unter dem Lifecycle-Lock synchron CancellationTokenSource.Cancel auf. Registrierte Providercallbacks können deshalb den aufrufenden Stopthread festhalten, bevor irgendein WaitAsync die Deadline durchsetzen kann. Auch WorkAsync verwendet im Fehlerpfad synchrones Cancel. CleanupCoreAsync wartet bisher nur RuntimeOperations und Worker, nicht einen eigenen asynchronen Worker-Cancelauftrag ab, und gibt danach Store/Lease frei.

RuntimeOperations zeigt bereits den passenden Erweiterungspunkt: CancelAsync starten, Task behalten, vor Ressourcenfreigabe drainen. RuntimeShutdown besitzt die serialisierte Stop-/Cleanupverwaltung. Storeverträge, Persistenzschema, öffentliche Signaturen und Provideradapter brauchen keine Änderung. ControlledProvider.Sending erlaubt einen kontrolliert blockierten Token-Callback; SchedulingData und RuntimeTestComposition liefern bestehende öffentliche Runtime-Testkomposition.

## Entscheidung und Risiken

Workerabbruch asynchron genau einmal auslösen und dessen Callbackabschluss in RuntimeShutdown verwalten. Cleanup wartet auf diesen Abschluss, ohne die callergebundene Stopfrist zu verlieren. Callbackfehler werden bereinigt gemeldet; Ressourcen dürfen nach beendetem Callback dennoch freigegeben werden. Kein erzwungenes Threadende und keine vorzeitige Leasefreigabe. Aufrufer können nach abgelaufener Stopfrist erneut Stop/Dispose ausführen. Der Workerfehlerpfad nutzt denselben Abbruchweg. Keine offenen implementierungsrelevanten Architekturfragen.

## Testplan vor Green

Ein echter öffentlicher Contracttest registriert im laufenden Send einen Callback, hält ihn per Barriere und startet Stop auf separatem Task. Bei 100-ms-Stopfrist muss innerhalb von zwei Sekunden ein bereinigter StorageUnavailable/Unknown-Fehler eintreffen; die Lease bleibt bis zur Callbackfreigabe gehalten. Der alte Code muss fachlich an dieser Zeitgrenze scheitern. Finally löst die Barriere und räumt auch beim Red auf. Danach Callbackfehler und regulären Stop sowie bestehende Lifecycle-/Recoveryregressionen prüfen; kompletter Codemetr-/Coveragegate und native CI bleiben erforderlich. Die bereits laufenden Stundenläufe gelten nach Produktänderung ausschließlich als früherer Checkpoint.
