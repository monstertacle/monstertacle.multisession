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

## T094 capacity/workspace/shutdown/limits

Vorabanalyse architecture/2026-10-29-host-boundaries.md, RedzustandvorGreenarchiviert
unterartifacts/tdd/host-boundaries-red-* (Gitbasis/Diff/neueAnalyse/Tar/Hashes/Zeit).
VierneueCLI-Fällefachlichrot,4bisherigeRegressionengrün; danach8/8Szenariofällegrünin6s.
Kapazität2gehalteneRunning+8paralleleEingängebeiQueue3 ergibt5Accepted/5QueueFull mit
NotAccepted; nurexaktdie5Acceptedgesendet. WorkspaceAgentTasksmitSchreibintent:2gleiche
Keysnieparallel, unabhängigerKeyläuftimzweitenSlot, wartenderSharedrequestdanachFIFO.
ShutdownabEingangunter10s:2QueuedCancelled/NotStarted,1UnknownmitReservationnach
Stop/Reopen; unkooperativerBackendabschlussspätererzeugtkeinenErfolg/Replay.
LimitsUTF8genau6Bytesinklusive/7Bytesexklusive, leerundkaputtesUTF16vorReceipt abgewiesen;
2Success+1FailedOutputLimitExceededmitvollständigleererFehlerausgabe, frühereHistory
unverändert. ÖffentlicheDBzählungenunabhängigvomReportgeprüft. KeinProduktsemantikwechsel,
keineSchwellenlockerung; ProbeHostProfilekind gezieltAgentTaskfürWorkspace gewählt.

## T094: Isolation, Retention und externer Vertrag

Am 2026-10-29T19:22:22Z dokumentierter Schnitt nach Vorabanalyse
`architecture/2026-10-29-scenario-dispatch.md`: drei fehlende CLI-Abläufe zuerst
fachlich rot, danach elf Szenariotests grün. Die Instanzprobe verwendet zwei
native Stores und getrennte Profile; Retention löscht nur die abgeschlossene
Sitzung und bewahrt ungewisse Arbeit; der externe Adapter behält Format 7 und
seinen Fortsetzungsstand. Kein Produktverhalten wurde dafür geändert.

Vollprüfung `artifacts/quality/linux/77805b160ca14e5182b38c152d700ca6`:
983 Tests grün, 2848/2919 Produktzeilen (97.5677 %), Sourcehash
`ab33a342361f50cd1a740b69b02e8d2453da3f7735e566ba12a6d4fa6c900e6c`.
Codemetr 0.1.0: 416 Dateien, 432 Typen, keine Verstöße, Parsefehler oder
übersprungenen Dateien. Native CI für diesen Schnitt steht noch aus.

Die Windows-Läufe 40936649050 und 40937967664 sind fehlgeschlagen:
Routing-Probe beziehungsweise ein Crash-Worker erreichten ihre erwarteten
Abschlüsse nicht. Linux war jeweils grün. Die Fehler sind T094/T099 und dem
SC-009-Auftrag zugeordnet; sie werden vor Abnahme untersucht. Ein bloßes
Wiederholen oder ein grüner früherer Quellstand ersetzt diesen Nachweis nicht.

## T094: native-context

2026-10-29T19:29:09Z: fehlende CLI-Ausführung zunächst NOT RUN und damit
fachlich rot; elf bisherige Fälle grün. Archiv native-context-host-red-* mit
Gitbasis, Diff, neuer Analyse und Messzeit vor Green. Danach zwölf Szenariofälle
grün. Echte Providerpakete mit kontrollierten HTTP-Handlern behalten Conversation
beziehungsweise Interactions-Head und senden ausschließlich die neue Eingabe.
Ein OpenAI-Kontextfehler bleibt ProviderContextLimit ohne Meldungstext oder Retry.
Gemini-spezifische Kontextgrenzen bleiben ausdrücklich T034/T094/T105 offen.

Volltor linux/426a0f712405424bad6294d5d6f87147: 985 Tests grün, 97.5677 %,
Sourcehash 76778b8c59bbdc76a523624123c4d515fd95a7abb1d336f2204162a12262f0dd.
Codemetr 418 Dateien/434 Typen, keine Verstöße oder ungemessenen Dateien.

## T094: native Recovery-CLI

2026-10-29T19:34:06Z: Recovery-CLI zunächst fachlich rot mit NOT RUN,
Redzustand vor Green unter recovery-host-red-* archiviert. Danach zehn echte
Kindprozess-Kills mit nativen Leases und Wiederöffnung bestanden. Je Store
bleiben zwei Sessions, sechs ursprüngliche Request-IDs, fünf Erfolge und eine
ungewisse reservierte Ausführung erhalten. Drei wartende A-Anfragen werden
FIFO fortgesetzt, B wird weder erneut gesendet noch seine native Identität ersetzt.
Der Test prüft jeden der zehn Datensätze zusätzlich direkt nach Prozessende.

Nach einem Kopplungsbefund wurde die Lease-Erzeugung vor dem fachlichen Setup
komponiert; 31 gezielte Regressionen und der vollständige Guard sind grün.
Volltor linux/1b2430c415354159902ae2539975deb2: 986 Tests, 97.5677 %,
Sourcehash fc8b42f48b39cf924da02ef69f3ec9b1976090c49dd2fc7fa8c2181dfd510033.
Codemetr 420 Dateien/436 Typen ohne Verstöße. Native Windows-Prüfung für diese
neue CLI bleibt ausstehend; keine vollständige Zielsystemabnahme behauptet.

## T094/T100: Soak-Zeitplan und tatsächlicher Lauf

