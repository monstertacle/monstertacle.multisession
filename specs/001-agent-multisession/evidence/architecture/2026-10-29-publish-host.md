# Framework-dependent Publish und eigenständiger Host

Bestandsprüfung 2026-10-29T19:59:08Z gegen 06fd25f. Der Host hat zwei TFMs und
native Lease-Aliasse, aber keine deklarierten RuntimeIdentifiers. RecoveryProbeChild
findet seine eigene Assembly und benötigt keinen Quellbaum. Geprüfte SHA256:
Host.csproj ceaae6e4905bd4e11290e9ebdf8879db683f688b0b26be5cc5263085d9155c8a;
RecoveryProbeChild.cs c85b255ea4d82ff793b8752c805be218de31d3af5c5985c84bbeb85c8f93caa5;
Program.cs 0c611b044e2a027a0494de36840d4d4d4002119fb6592d43732641f572332791.

Entscheidung vor Konfigurationsänderung: linux-x64 und win-x64 für den Host
explizit deklarieren, Restore-Locks ohne Versionsupdates erneuern und anschließend
Standard-Locked-Restore prüfen. Framework-dependent Publish bleibt --self-contained
false; keine Single-File-/Trimmingänderung. Beide RID-Artefakte können lokal
gebaut werden, nur das native darf hier ausgeführt werden. Der veröffentlichte
Linux-Host muss embedded-consumer und zehn native Recovery-Kills außerhalb des
Quellbaums bestehen; anschließend übrige endliche nicht-Live-Szenarien prüfen.

Dies ist Buildkonfiguration/Regression vorhandener Semantik, kein künstlicher
fachlicher Red-Zyklus. Restore-/Compilerfehler wären Infrastrukturfehler. Die
C#-Probe aus docs/embedding.md wurde separat gegen lokale NuGets nur kompiliert
und gemessen: zwei Typen, keine Verstöße. Keine Herstelleranfrage wurde gestartet.
Native Windows-Publishausführung und Windows 11 bleiben eigene Abnahmen T099.

2026-10-29T20:01:48Z: Beide framework-dependent Publishes und Locked-Restore
lokal bestanden. Der Hostlock aktualisierte dabei bisher stale eigene
Project-Abhängigkeiten von 1.0.0 auf die tatsächliche Version 0.1.0. Die übrigen
Locks werden ebenfalls mechanisch neu bewertet; externe resolved-Versionen
müssen vor/nach identisch bleiben. RID-Einträge dürfen nur bereits vorhandene
SQLitePCLRaw-Versionen für die zwei deklarierten Architekturen festhalten.

Für T099 fehlen erfolgreiche Reports bisher die tatsächlich geladene Runtime-
und SQLite-Version. Vor Erweiterung gelesen: ValidationReport.Pass wird erst
nach erfolgreicher ausgewählter Probe aufgerufen; Fail/NotRun stehen vor I/O.
SHA256 ValidationReport.cs 16b1fd7abbbd9995c20d463e83c8b0de512c2fd6a95be22702ef25323feea870.
Entscheidung vor Umsetzung: ausschließlich PASS ergänzt OSDescription,
Environment.Version und die geladene SqliteConnection.SQLiteVersion. Kein
Native-Loaderzugriff für ungültige/unfreigegebene Auswahl. Zuerst bestehenden
Szenariotest auf tatsächliche Werte erweitern und fehlende Felder fachlich rot
beobachten; danach minimal ergänzen. Das erfasst eine operative Testumgebung,
keine Prompts, Credentials, Pfade oder sonstigen Hostdaten.
