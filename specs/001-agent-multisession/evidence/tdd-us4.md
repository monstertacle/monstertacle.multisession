# US4 native Besitz-/Einbettungsnachweise

## Linux erster Besitzschnitt T088/T092

Architektur vor Erweiterung:
`architecture/2026-10-29-linux-ownership.md` mitBestandsdateien/Hashes/nativeABIquellen.
Ausgang9f48f54. Plattformpaket hatte zuvor keinen eigenen C#-Produktcode.
`artifacts/tdd/linux-ownership-red.log`:6/6fachlichrot gegen noch nicht verfügbare
nativeFähigkeit; keine Compilefehler alsRed. `linux-ownership-green.log`:dieselben6/6grün.
Ergänzte bereits grüne Regressionen für kanonischeNamen, Traversal, Symlinkfile,
Verzeichnis-/Lockinodetausch und vorabgebrochenenFilecreate sind kein weitererRedbeleg.

CreateNew/OpenExisting halten echteDirectory-/Lockdeskriptoren. Linuxx64/statx,
kein PIDfile-/Löschlock. ExistierendefalscheRechte undDateien bleibenunverändert.
Kernel-flock blockiert unabhängigeDeskriptoren auchimselbenProzess. Dispose löschtweder
ZustandnochLockinode. Noch kein zweiterProzess-/Kill-/SQLiterecovery-/FremdUIDnachweis;
Mountalias-/Netzwerkfähigkeitsmatrix undnativeUbuntuCI bleibenoffen. Diese offenen
Prüfungen gehören zuOwnerHauptimplementierung inT088/T092/T093, Abnahmebedingungen
stehen in den vorhandenenTasks. WindowsimplementierungT091 weiterhinfehlend.

T086/T089/T090/T094–T101 Einbettungs-/Paket-/Zielsystem-/Soakaufträge bleibenoffen;
keineBestandsmigration undkeinevollständigeUS4-Abnahme behauptet.
