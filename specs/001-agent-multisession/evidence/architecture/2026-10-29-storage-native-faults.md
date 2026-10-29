# Native Read-only- und Disk-full-Proben

Ausgangf838da6. Quellenmessung2026-10-29T14:02:07Z:
StorageFailurePolicy.cs SHA256339fb6a3c9211628cb4f3bbd16f448154ff4bb9e7d5bba939e90bd303e6fbaa8;
SqliteTransactions.cs bcdff74394f836f5d1c8171183b2ec92df273353b996a2968fb6da362964c261;
StorageFailureTests.cs af615940ea8d2feaa43de7aedff352cf63784572b65829acfe0920f85f20e5e4.
T054/T065. Bestehende Tests behandeln echte Busy-Sperren, begrenzte DBqueue, Cancellation,
Commit-/Rollbackfehler und echte Prozesskills. Tatsächliche Betriebssystem-Schreibsperre
und voller kleiner Datenträger fehlen. StorageFailurePolicy bereinigt Nativefehler und
hält Unknown bei fehlendem Rollbacknachweis; keine neue Fachlogik ohne tatsächlichen Redfall.

## Primärquellen vor Umsetzung

- SQLite https://sqlite.org/c3ref/open.html: READWRITE darf bei OSschreibschutz auf Readonly
  zurückfallen; bloße Open-Mode-Konfiguration beweist keine Schreibfähigkeit.
- SQLite https://sqlite.org/rescode.html: READONLY8 für fehlendes Schreibrecht, FULL13 für
  fehlenden Platz bei DB-/Tempwrites; IOERR ist ein eigener Code. Nur bereinigte öffentliche
  Kategorie nach außen, keine erfundenen Exceptiontexte als Disk-full-Beleg.
- Linux https://man7.org/linux/man-pages/man1/unshare.1.html und
  https://man7.org/linux/man-pages/man7/mount_namespaces.7.html: eigene Mountnamespace mit
  privater Propagation isoliert Mountänderungen; keine Shared-Propagation verwenden.
- Linux https://man7.org/linux/man-pages/man5/tmpfs.5.html: size setzt endliche Kapazität,
  uid/gid/mode private Ablage; niemals Default50%-Kapazität oder globales/tmp benutzen.
- Windows https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/attach-vdisk
  und https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/assign:
  eigene VHD fokussieren, privater Mountpfad, keine System-/Bootvolumes verändern.

## Entwurf vor Test-/Hosterweiterung

Read-only: echte neue SQLiteverbindung nach vollständigem Schließen aller alten Handles,
OSschreibschutz durch400 beziehungsweise ReadOnlyattribut; ein direkter neuer Schreibhandle
muss bereits scheitern. Vorhandenen kooperativen CrashStateDirectoryLease für ausschließlich
existierende Datei wiederverwenden, weil strikte PrivateStoreDirectory-600-Prüfung sonst
bewusst vor SQLite abweist. Diese Brücke behauptet keine native Ownership-/Rechteabnahme.
Nach Versuch Modus exakt wiederherstellen, DBhash und vorheriger Ergebnis-/Historygraph
unverändert. Factory/Start oder Annahme darf nur bereinigt fehlschlagen, keine Quittung.
Zuerst tatsächliche Farbe beobachten; bereits grüne Schutzlogik bleibt zusätzliche
Regression, kein künstlicher Fehler für TDD. Produktlücke erst nach wirklichem Red beheben.

