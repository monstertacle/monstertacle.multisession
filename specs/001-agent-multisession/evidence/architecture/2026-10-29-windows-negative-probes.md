# Native Windows-Negativmatrix

Bestandsprüfung 2026-10-29T20:33:21Z gegen e385ca8; dieselben nativen Quellen
bleiben im anschließenden isolierten Probenstand unverändert. SHA256:
WindowsDirectoryPath.cs 5a2338e950963984d8128dea7b3d8f42a22d371874b29edb684cbe880b028359;
WindowsPrivateSecurity.cs 32870750474a0331cdcc4c3238492bec0a814186689f567f1b8d564a50dca143;
PlatformTests.csproj 225058963e1d3ddfe96de7c80997605c800702ee8f5fde7c4f0672ad25d6c5a4.
Gelesen: native Pfad-/Security-/Identitäts-/Lifetimeimplementierung,
WindowsOwnershipTests, LinuxOwnershipRejectionTests und T087/T091/T093.

Der vorhandene Windows-Testumfang deckt Rechteerzeugung, breite Verzeichnis-ACL,
Lifetimebesitz und Prozesswiedererwerb, aber nicht die komplette Negativmatrix ab.
Die Implementierung besitzt bereits lexikalische DOS-/UNC-/ADS-Prüfung,
Reparse-Point-Handleprüfung und Hardlinkzählung sowie gehaltene Vorfahrenhandles.
Vorhandenes Verhalten wird als Regression untersucht; kein erfundenes Red durch
absichtliches Abschalten einer Prüfung.

Entscheidung vor Tests: reine native Windows-Testklasse für ungültige Verzeichnis-
und Dateinamen, Junctions, Hardlinks, breite Datei-ACL, geänderte Root-ACL und
verhinderten Rename bei gehaltener Lease. Daten und ACLs ausschließlich in
noncebenannten eigenen Agenttemp-Ordnern. Ein kleiner BCL-Prozessfixture startet
PowerShell New-Item mit strukturierten Argumenten für Junction/HardLink; keine
Admin-/Token-/systemweite Richtlinienänderung. Prozesse haben 30 Sekunden und
werden bei Fehlern beendet. Zielinhalte müssen erhalten bleiben, unsichere Rechte
werden nicht repariert. Native Tests sind per TFM auf Windows beschränkt, aber
sämtliche eigenen Klassen bleiben im unveränderten Full-Codemetr-Umfang.
Linux-Build/Codermetr prüfen nur Übersetzbarkeit und Umfang; Ergebnisse dürfen
erst aus tatsächlicher Windows-CI übernommen werden. Windows 11 bleibt NOT RUN.

Lauf 40951619323 auf Windows Server 2025: 36/37 Plattformfälle bestanden;
Junctiontest scheitert ausschließlich im Klassen-Dispose an rekursivem
Directory.Delete mit IOException/Parameter incorrect. Quelle der ursprünglichen
Behauptung blieb unverändert; kein fachlicher Sicherheits-Red wird behauptet.
Entscheidung vor Fixturekorrektur: den explizit selbst erzeugten Junction-Link
im Testfinally mit nicht rekursivem Directory.Delete entfernen. Zielverzeichnis
und dessen Inhalte bleiben erhalten. Danach kann der normale Fixtureordner
rekursiv gelöscht werden. Kein Rechte-/Token-/Systemeingriff und keine Änderung
an der nativen Bibliothek. Native Wiederholung erforderlich.
