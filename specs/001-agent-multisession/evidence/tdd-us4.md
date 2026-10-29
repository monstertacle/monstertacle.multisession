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

## T086/T090 Common-Hostkomposition und Stop während Open

Architektur `architecture/2026-10-29-host-composition.md`. HostCompositionTests zunächst
1/1fachlichrot: Stop gibtEigentumfrei, öffentlicheOpenaufgabe bleibtnochunfertig. Keine
Compile-/InfrastrukturfehleralsRed. RuntimeOperations registriert jetztdenöffentlichen
Openablauf atomarmitRunningzulassung, StoplinkedCancellation undDrain vorStorecleanup.
SessionCatalog wartet aufOpen mitCancelgrenze undspeichertdanachUnknownopening, bevor
Eigentumfreigegebenwird. SpäterfertigerProviderOpen hatkeinen lokalenBindpfadmehr.
UnkooperativeexterneArbeit wirdnichtalsbeendetbehauptet. Danach204Contractfällegrün.

Zwei zusätzliche sofortgrüneRegressionsfälle: zweiCommon-RuntimesmitgetrenntenStores/
Leasen undidentischenRegistrierungsnamen haltenModelle/Settings/IDs/Historygetrennt;
fremdeKennungen liefernnull, spätereEingänge nachIdleohneNeustart, StopvonA berührtBnicht.
CommonAssembly hatkeinePlattform-/SQLite-/Hostingframeworkabhängigkeit. Diese schon
korrektenEigenschaften werdennichtnachträglichalsRed ausgegeben. Alle3gezieltenFälle
grün, Codemetr382Dateien/393TypenohneBefunde. Volltor/nativeNachprüfungfolgen.

DieserSchnittverfolgtöffentlicheCreateSession-Provideroperationen. RecoveryInspect/Resolve,
Close-/Startfristen undweitereRaces bleibenimvorhandenenT076/T079/T083-Audit ausdrücklich
offen; dasOperationsregister ist ihrWiederverwendungspunkt, keineGesamtabnahme.

WindowsRed tatsächlich nativzu127df3a inQualität40921348366: drei ausgeführte
WindowsOwnershipTests fehlgeschlagen, Infrastrukturfallbestanden,0Skips. Zwei erwartete
funktionierendeLeasen werfenUnsupportedPlatform; breiteACL erwartetStorageUnavailable,
tatsächlichUnsupportedPlatform. Original-TRX unter
/home/user1/dev/.agenttemp/multisession-ci-40921348366/windows/ undLog
artifacts/tdd/windows-ownership-native-red.log. ErstnachdiesemNachweis Greenimplementierung.

WindowsGreenkandidat nachnativem3/3Red implementiert. LokalerWindows-TFM-Build ohne
Warnungen/Fehler; Linux-Wrongplatformkonsument bestätigtUnsupportedPlatform, keinNo-op.
PrivateSecurity verwendetvorhandeneBCLACLzugriffe nachHandlepinning undatomareSecurity-
Attributes beiErzeugung. FileCreateNew ersetztkeineBestandsdateien; ShareNone-Lockbleibt
bisDisposeoffen, Elternhandles verhindernPfadtausch. NeueTestablage ausdrücklichunter
UserProfile/dev/.agenttemp stattpotenziellgekürztemTEMP-Pfad.

LokalesVolltor linux/64ff350be59d4f12a4beccf2e9e5426b, Sourcehash
baec2245c1db6d5a9faed7c07fbc1d1cd290d11a89dd58445a95e9673ac01bdb,
940/940Tests,2840/2916Zeilen=97.3937%. Codemetr387Dateien/398TypenohneBefunde/
Parsefehler/Skips. DasmisstWindowsklassenstatisch undtestetCommon+Linuxnativ;
Windows-Verhaltensgreen istnoch zuprüfen, keinT087/T091-Abschluss.

NativeGreen zu0d196319dadc108c4af4d3e89108033ea3cd1930: Qualität40924127106 aufWindows
undUbuntu erfolgreich,Guard40924127596 grün. Damit3WindowsACL-/Besitzfälle tatsächlich
nativbestanden, außerdemCommon-Hostkomposition undOpen-/Stop-Korrektur. T086/T090
abgeschlossen; übrigePlattform-/Paket-/Lifecycleaufträge bleibenoffen. T087/T091 benötigen
weitereAlias-/Reparse-/Zweitprozessabnahme undechteWindows11-Zielsystemprobe.

## T093 Windowsprozessprobe: Redvorbereitung