Disk-full: separater markierter Kindprozess/kleines eigenes Dateisystem mit harten Grenzen,
keine Befüllung existierender Volumes. Linux private Mountnamespace ist lokal per
sudo-n-unshare erfolgreich sondiert; spätere eigentliche Fixture nur im neuen markierten
.agenttemp-Root. Windows eigene eindeutig adressierte VHD und Mountdirectory, niemals
selectdisk auf existierenden Datenträgern. Füller auch unabhängig vom Mount auf maximal128MiB
begrenzt, tatsächliches ENOSPC/ERROR_DISK_FULL vor SQLiteversuch belegen. Parent/Childnonce,
endliche Prozessfristen und Cleanup aller selbst angelegten Ressourcen. Vorherigen Erfolg
sichern, unter vollem Volume neue Annahme versuchen, nach Freigabe Füller alte Daten und
fehlende neue Anfrage prüfen, null zusätzliche Sends. Keine Produktbackdoor/Reflection oder
falsches FULL durch Triggertext. Privileg-/Zielhostfehler sind NOT RUN/Fehler, niemals PASS;
native Rechte-/Lockabnahme bleibtUS4. Auf Windows lokale Ausführung hier nicht behaupten.

Weitere gemessene Sonden vor Disk-full-Code: rootloses unshare mit User-/Mountnamespace
und privater Propagation funktioniert lokal; tmpfs1MiB meldet stat sowie .NETDriveInfo
exakt1048576Bytes. Deshalb Linux ohne Maschinenroot und ohne Änderung fremder Mounts.
Worker prüft Volumekapazität vor Füller zusätzlich auf maximal128MiB. Windows erhält
statt eines nicht zuverlässig von DriveInfo auflösbaren Verzeichnismounts einen freien
temporären Laufwerksbuchstaben; Diskpart assign scheitert bei Kollision, danach eigene
Volumegröße und eigener Marker verifizieren. Nur neue VHDdatei im privaten Testroot
fokussieren/abtrennen, keine vorhandene Disk auswählen. Quelle für Sequenz attach→create
partition→format→assign: MicrosoftLearn
https://learn.microsoft.com/en-us/windows-hardware/manufacture/desktop/boot-to-vhd--native-boot--add-a-virtual-hard-disk-to-the-boot-menu?view=windows-11
und assign-Syntax oben. Scriptdateien in UTF16 für Windows, Prozessargumente strukturiert.

## Native CI-Fixturekorrektur vor Umsetzung

Gemessen2026-10-29T14:46:42Z, Ausgang33b262a. DiskFullHarness SHA256
0056f1b2f783bdb290c1f625be56f78b74bdcc8ff1486d62666cb1fa729e2ebd,
DiskFullWindowsVolume d969346707cc45b591d95d507be5e91a94473e7097ada70b6a546d4bf4db82f7.
Qualitätslauf40893625049: Ubuntu uid_map EPERM vor Workerstart; Windows VHDpreparation
Exit!=0 ohne bisher ausreichende Diagnose. Produktgraph unverändert, kein fachlicher Red.
Fixtureentscheidung: zuerst nebenwirkungsfreie rootlose Namespaceprobe; wenn nicht verfügbar,
sudo-n mit harter äußerer50sFrist, private Mountnamespace, tmpfs uid/gid des ursprünglichen
Benutzers; vor Worker exec setpriv reuid/regid/clear-groups. Kein globales sysctl/AppArmorändern,
kein Worker mit Rootrechten, kein Mount im Hostnamespace. Fehlende sudo/setpriv bleiben Fehler.
Gezielte lokale Probe muss auch diesen Pfad tatsächlich ausführen.
Windows Script ohne BOM als ASCII und CRLF, da offizielles MicrosoftSupport-Beispiel
https://support.microsoft.com/it-it/servicing/os/windows/2024/02/kb5035679-instructions-to-run-a-script-to-resize-the-recovery-partition-to-install-a-winre-update
Diskpart-Dateien mit Out-File-Encoding-ascii schreibt. Nicht darstellbare Pfade explizit
abweisen statt ersetzen. Bei Fehler begrenzte stdout/stderr der eigenen Diskpartprobe zeigen.
MicrosoftLearn diskpart-scripts-and-examples fordert15s zwischen zwei Scriptaufrufen;
diese Pause gilt auch vor dem eigenen Detach. Native CI erneut nötig; kein WindowsPASS lokal.