2026-10-29T19:38:37Z: Vier kontrollierte Zeitplantests waren fachlich rot,
danach grün. Compiler-/Analyzerfehler der ersten Signaturfassung zählen nicht
als Red. Der geprüfte Red-Stand wurde vor Green unter soak-schedule-red-*
archiviert. Die tatsächliche fehlende CLI-Ausführung war NOT RUN; ihr Green
bleibt bis zum echten Stundenende offen. Zeitplantests ersetzen SC-007 nicht.

Volltor linux/e4a5aeec3f8f4aab83e26b33a9a7de53: 990 Tests grün,
Sourcehash 3a9fd0d19eae5743832fac534a4b198e038a22831a65ca6105a9e75748b92ad9,
97.5677 %, Codemetr 423 Dateien/440 Typen ohne Verstöße. Eine eingefrorene
Binärkopie dieses Zwischenstands läuft separat eine monotone Stunde;
Pfad unter artifacts/tdd/soak-candidate-path.txt. Ergebnis noch nicht vorhanden.
Dieser Zwischenstand ist kein finaler identischer Source-/Paketnachweis.

## T088/T092/T122: tatsächliche Fremdeigentümer, Hardlink und explizites Unlock

Vorabanalysen: architecture/2026-10-29-linux-owner-hardlink-probes.md und
architecture/2026-10-29-linux-explicit-unlock.md. Drei zusätzliche native Regressionen
prüfen abweichende UID an eigenem Verzeichnis/File (kontrolliert per sudo chown, im
finally zurückgesetzt) und einen eigenen Hardlink; alle drei zunächst unmittelbar grün.
Keine Produktänderung oder neuer Red für diese bereits vorhandenen Ablehnungsregeln.

Das erste Volltor74226641a1394e5f8b4d7bff0fe7610f scheiterte aber einmal an direkter
Wiederübernahme nach Dispose mit StoreAlreadyOwned. Genaue fork-Terminierung dieses
Laufs nicht behauptet. Danach deterministisch reproduziert: LinuxOwnershipReleaseTests
hält ein libc-dup der tatsächlichen eigenen Ownership-File-Description, disponiert die
Lease und erhält beim Wiedererwerb mit altem Code StoreAlreadyOwned. Echte fachliche
Redausgabe linux-unlock-red.log; HEAD/Diff/Untracked-Tar vor Green archiviert.

Jetzt LOCK_UN vor Close, beide Handles auch bei Fehler geschlossen, altes Dispose
idempotent. Derselbe Test prüft sofortige erfolgreiche Nachfolgelease und deren weiterhin
exklusiven Besitz nach zweitem alten Dispose. Kein PID-/Dateilöschen, Sleep oder Retry.
LibraryImport im Test ist ein eigenes natives Messinstrument; UnsafeBlocks nur dafür im
Platformtestprojekt explizit. Alle 31 Linuxplattformfälle sowie das vollständige Tor
linux/7c418d4439924149b0f5e1b0a1e5c107 bestehen: 1010 Tests, 2903/2977Zeilen=97.5143 %.
Fullguard 434Dateien/452Typen, keine Verstöße/Parsefehler/übersprungenen Dateien.
Native CI folgt; Windows-11-/Netzwerkfilesystem-Abnahme bleibt unbehauptet.
Gemessener Sourcehash dieses vollständigen grünen Stands:
`a55d178360a2c11c9a5427a693cffb7d1d130c64928d7eb1187318e11b62d1e0`.

## T124: exakt derselbe kanonische Paketstand

NativeProduzent40962572345 hatte bei identischem Sourcehash unterschiedliche sechs
Windows-/Linuxarchive, einschließlich unterschiedlicher DLL-/XMLbytes; deshalb kein
identischer Paketkonsum daraus behauptet. Analyse vor Änderung
architecture/2026-10-29-canonical-package-consumption.md. Nach Quality lädt jetzt ein
zweiter nativer Consumerjob je OS dasselbe Linux-Artefakt desselben Workflowruns.

`canonical-package-hashes-red.log`: lax ausführbarer Prüfer akzeptiert sechs tatsächlich
manipulierte/fehlende/zusätzliche/doppelte/ungültige/fehlgeschlagene Produzentenfälle;
6fachlichrot/1positivgrün. Vor Green HEAD/Diff/Untracked-Tar archiviert. Minimaler
Hashprüfer kontrolliert exaktes Dateiset vor Hash-I/O und alle sechs Archivdigests;
NuGetlayout bleibt bei bestehendem Test-PackageLayout, keine doppelte Regel.
Ein Parserfehler im ersten Greenversuch war kein fachlicher Red; positiver Testfall
verhinderte korrekt einen falschen Gesamtpass trotz sechs negativer Exitcodes.

Echter Produzentenreport enthält zusätzlich vier Inputhashes. Zuerst separater echter
Redfall canonical-producer-input-hashes-red.log: gültiges10er-Metadatenmanifest wird
abgewiesen (1rot/7grün). Danach ausschließlich dessen sechs nupkg-Einträge ausgewählt,
alle acht Verhaltenstests und echter zuvor abgewiesener Produzentreport grün. Beide
Redquellstände archiviert. Tests laufen künftig in beiden vollen nativen Qualitätstoren.
YAML mit tatsächlichem Rubyparser auf Producerupload/needs-Graph geprüft, Exit0.
Keine Library-/Paketversions-/Consumerimplementierungsänderung.

Volltor linux/31cfc003e1ea48dc9c72ee8037ada854:1036C#Tests und zusätzliche acht
Hashprüferfälle bestanden,97.5796 %. Fullguard440Dateien/458Typen ohne Befund.
Native neue Consumerjobs und RequiredChecks folgen erst nach tatsächlichem Green.
Stundenbelege älterer Sourcehashes bleiben Checkpoints, kein Ersatz für neuen Lieferstand.
