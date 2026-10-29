# T103: native ABIwerte semantisch benennen

Gemessen2026-10-29T22:41:15Z, Basis8caa0da. Aktuell grün:
Linuxvolltor31cfc003e1ea48dc9c72ee8037ada854 mit1036Tests, darunter31native Fälle;
Windows-/Ubuntu-Gesamt-CI40962572345 bestand mit denselben nativen C#-Dateien.
Die seitdem geänderten CI-/PSdateien ändern diese Bestandsmodule nicht.

Geprüfte SHA256 vor Refactoring:
- LinuxNative df8bad306cb99a75ae5ce9db5db57b428ecbb910bb9acb2996dd5c27b495dad1
- LinuxDirectoryPath 7f6d2b530bb6aaf58dff743871a98f2c9efba1b8f7fad58fb7b16fa88e005ee2
- LinuxFileIdentity 378ef98230c2924404231badf645d35041fde3be32cef3d5018319335091f0b2
- LinuxOwnershipLock b989d432ff0b39e1b66881a799d61c8934799674c002c58102236c622a213fab
- WindowsNative a02fe99a618f7a563f65aea441ee6db979eb0ec1f77b3f86aed0ee529716526a
- WindowsDirectoryPath 5a2338e950963984d8128dea7b3d8f42a22d371874b29edb684cbe880b028359
- WindowsFileIdentity 605393ef3b58ed355484dafc423793499703e5bf57fa9964fc690e657fa00e7c
- WindowsOwnershipLock de77d7e6c2b8aa83c3b17aef0e89b3d68e00a5d00f43a06b673e21b81ef7e2de

## Bestand und begrenzter Auftrag

Handle- und Sicherheitsverantwortlichkeiten sind bereits getrennt. Read/RequireType
interpretieren aber native Bytepuffer über unbenannte Offsets; LinuxDirectoryPath nutzt
AT_FDCWD als -100, Locks klassifizieren nackte errno/Win32zahlen, native Dateitypen und
Filesystemallowlist erscheinen als anonyme Hexwerte. Die Kommentare erklären Teile des
Layouts, nicht jeden positionalen Wert. Dies ist ein konkreter Wartbarkeitsbefund gegen
Constitution II, kein Anlass für neue Interopschichten oder geänderte Datenformate.

Nur Werte durch lokale benannte Konstanten ersetzen: statx/statfs-Puffer und Feld-/Mask-
identitäten, Linuxopen/flock-/errnokonstanten, WindowsFILE_ID/ATTRIBUTE_TAG/STANDARD_INFO-
Klassen/Größen/Offsets, SecurityAttributes-Pointer-/Inheritoffset und Fehler-/Pfadlängen.
Exakt dieselben Werte, Vergleiche, Reihenfolge, Layouts und Rechte behalten. Keine neuen
Flags, FStypen, native APIs, Branches, Sourceexklusionen oder Packageupdates. Offensichtliche
0-/1-Schleifen-/Strukturwerte benötigen keine künstlichen Konstanten. ABIwissen bleibt
bei seiner bestehenden nativen Verantwortung; gleiche Puffergröße für verschiedene
Structs wird nicht als eine neue fachliche Kopplung behandelt.

Die externe Network-Magicsonde muss nach Benennung die tatsächlichen Allowlistkonstanten
auflösen und erneut gegen Linux-ABI vergleichen; alter Quellhash bleibt historisch.
Verhaltenserhaltendes Refactoring benötigt keinen künstlichen Red. Vorhandene31Linux- und
37Windowsfälle, falsche Plattform, Zweitprozess-/Kill-/Recovery-/Paketregression sowie
vollständige Quality/Codemetr danach erneut. Native Bestätigung erforderlich, bevor der
neue Source-/Paket-/Stundenstand final gilt. Keine Änderung der gemessenen Fristen.

Nachprüfung gemessen 2026-10-29T22:48:18Z: relevante Ausgangssuite
`artifacts/tdd/native-abi-before.log` 31/31 grün. Danach vollständiges Linux-Tor
`8530362e86bf4cfcaf1669f640cd3f24`: 1036 Tests bestanden, 2943/3016 Zeilen
(97,5796 %), Sourcehash
`dba419f35bf40fd7282a5b2f3bfe7c685576e2c62ceb570d5fec2d0f4f84498b`.
Codemetr 0.1.0: 440 Dateien, 458 Typen, keine Befunde, Parsefehler oder Skips;
Rohbericht `artifacts/tdd/native-abi-codemetr.json`. Die externe ABI-Sonde löst jetzt
die sechs tatsächlich verwendeten benannten Konstanten auf; erneut PASS gegen die
installierten Linux-Header, siehe `../linux-network-policy-refactor.json`.
Native Windows-CI und Stunde für diesen neuen Stand folgen separat.
