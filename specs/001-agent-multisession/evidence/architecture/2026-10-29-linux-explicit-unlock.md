# Linux: explizite Freigabe der Besitzsperre

Gemessen 2026-10-29T21:38:18Z. Basis1b2aba6 mit ergänzten Linux-Negativproben.
LinuxStateDirectoryLease.cs SHA25656906947d2461703581fac6915c1ed34a12c24fcfca8146911f8d95d759f0069.
LinuxOwnershipLock.cs ed87cc987ddf4483b5bf54b55c28c5cc733ccd39965ee1793b550970ae19d07f.

Im Gesamtgate74226641a1394e5f8b4d7bff0fe7610f scheitert einmal die unmittelbare
Wiederübernahme nach Dispose mit StoreAlreadyOwned; 29 weitere Plattformfälle bestehen.
Die neuen parallel gestarteten Fixtureprozesse machen eine verbleibende duplizierte
Open-File-Description plausibel. Der genaue fork-Zeitpunkt dieses einzelnen Laufs
ist nicht aufgezeichnet und wird nicht als erwiesen ausgegeben.

Quellprüfung: Acquire setzt korrekt O_CLOEXEC und LOCK_EX|LOCK_NB. Dispose schließt
nur die native Ownership-SafeHandle; es gibt kein LOCK_UN. Laut Linux-man-pages
[flock(2)](https://man7.org/linux/man-pages/man2/flock.2.html) teilen dup/fork dieselbe
Sperre; Freigabe erfolgt durch LOCK_UN oder erst nach Schließen sämtlicher Duplikate.
CLOEXEC verhindert Überleben nach exec, nicht die Existenz während des fork/exec-Fensters.
Der Lifetimevertrag braucht nach explizitem Dispose eine tatsächlich gelöste Sperre.

Vor Produktänderung deterministischer nativer Test: nur die eigene .multisession.lock
über /proc/self/fd auffinden, libc dup auf diese File-Description, Originallease disponieren
und bei weiterhin offenem Duplikat neu übernehmen. Kein Reflection/Testhook am Produkt.
Testinteroppart nutzt LibraryImport und aktiviert UnsafeBlocks ausschließlich im eigenen
Platformtestprojekt; Windows kompiliert den BCL-/Interopcode, führt diesen Linuxfall
aber nicht aus (bestehende TFM-Zuordnung ergänzen). Original und Duplikat im finally
schließen. Erst tatsächliches StoreAlreadyOwned als Red, nicht Compilerfehler, zählt.

Bei bestätigtem Red: LinuxOwnershipLock.Release führt LOCK_UN vor Close aus, schließt
auch im Fehlerfall und meldet diesen bereinigt. Dispose auf bereits geschlossener
Ownershiphandle darf keine inzwischen wiederverwendete Deskriptornummer anfassen;
Leasegate plus IsClosed-Prüfung sichern Idempotenz. Verzeichnishandle im finally schließen.
Keine Lockdateilöschung und keine Freigabe während Store-Nutzung; Runtime-Reihenfolge
(Storedispose vor Leasedispose) bleibt unverändert. Neuer Eigentümer muss nach zweitem
Dispose des alten weiterhin exklusiv sein. Voller Plattform-/Qualitygate und native CI.
