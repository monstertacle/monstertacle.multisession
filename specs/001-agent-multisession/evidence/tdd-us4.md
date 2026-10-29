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

## Linux T088/T093 echte Prozessgrenze und SQLitejournal

Ausgang075d4e2; Architektur `architecture/2026-10-29-linux-process-recovery.md`.
`linux-process-recovery-red.log`:2/2fachlichrot, bestehenderHost erreicht erforderliche
nativeBesitzbarriere nicht. Nach Workerroute25/25Besitzregressionengrün. WeiteresRed
`linux-owned-journal-red.log`: erwartetes tatsächlichesRollbackjournal fehlt. Vorhergrüne
Crashsonde inCrashJournal extrahiert und gemeinsam genutzt; Worker hält uncommitteten
1MiBWrite erst nachnativerLease-/SQLitevorbereitung. `linux-owned-journal-green.log`:
26/26Plattformtestsgrün,0Skips. Parent→Kind StoreAlreadyOwned, Kind→Parent ebenfalls,
Kill und erneuter nativerBesitz,0600DB undaktivesJournal, Unknownrequest undexakte
Dispatchkennung imRecoveryfall sowieeinebelegteReservierung undeineUnknownhistory
mitursprünglichemInput bleiben erhalten. KeinProviderSend, keinReplay. DasKindergebnis
enthält nuröffentlicheKategorie/PID undkeinRohfehlermaterial.

Dasbelegt dieseLinuxprozess-/Journalschnitte lokal, nichtdiegesamteT093Matrix:
Windows, allegefordertenZielsysteme undAlias-/FremdUID-/Mountproben bleiben inihren
bestehendenAufträgenoffen. Kein vollständigesT088/T093-Häkchen gesetzt.

## Windows T087/T091: native Redprüfung vorbereitet

Drei WindowsOwnershipTests gegen noch nicht verfügbare Windowsfähigkeit, Architektur
`architecture/2026-10-29-windows-ownership.md`. Windows-TFM-Build unterLinux erfolgreich,
0Warnungen/Fehler, kein fachlicherRedbeleg. Codemetr378Dateien/389Typen ohneBefunde,
auchWindowsTestklassen gemessen. Präprozessorumhüllungen derPlattformtestklassen durch
explizite TFM-Compileauswahl ersetzt, damitStandaloneGuard alleKlassen analysiert.
NativeWindowsausführung desfolgendenCommits wird erst alsRed anerkannt, wenn die
konkreten ausgeführtenTestfehler vorliegen; Produktfähigkeit bisdahin nichtimplementiert.
