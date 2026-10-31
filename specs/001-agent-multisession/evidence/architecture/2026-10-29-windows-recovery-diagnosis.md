# T125: native Besitzdiagnose ohne Freigabeverwechslung

Gemessen2026-10-29T22:58:19.7023731Z, Messquelle aktuelle Mountprobe;
geprüfter Produktstand7489450, Source dba419f35bf40fd7282a5b2f3bfe7c685576e2c62ceb570d5fec2d0f4f84498b.
Ursprung:40966955521 RecoveryExecutesTenNativeRestarts erhielt StoreAlreadyOwned,
40966982840 bei gleichem Source2c5fd5 vollständig erfolgreich. Neuer Stand7489450
ebenfalls native Quality/14-Szenarien erfolgreich. Fehlfarbe nicht verworfen.

Bestand: RecoveryProbeFlow.ExecuteAsync erzeugt privates Setup, schreibt Marker,
RecoveryProbeChild.StartAsync disponiert Setup-Lease vor Childstart; KillAsync ruft
Kill(entireProcessTree:true), dann WaitForExitAsync mit30s. Direkt danach erwirbt
NativeLease.OpenExisting den Store neu. ProbeHost.Stop/Dispose drainiert Runtime,
SQLite und Lease. WindowsOwnershipLock.Acquire klassifiziert Win32 32/33 als
StoreAlreadyOwned; zugrunde liegender Fehler und Schleifenphase fehlen im Bericht.
WindowsPrivateSecurity setzt bInheritHandle=0; vorhandene Handles sind SafeFileHandle,
Dispose idempotent. Statische Lesung allein entscheidet die native Fehlerursache nicht.

Entscheidung: isolierter Diagnosebranch ab7489450 mit ausschließlich zusätzlicher
synthetischer Phasen-/Iterations-/Win32ausgabe und eigenem CIjob. Native Recovery-CLI
wiederholt auf Server2025, unveränderte funktionale Assertions und keine Lease-Retrys,
kein PIDlockfallback, keine Fristerhöhung. So lässt sich Erzeugung, Kindstart,
Kill oder Wiederöffnung unterscheiden. Diagnoseworkflow/-ausgaben sind kein finaler
Source-/Coverage-/Stundennachweis und werden nicht in Produktbranch übernommen.
Bei reproduzierbarem Produktfehler folgt eigene aktuelle Analyse und echter
Regressionstest vor Green; andernfalls bleibt begrenzter unaufgeklärter Befund offen.
Keine Behauptung eines Antivirus-/Kernel-/Vererbungsfehlers ohne Nachweis.

Quellenabgleich2026-10-29T23:04:34Z: offizieller .NET10.0.14-Code
[Process.WaitForExitAsync](https://github.com/dotnet/runtime/blob/v10.0.14/src/libraries/System.Diagnostics.Process/src/System/Diagnostics/Process.cs)
verwendet HasExited als Schnellpfad. [ProcessManager.Windows.HasExited](https://github.com/dotnet/runtime/blob/v10.0.14/src/libraries/System.Diagnostics.Process/src/System/Diagnostics/ProcessManager.Windows.cs)
prüft historisch zunächst den Exitcode und erst danach das Kernelsignal.
[Process.Windows.WaitForExitCore](https://github.com/dotnet/runtime/blob/v10.0.14/src/libraries/System.Diagnostics.Process/src/System/Diagnostics/Process.Windows.cs)
wartet dagegen direkt am Prozesshandle. Microsoft beschreibt vollständige
Handlefreigabe und Prozesssignal bei [Prozessende](https://learn.microsoft.com/en-us/windows/win32/procthread/terminating-a-process).
Hypothese, noch kein Ursachenbeweis des ursprünglichen Laufs: asynchrones Exitwarten
kann früher zurückkommen als vollständiges Kernelende. Diagnose ergänzt direkt nach
dem bisherigen WaitForExitAsync ein nichtblockierendes WaitForExit(0) auf Windows
und protokolliert false unverändert als Beobachtung; keine zusätzliche Wartezeit
oder Freigabewiederholung. Erst tatsächlicher nativer Nachweis erlaubt Entscheidung.

Native Reproduktion vor Korrektur2026-10-29T23:07:37Z: Diagnose40970595632,
Job123786071389, Commit08e6208, repetition0/iteration5. Unmittelbar nach bisherigem
Exitwarten native-kernel-signaled=False, dann Acquire-Fehler32/StoreAlreadyOwned;
auch Dispose desselben Kindes sieht zunächst False. Iterationen1–4 zeigen ebenfalls
False vor später erfolgreichem Reopen. Rohartefakt windows-recovery-diagnosis-2,
lokal /home/user1/dev/.agenttemp/multisession-diagnosis-40970595632-2.
Uninstrumentierte Phasenvariante40970002551 ist ebenfalls fehlgeschlagen.
Das reproduziert den gemeldeten Fehlermodus und die unzureichende Exitwartebarriere;
keine Behauptung, ursprüngliche fremde Handles vollständig forensisch bestimmt zu haben.

Begrenzte Entscheidung vor Green: ein einziges ProcessExitWait im nicht ausgelieferten
ValidationHost wird von RecoveryProbeChild und den Ownership-/Crash-/ProbeProcess-
Testfixtures benutzt (Compilelinks wie vorhandene CrashProtocol-/HostBuildquellen).
Erst bestehende WaitForExitAsync-Semantik extrahieren; OwnershipProcess stellt zusätzlich
die reale nichtblockierende Kernelprüfung für die vorhandenen beiden Kill-/Unknown-
Tests bereit. Diese müssen nach Kill das echte Prozesssignal verlangen; native Red-CI
vor Green, Linux bleibt relevante grüne Refactorregression. Danach Windows über
Task.Run auf endliches BCL-WaitForExit(timeout) warten, Linux unverändert asynchron.
Kein Anwendungscode/Librarypackage, keine Lock-/SQL-/Recoveryregel verändert;
Timeoutwerte bleiben20/30/60/5s je bestehendem Aufrufer. Kein blindes Sleep/Lease-Retry.
Die schon vorhandene zusätzliche Journal-/Dateifreigabeprobe im CrashHarness bleibt
eigenständiger Integritätsnachweis, wird nicht als Kernelsignal umgedeutet.
Gesamtes Quality-/Codemetr-/native Paket-/Publishtor danach; wiederholte Diagnose
mit gleicher neuer Wartebarriere. Neuer Sourcehash und neue reale Stunde erforderlich,
weil ValidationHost zum Liefer-/Abnahmequellumfang gehört.

Lokales Green2026-10-29T23:18:51Z: vollständiges Linux-Tor
2a38af0332e74502aabe07a0589dc62d,1036Tests,2943/3016=97,5796 %;
Source203fbcf8453fe8baabdf184e0ed40265bd5fbbfb7313dd16fc8a7db9998017ed.
Codemetr441Dateien/459Typen,0Verstöße/Parsefehler/Skips. BCL-Kernelwait läuft nur
im nicht ausgelieferten Prozessmessgerüst auf Task.Run, kein synchrones Task-Warten
im Bibliothekspfad. Aufruferbudgets unverändert. Relevante grüne31Linuxfälle vor
Refactorextraktion dokumentiert; vollständiger Redquellstand/diff/untrackedtar samt
gemessener UTC vor Green unter artifacts/tdd/process-exit-red-source gesichert.
Native Greenwiederholung läuft separat als Diagnosebranch5fea615; finale native
Gesamt-/Paket-/Stundenprüfung wird für diesen Sourcehash neu angefordert.
