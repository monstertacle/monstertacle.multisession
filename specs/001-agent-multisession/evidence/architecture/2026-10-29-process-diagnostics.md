# Diagnose nativer Probeprozesse

Bestandsprüfung 2026-10-29T19:22:51Z gegen e051325. `ProbeProcess` begrenzt
alle Kinder auf 60 Sekunden. `ValidationScenarioTests.RunPassedAsync` verwirft
bei Exit 2 den vorhandenen strukturierten Report durch einen reinen Zahlvergleich.
`CrashHarness.StartAsync` verwirft eine abweichende Signalzeile; `CrashWorker`
reduziert jede Ausnahme auf denselben Kategorienamen. Damit sind die Windows-
Fehler 40936649050 und 40937967664 nicht ausreichend eingrenzbar.

Geprüfte SHA256 vor Änderung:
ProbeProcess.cs 828cc5dba9fcbfe1965244062d8af063c781ad543d067e3807a10519d18a344a;
ValidationScenarioTests.cs cde28da65721e38e19fec6a6d11c9ed7a5b2b5a2eda203334e8524674534cf2e.
Aufrufer und Prozessabläufe einschließlich CrashRecoveryWorker wurden gelesen.

Entscheidung: vorhandenen endlichen Report im fehlgeschlagenen Test anzeigen;
abweichendes Worker-Signal im Testfehler erhalten. Worker meldet ausschließlich
bekannte Session-Fehlerkategorien, Timeout oder eine feste Restkategorie, keine
Ausnahmetexte, Inhalte, Pfade oder Credentials. Dies ändert keine Erfolgskriterien
und repariert die zugrunde liegenden Fehler noch nicht. Relevante grüne
Szenario-/Crashregressionen und vollständiger Guard folgen; neue beobachtbare
Diagnosekategorien werden mit einem gezielt fehlerhaften Worker-Store zuerst rot
abgesichert. Neue Zeitlimits erst nach konkreter Ursacheneingrenzung.

Zusätzliche Ausgangshashes: CrashWorker.cs
5c816a51b232b7aa6b762d7fe1bb732339b9a987f4eb3c1087da95430afcf7f1;
CrashHarness.cs nach reiner Diagnoseanreicherung
5705bdf8dbc23e68572681d2f3ed5ba7ef19f426c06b29877616597fc43a0b62.
Der erste neue Test erwartete irrtümlich StorageUnavailable. Die vorhandene
Headerprüfung meldet korrekt UnsupportedStoreVersion für ungültiges SQLite.
Nach Quellenprüfung wurde der Test korrigiert und gegen den ursprünglichen
Worker erneut fachlich rot ausgeführt; kein Produktfehler wurde dafür injiziert.
Dieser geprüfte Red-Stand ist unter process-diagnostics-format-red-* archiviert.
Die anschließende minimale Kategorisierung bestand 29 Diagnose-/Szenario-/
Crashregressionen. Die Grenze bleibt klar: Diagnose repariert keine Ursache.
