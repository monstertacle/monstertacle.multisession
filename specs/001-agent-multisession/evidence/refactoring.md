# T103: Refactoring bei grüner Suite

Aktueller technischer Kandidat nach T125/T126: **fb1a167 / Source d51bf9f9...**,
native Gesamt-/Paket-/Guard-/Auditprüfungen erfolgreich. Verbindlicher aktueller
Index mit vollständigem Sourcehash, ausgewähltem Paketartefakt, Laufnummern und
Abnahmegrenzen: [delivery-candidate.md](delivery-candidate.md),
[build-native.json](build-native.json), [canonical-package-final.json](canonical-package-final.json).
Die älteren Zahlen/Quellstände in den folgenden Abschnitten bleiben historische
Checkpoints; sie ersetzen diesen aktuellen Bericht nicht. Die reale finale Stunde
und fehlenden externen Freigaben bleiben separat ausgewiesen.

Messstand 2026-10-29T22:48:18Z. Explizit verhaltenserhaltend, kein künstliches Red.
Konkrete Vorabanalyse mit Ausgangshashes:
[Native ABI-Konstanten](architecture/2026-10-29-native-abi-constants.md).

Nach 31 grünen nativen Linuxfällen wurden in LinuxNative, LinuxDirectoryPath,
LinuxFileIdentity, LinuxOwnershipLock sowie WindowsNative, WindowsDirectoryPath,
WindowsFileIdentity, WindowsOwnershipLock, WindowsPrivateSecurity und
WindowsStateDirectoryLease native Layoutoffsets, Fehlercodes, Pfadlängen und
Filesystemwerte benannt. Werte, Reihenfolge, Rechte, erlaubte Typen und API bleiben
gleich. ABIwissen bleibt in der jeweiligen nativen Verantwortung; gleiche
Puffergrößen verschiedener Strukturen erhalten getrennte Namen. Keine zusätzliche
Abstraktionsschicht, kein kopiertes Common-Sitzungsverhalten.

Danach vollständiges Tor `linux/8530362e86bf4cfcaf1669f640cd3f24`: 1036 Tests grün,
2943/3016 Produktzeilen (97,5796 %), Codemetr 440 Dateien/458 Typen ohne Befunde.
Netzwerkfilesystemkonstanten separat erneut gegen installierte Linux-Header geprüft:
[linux-network-policy-refactor.json](linux-network-policy-refactor.json).
Native Windowsbestätigung gehört zum finalen CI-Bericht.

Frühere gezielte Extraktionen: LocalDirectoryPath nach grünen Optionsfällen
(tdd-foundation.md); Leasekomposition vor Recoverysetup nach grünen CLI-/Recoveryfällen
(tdd-us4.md); gemeinsame State-/Availability-/UTF8regeln statt duplizierter
Persistenz-/Fakepolicy (tdd-us3.md und tdd-us5.md).
Fehlerkorrekturen wie explizites LOCK_UN, Stopcallbackfrist und Reopen vor Queuefreigabe
sind eigene fachliche Red/Green-Zyklen und werden nicht als bloßes Refactoring umetikettiert.
SOLID/DRY/KISS/YAGNI werden an Verantwortlichkeiten und diesen konkreten Änderungen
bewertet; Codemetr allein beweist sie nicht. Keine Baseline, Scopeverkleinerung oder
Grenzwertlockerung.