Weitere native Messung2026-10-29T14:54:05Z, Lauf40895383898: Ubuntu vollständig grün.
Windows erstellt/attached VHDX und Partition erfolgreich, formatNTFS mit erzwungenem
4096Bytecluster meldet parameterincorrect. Konkrete Rootcause zwischen Größe/Sektor-
und Clusterkombination hier nicht lokal messbar. Nächste begrenzte Fixturefassung nutzt
64MiB-VHD mit512Bytesektoren und überlässt NTFS die Wahl seiner Defaultclustergröße;
keine Kapazitätsgrenze aufweiten. Primärbeleg für VHD512/VHDX4096physicaldefaults:
https://learn.microsoft.com/en-us/windows-server/administration/performance-tuning/role/hyper-v-server/storage-io-performance .
Format-Defaults: https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/format .
Gleiche native Testabnahme bleibt zwingend; Formatversagen bleibt Fehler.

Lauf40896833128 nach b80e47c: VHD64MiB wird ebenfalls erfolgreich erstellt/attached,
FormatNTFS-default erneut parameterincorrect. Gemessener Quellen-/Logabruf2026-10-29T15:03:48Z,
artifacts/tdd/storage-vhd-ci-failure.log. Sektorhypothese reicht als Erklärung nicht aus.
Nächste isolierte Sondierung128MiB innerhalb bestehender harter128MiBGrenze; diskpart
filesystems vor Format liefert auf dem echten Host die Formatmöglichkeiten. Kein Fallenlassen
von NTFS-ACL, kein Skip und kein Weiten des Füllerlimits. Wenn weiterhin Fehler, Diagnose
am Host fortsetzen; dieser Zustand ist keine native Abnahme.

## Echte Hostdiagnose statt weiterer Größenhypothesen

Ausgang1366585, letzter nativer Lauf40899122477 zu903338d:128MiB scheitert ebenfalls
mit parameterincorrect, obwohl filesystems NTFS mit512/1024/2048/4096/... als unterstützt
meldet. Außerdem zusätzliche5sAdmission-/Stopfehler anderer parallel laufender SQLitefälle.
Keine sichere Ursache bewiesen. Der Volumeformat-/Disk-full-Test beansprucht globale
Windows-Diskmanagement- und physische Flushressourcen; xUnitcollection mit explizitem
DisableParallelization verhindert gleichzeitige andere Tests, ändert keine Produktfrist.

Für nächsten Windowsfehler führt CI nach dem beendeten Tor eine diagnostische eigene
64/128/256/512MiB-VHDformatmatrix aus: nur frisch erzeugte eindeutige Images, kein Befüllen,
niemals selectdisk/Format existierender Volumes; feste Prozessfristen,15sDiskpartabstände,
Cleanup fokussiert ausschließlich eigenes zuvor nachweislich fehlendes Image. Private
Sonden unter dev/.agenttemp, nur JSON/Textberichte nach artifacts. Jeder Formatfehler ist
als Fehler/Exitcode im Bericht sichtbar, kein Ersetzen des roten Qualitätslaufs durch eine
Diagnose. Kein globaler Sysctl-/ACL-/Quotaeingriff, kein Aufweichen des128MiB-Füllerlimits.
Zunächst hier nur Syntax-/Linuxregression, tatsächliche Matrix ausschließlich nativ.
Quellen vor Änderung gemessen2026-10-29T15:19:18Z: ci.yml
4fb4450715c8f19dd83336573b06489c3f30a5cbe9c76f47255da035ac6ea036,
DiskFullStorageTests6dc440cfef4bb20cd7f725f63da3c46e5483286cca118c59702c6914b75e8a1c,
DiskFullWindowsVolumea963d7f2de2bc7a2da67725daae3382bdb9940c56568e2b3d34df50975299a2d.
