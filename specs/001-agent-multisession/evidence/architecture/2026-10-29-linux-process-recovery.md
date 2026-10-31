# T088/T093 Prozessgrenze mit echtem Linuxlease und SQLite

Bestandsprüfung2026-10-29T17:14:59Z an075d4e2. Program SHA256
e0eba8da77ae983d0adbf71bf62ef6c44cb4c82dd36d365e029cc9184a5a11b0,
LinuxStateDirectoryLease56906947d2461703581fac6915c1ed34a12c24fcfca8146911f8d95d759f0069.
Program unterstützt bisher ausschließlich Crash-/Diskfulltestprotokolle; unbekannte
Argumente liefern ohne Signal0. Bestehender CrashHarness verwendet eine kooperative
Lease und ist kein nativer Zweitbesitznachweis. ValidationHostBuild findet den bereits
gebauten Worker; Wiederverwendung per Quelldateilink im Plattformtest ohne Produktbezug.

Entscheidung vor Erweiterung: separates begrenztes Testworkerprotokoll mit genauem
Argumentlayout, absolutem testpräfigiertem Root, nonce-Marker und expliziter Trustscope.
Worker öffnet vorhandene native Lease und SQLite, führt Startprüfung/Recovery aus,
signalisiert erst danach PID/Ergebnis. Kein neuer Store, kein Provider, kein externer
Prozess im Produktfluss. Prozess wartet mit offener Standardeingabe auf Elternprozess;
Kill im Test kontrolliert, Dispose des Harness beendet immer verbliebenes Kind.
Fehler enthalten nur öffentliche Kategorie; kein Rohtext/Prompt. Unter Windows nicht
als Linuxprobe ausführen. TDD: erwartetes strukturiertes Signal fehlt beim bestehenden
Host tatsächlich; erst dann Workerroute implementieren. Zwei native Szenarien:
Elternlease weist Kind mit StoreAlreadyOwned ab; Kind hält Store/Lease, Elternbesitz
abgewiesen, Kill, Wiedererwerb und unveränderte Unknownreservation/Kennungen/History.
Rechte der tatsächlich erzeugten DB ebenfalls prüfen. Journal während aktivem Write,
FremdUID-/Mountmatrix und Windows bleiben separate vorhandeneT088/T093-Abnahmepunkte.

Erweiterung vor Umsetzung: Der erste Prozess-/Killzyklus ist mit25/25Besitzregressionen
grün. Für T093 fehlt noch der private aktiveRollbackjournal. Die bestehende
CrashWorker-Journalsonde erzeugt bereits realen uncommitteten1MiBWrite; diese begrenzte
Testinfrastruktur nach grünem Ausgang in eine gemeinsam verwendete Disposable-Sonde
extrahieren. Linuxworker beginnt diese Sonde erst nach erfolgreichem Storestart, dann
Barriere. Vorher neues Red auf fehlendes Journal beobachten. Danach0600am tatsächlichen
Journal prüfen und Kill/Wiederöffnung mit vorhandener Unknownreservierung wiederholen.
Kein Produktcode-/SQLitepolicywechsel, kein Testhook imProdukt. Änderung dient einem
noch offenen T093-Abnahmepunkt, keine vollständige FremdUID-/Mountabnahme.
Messung dieses Nachtrags2026-10-29T17:18:10Z: LinuxOwnershipWorker SHA256
 ae45b51c88c06f0edc88759866c031551ab9aa59ff3e68f073ff89c25b0de226,
CrashWorker1479069bd2aff8a94cf8340b01695e03feff67e468d427b677cdaac7f03ffcf3.