NativeWindowsgrundfähigkeit0d19631 beidseitiggrün, danachvorhandeneLinux-Prozessprobe
TFMneutralvorbereitet: nativeLeasealias ausdemjeweiligenProjekt, gemeinsamerWorkerlocator
wähltdasnativeTFM, ACL-/Unixrechte inNativeFileAssertions getrennt. VorherLinux26/26grün,
nachmechanischerVorbereitung erneut26/26grün, WindowsTFM-Buildgrün. ErsterGuard fand
Kopplung47imRecoverytest nachACLaufnahme; nativeRechteverantwortungindasbenannteFixture
ausgelagert, dann389Dateien/400TypenohneBefunde. KeinGrenzwert-/Scopewechsel.
Windows-Ownershipworkerroute weiterhinfehlend; erstkonkretenWindowsReddiesesfolgenden
Stands beobachten, dannbestehendenWorker plattformübergreifendverdrahten. Architektur
`architecture/2026-10-29-windows-process-recovery.md`. KeineCompilefehler alsRed gewertet.

## T089/T094 öffentliche CLI: Argumente und ehrliche Statusberichte

Vorabanalyse `architecture/2026-10-29-validation-host-cli.md`, Basis468b401.
VorGreenvollständigerRedzustand gesichert: Gitbasis invalidation-host-cli-red-base.txt,
TrackedDiff undTar derunversioniertenTest-/Analysedateien unterartifacts/tdd/,
SHA256manifest undgemessenerErfassungszeitpunkt2026-10-29T18:01:51Z. Anschließend
originalerTRX validation-host-cli-red:25/25fachlichrot, erwarteteExit2/3tatsächlich0.
KeineCompiler-/Restorerote alsTDDausgegeben. Greenvalidation-host-cli-green:25/25.

KleinerArgumentparser/Szenariokatalog/JSONReport, alle17Quickstart-/Livenamen erkannt;
relativeRoots, fehlende/doppelte/unbekannteArgumente undunbekannteSzenarien FAIL/2.
KeineRohargumente oderAusnahmetexte imReport. UnfreigegebeneLivefälle NOT RUN/3 vor
Netzwerk-/Credential-/State-I/O; fremdeTestdaten nachweislichunverändert. Nochfehlende
Szenarien sindebenfalls NOT RUN/3, keinPASS ohneausgeführteProbe. InterneCrash-/Diskfull-
Workerprotokolle weiterzuerstverdrahtet. TatsächlicheSzenarien/neueprivateUnterablagen/
Consumer-/Paketprobe bleibenT089/T094/T096; dieseCLI-Teilprüfung istkeinSC-011-Nachweis.

T093 nativzu468b401 in40926185397:2/2Windowsprozessfälle fachlichrot amfehlenden
Ownershipsignal,4bestehendePlattformfällegrün,0Skips. Originallogs
artifacts/tdd/windows-process-recovery-native-red.log undTRXunter
/home/user1/dev/.agenttemp/multisession-ci-40926185397/windows/ gesichert.
Danach vorhandenenOwnershipworker plattformneutralverdrahtet: gleicherCode mitnativer
TFM-Leasealias undunkonditionierterProgramroute, keineQuellklassehinter#ifversteckt.
Linux-Gesamtregression grün: linux/c2f3a8a7912240f289dc44b2e58edcab,
Sourcehashdcb601666f3f137aa406cbd53f2932ab700e7160825e6ca5271fb0244d7dc2ce,
967Tests,2847/2918Produktzeilen=97.5668%,Codemetr394Dateien/405TypenohneBefunde.
WindowsGreen diesesWiringstands folgtnoch, T093vollständigeZielmatrixbleibtoffen.

## T089 eigenständiger öffentlicher Sourceconsumer

ConsumerOwnedProvider undEmbeddedConsumerTests imkonsumierendenIntegrationprojekt:
keineBibliotheksinternenAPIs/InternalsVisibleTo, einRuntime/einSQLiteStore, eigener
Adaptermitzwei unabhängigenbestätigtenHeads. ErsteSendsüberlappen, StatusRunninglesbar,
Folgeanfragen/volleUnicodeinhalte/Historyzuordnung korrekt; verzögerteAnfragekooperativ
Cancelled/ConfirmedEnded unter2s, StopohneDatenlöschung,5Originalkennungen nachReopen.
ErsterfachlichausführbarerFallsofortgrün, keineProduktkorrektur undkeinerdichtetesRed.
VorherigeCA1861-AnalyzerfehlerderTestarraysausdrücklichkeinRed; statischreadonlyerwartete
Werte. Logembedded-consumer-observed.log:1/1in440msgrün. NativeLease-/NuGetnachweis
bleibtgetrennt; kontrollierterRetainedTestLease behauptetkeinenOSbesitz.

## T089/T094 echte Hostausführung in privater Runablage

