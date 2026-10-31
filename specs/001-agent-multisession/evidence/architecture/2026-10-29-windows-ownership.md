# T087/T091 nativer Windowsbesitz

Bestandsprüfung2026-10-29T17:23:45Z an1716d26. Windowsprojekt enthält weiterhinnurcsproj,
SHA25617974443b4772253294d7512cf35ab1448c3ef19b5ced1f737993e6aebf2b78a.
Plattformtestprojekt aa5e0daeef16beac612d7550d78429045b8f1dfefc455297d7b0e6e6305592c9.
Linuxport bietet bereits expliziteCreateNew/OpenExisting undIStateDirectoryLease.
Common/Sqlite erwarten gehaltene passendeLease undprivateDateivorbereitung; keine
Windowsersatzimplementierung vorhanden. BestehendePrivateStoreDirectory ist ein
ACL-Testfixture, ohne Reparse-/Alias-/Lebenszeitbesitznachweis.

Entscheidung vor Erweiterung: gleiches öffentlichesLeasemuster, Windowsx64-only.
Lokaler absoluter kanonischer DOS-Pfad, keineUNC-/Device-/ADS-/Dot-/Shortpathaliase;
NTFS/ReFS auflokalemFixedDrive. NativeCreateFileW mitBACKUP_SEMANTICS und
OPEN_REPARSE_POINT öffnetjedeKomponente; Reparseattributeablehnen, alleElternhandles
ohneFILE_SHARE_DELETE bisLeaseendehalten, damitPfade nichtumgebogenwerden.
CreateDirectoryW mitprivatemSecurityDescriptor beiderErzeugung, keineExistenzreparatur.
PrivatergeschützterDACL nuraktuelleSID FullControl, Eigentümergleich; vorhandeneACL
unverändertprüfen. GetFinalPathNameByHandle gegenOriginalpfadOrdinalIgnoreCase,
GetFileInformationByHandleEx fürTyp/Hardlinks. StabileprivateLockdatei mitShareNone,
nieLöschen. BereitsbelegterLock liefertStoreAlreadyOwned. FileCreateNew exklusiv,
OpenExisting nieReparatur; DateiennurOwnerSID, keineHardlinks/Reparsepoints.
GetFileSecurityW istpfadbasiert, dahererstnachvollständigerHandlepinningkette; nicht
GetSecurityInfo mit8Parametern undkein Aufweichen desCodemetr-7Parameterlimits.
Sperren/Dispose serialisieren, Common-Sitzungslogikbleibtunverändert.

Primärquellen: MicrosoftLearn CreateFileW (Sharemode/Attributezugriff/Reparsepoint/
Directoryflags), CreateDirectoryW (SecurityAttributes), GetFileSecurityW,
GetFinalPathNameByHandleW undGetFileInformationByHandleEx.
https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-createfilew
https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-createdirectoryw
https://learn.microsoft.com/en-us/windows/win32/api/securitybaseapi/nf-securitybaseapi-getfilesecurityw
https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-getfinalpathnamebyhandlew

Testplan zuerst: tatsächlicherWindows-CI-Red fürprivateDirectory-/FileACL, exklusiven
Zweitbesitz/Wiedererwerb, vorhandeneDaten unverändert, breiteACL abweisen, Dispose.
Wrong-platformProbe separat unterLinux/net10.0-windows ausführen; kein No-op.
WeitereReparse-/Alias-/Zweitprozess-/Kill-/SQLitejournalfälle folgen innerhalbT087/T093.
Windows-latest ersetztkeineWindows11-25H2-Abnahme; keineTaskvollabnahme alleinvomCI.

Guardvorprüfung ergab: #ifWINDOWS imneuenTestfile wirdvomStandalone-Codemetr unterLinux
nichtgemessen. VorweitererImplementierung stattdessen explizite nativeCompile-Items im
Testprojekt verwenden undganzeKlassen ohnePräprozessor kapseln; Codemetr misstweiterhin
alleeigenenDateien auchaußerhalbderaktivenTFM. BestehendeLinuxbesitztests entsprechend
mechanischumstellen, nach26/26grünemAusgang. KeineCodemetr-/Coverage-Scopeausnahme,
keinneuerSkip; Windows-/Linuxpaketzuordnung imTFM bleibtunverändert.

Implementierungswahl im gleichen Pfadvertrag: ACL-Lesen über vorhandeneBCL-
FileSystemAclExtensions/GetAccessControl statt eigenemGetFileSecurityW-PInvoke.
Diese pfadbasiertenBCLzugriffe erfolgen ebenfalls ausschließlichnach vollständigem
Handlepinning; keine eigeneInteropdeklarationmitmehrals7Parametern. PrivateSDs werden
alsgeschützterOwner-SID-only Descriptor beiCreateDirectory/CreateFile übergeben.
Directory-DACL benötigtContainer/Objectinheritance fürprivateSQLitejournals; vorhandene
File-DACL darfdieseprivateSID-Regel erben, ohne fremdeACE. KeinnachträglichesACLrepair.
WindowsTFM-Build bereitsgrün, nativeGreen erstnachCI. Linux-Wrongplatformprobe im
getrennten temporärenProjectReferencekonsumenten ist Regression, keinNuGet-/SC-011-Beleg.
