# T088/T092 nativer Linuxbesitz, erster Schnitt

Bestandsprüfung2026-10-29T17:06:35Z an9f48f54: Linuxpaket enthält nurcsproj, keine
C#-Implementierung. IStateDirectoryLease SHA256
f692276aefbca22915854614e3f0cdc68ec0ed02fbd329c1dea64d66fe8fe389,
SqliteStoreFactory6fc19cd8798192725f0b0fdc4903f56cd2a67cf119be15a305a0067d8ddab706.
Factory verlangt gehaltene passende Lease, legt DB exklusiv über PrepareFileAsync an,
JournalmodeDELETE; SQLite übernimmt bei neuen Journaldateien die DBrechte. Runtime
übernimmt Lease erst nach erfolgreicher Vorbereitung und gibt sie nach Storecleanup frei.
Bisherige Testleasen sind ausdrücklich keine native Sicherheitsimplementierung.

Entscheidung vor Erweiterung: Linux-only öffentlicher Lease mit explizitem CreateNew/
OpenExisting. Nur absoluten bereits lexikalisch kanonischen Pfad akzeptieren, Eltern
müssen existieren. Jede Komponente relativ zu einem gehaltenen Directory-FD mit
openat(O_DIRECTORY|O_NOFOLLOW|O_CLOEXEC) öffnen; keine symbolischen Links normalisieren.
Nur neues Blatt mkdirat0700. Bestehendes Blatt auf effektive UID und exakt0700 prüfen,
niemals fremde Rechte reparieren. Native statx über feste256ByteABI statt architektur-
abhängigem statstruct, nur bestätigte Maskenfelder verwenden. fstatfs mit enger lokaler
Dateisystemallowlist; unbekannte/entfernte Fähigkeiten ablehnen. Ein stabiler0600Lockinode
mitflock(LOCK_EX|LOCK_NB), nie nach Freigabe löschen. Lock-/Datenfile nur regulär,
UID/exakt0600/einHardlink; CreateNew darf nie überschreiben. Kein Prozess-globales umask.
Directory-/Lockidentität vor jeder Nutzung erneut prüfen; privater Monitor serialisiert
Dispose und Handleoperationen. Keine Common-Sitzungslogik und keine zusätzlichen Pakete.

Native Quellen: https://man7.org/linux/man-pages/man2/open.2.html ,
https://www.man7.org/linux/man-pages/man2/statx.2.html ,
https://man7.org/linux/man-pages/man2/flock.2.html und lokale Linux-UAPIheader
/usr/include/linux/stat.h sowie /usr/include/asm-generic/fcntl.h .

Testplan zuerst echteVerhaltensfarbe: neues0700Verzeichnis/0600File, exklusiverZweitbesitz,
Wiedererwerb ohneLöschen, falscheRechte unverändert ablehnen, Symlinkblatt/-eltern und
Traversal/relativePfade ablehnen, keineFileüberschreibung, unzulässigeFilenamen,
Disposeverhalten und Pfadtausch. Plattformfremde Aufrufe liefernUnsupportedPlatform.
Danach eigenerProzess/Kill/Recovery/Journalrechte nachT093; dieser ersteSchnitt beweist
noch keine vollständige Alias-/Mount-/Fremdidentitätsmatrix und bleibtT088/T092offen.