ValidationStateRootTests:3/3fachlichrot amfehlendenAusführungs-/Rootvertrag, erwartetes
FAIL/2oderPASS/0 tatsächlichNOT RUN/3. Redbasis/TrackedDiff/neueTestdatei-Tar/Hashes und
Messzeit2026-10-29T18:39:19Z unterartifacts/tdd/validation-state-root-red-* gesichert.
NachGreen29/29CLI-/Root-/Consumerfällegrün. SharedConsumerOwnedProvider wirdim
nichtproduktivenHost ausdemkonsumierendenTestprojektquelltextmitkompiliert, nurBCL/
öffentlicheCommonverträge, keineXunit-/Testassembly-/Bibliotheksinternenabhängigkeit.

NativeValidationbasis neuprivaterzeugen odervorhandeneleere/privatebeziehungsweise
markierteBasisverwenden; belegteunmarkierteAblagevorLock-/Markeränderung abweisen.
Rootdateiniereparieren/ersetzen. JeLaufeigenesprivatesrun-GUID-Verzeichnis; Host prüft
2paralleleSessions/Followups/Status/Cancel/Stop/Reopen und5OriginalIDs, erstannPASS/0.
ZweimaligeHostausführung: getrennteRunIDs, je2Sessions/5Requests/5History/4Succeeded/
1Cancelled/0aktiveReservations; alleDateibytes deserstenLaufsbleibenunverändert.
PrivateProbeablagen bleibenzurInspektionerhalten; vorhandeneElternverzeichnisse nötig,
keineunsichereCreateDirectory-Kette durchAliase. FehlendeSzenarien weiterNOT RUN,
Live ohneFreigabe ohneState-/Provider-I/O. KeinNuGet-/Live-/vollständigesT094-Häkchen.

WindowsOwner-Portzu391e5e9 in40932597035 tatsächlichrot:6/7Plattformfällegrün,
privatesOriginaljournalnachKillvonWindowsPrivateSecurity.Require abgelehnt.
DanachengeProduktkorrektur: DirectoryownerweiterUser; DateiownerUseroderaktueller
Default-TokenownernurbeiBuiltinAdministrators. DACL unverändertgenaueineUserFullControl-
ACE, keinefremdenACEs, keinebestehendenACL/Ownerrepariert. NormativeR-07-Abgrenzung,
primäreOS-/BCLquellen undAdmin-Sandboxgrenze imArchitekturbeleg. Keine Änderungder
RechteaufirgendeinemHost oderProzesstoken. NativeWindowsGreen diesesCodes noch nötig.

LokalesVolltor linux/529d9f698a4048cc8996b14081e18a08 erfolgreich:972Tests,
Sourcehashac8f8fcf08ca60254951e6dc79660f19a8542e627574b3c35c700453d2fa7965,
2848/2919Zeilen=97.5677%,Codemetr401Dateien/414TypenohneBefunde. Linux misstWindowscode
statisch, nativeVerhaltensbehauptungweiteroffen. T093/Zielsystemprüfung nichtabgehakt.

NativeJournal-/Ownershipgreen zud3d38f7c89f9b21ef3693ce35abdef0deaf920c6:
Qualität40934964534 aufWindowsundUbuntu erfolgreich,Guard40934964497grün. Private
Default-Tokenownerjournale unverändertvalidierbar, öffentlicheOriginaljournalrechte
abgewiesenohneDB-/Journaländerung. Zweitprozess/Kill/Unknown-/Dispatch-/Historyerhalt
beidseitigbelegt. Windows11-Zielhost-/Alias-/FremdUID-/Soakmatrixbleibteigenständigoffen.

## T094 deterministic/routing/failures/cancellation

Vorabanalyse architecture/2026-10-29-host-scenarios.md. Redzustand(Gitbasis/TrackedDiff/
Tar/Hashes/Zeit)vorGreenunterartifacts/tdd/host-scenarios-red-*gesichert;4/4fachlichrot,
fehlendeAusführungliefertNOT RUN/3stattexpectedPASS/0. Danach4/4grün in3s.
HosteigeneProbeProvider/Barrier/Trace undöffentlicheProbeHostkompositionohneNetzwerk:
10Sessions/20Success,10Sessions/100FIFOkorrekteSuccess,5Failed+5Success,1kooperatives
Cancelled/ConfirmedEndedunter2s. UnabhängigeSendsnachweislichgleichzeitiggehalten;
Callanzahl/Originalinput/Model/Session/Request/Sequence/History stimmenüberein.
CLItestsprüfenzusätzlichdieechteDBzählungnachProzessende. GenerischeBackendfakes sind
keinnativerHerstellerprotokoll-/Livenachweis. WeitereSzenarienbleibenT094offen.
