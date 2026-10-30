# Eingefrorener technischer Lieferkandidat

Abgleich2026-10-29T23:57:19Z. **Keine Produktfreigabe.**
Codecommit `fb1a16771a3ecd0e20bc6103c89c6f3ed2a65b71`,
Sourcehash `d51bf9f9efd33341e34037a6a027cac887abb117111fa6afa2d56d66d6d37cbf`.
[Entwurfs-PR1](https://github.com/monstertacle/monstertacle.multisession/pull/1).
Spätere reine Belegcommits verändern weder diesen Sourceumfang noch die Auswahl
der bereits gebauten Archive. Ein neuer Build wird nicht still als byteidentisch
mit dem ausgewählten Paketartefakt ausgegeben.

| Prüfung | Tatsächlicher Nachweis |
| --- | --- |
| Ubuntu24.04.5LTS x64 |1036Tests,2943/3016Produktzeilen=97,5796 %,14publizierte Szenarien |
| WindowsServer2025 x64 |1042Tests,3004/3080Produktzeilen=97,5325 %,14publizierte Szenarien; tatsächliches Runnerimage windows-2025-vs2026 |
| Beide Qualitätsläufe | [quality40974541005](https://github.com/monstertacle/monstertacle.multisession/actions/runs/40974541005), native TRX/Coverage/Audit/Host-/Consumerzusammenfassung [build-native.json](build-native.json) |
| PR-Gegenprüfung | [quality40974638685](https://github.com/monstertacle/monstertacle.multisession/actions/runs/40974638685) und Guard40974638699 ebenfalls erfolgreich |
| Strukturguard | [40974541014](https://github.com/monstertacle/monstertacle.multisession/actions/runs/40974541014),442Dateien/459Typen,0Regelverstöße/Parsefehler/Skips; semantische Grenzen codemetr-results.md |
| Bekannte Advisories | Keine direkten/transitiven Meldungen im Elfprojektgraphen; zusätzlicher [Audit40975671555](https://github.com/monstertacle/monstertacle.multisession/actions/runs/40975671555) erfolgreich |
| Kanonische Pakete | Genau sechs Archive des Linuxproduzenten aus40974541005, dieselben sechs File/SHA256 bei beiden nativen Konsumenten: [canonical-package-final.json](canonical-package-final.json); je2Sessions/5Requests/5Reopens |
| Laufzeiten | SDK10.0.403, Runtime10.0.14, native SQLite3.53.3 |
| Native Mountprobe | [linux-mount-final.json](linux-mount-final.json), genau das gewählte Linuxarchiv auf Debian13; echter Bindalias/ramfs, ergänzend zur Ubuntu-Suite |
| Stunden | Workflow40974541000 läuft separat auf Ubuntu und Server. Kein vorzeitiges PASS; endgültiger Roh-/SQLabgleich wird nach tatsächlichem Ende ergänzt |
| Windows11 25H2 | NOT RUN, vorhandener Serverrunner ersetzt keinen Clienthost |

Alle Mindest-Coverageschwellen bleiben87 %, alle ursprünglichen Fristen/Assertions
unverändert. Die jeweils fremde Plattform ist im anderen nativen Bericht geprüft;
Tests/Host sind kein Produktnenner. Paket-/Host-/Projektquellumfang ist ausgewiesen.

T125 ist behoben: tatsächliche neue Kernelassertions waren auf Windows vor Green
2rot/35grün; danach600nativeKills/Reopens und1200positive Kernelsignalprüfungen,
0verfrühte Rückkehr. Main-/PR-Gesamtprüfungen einschließlich37Plattformfällen grün.
[Red](windows-process-exit-regression-red.json), [Green](windows-process-exit-green.json).

T126 ist abgeglichen: alle405Integrationsfälle unverändert auf beiden OS grün,
tatsächliche TRX-Timeline max1konkurrierende Klasse/Fall statt vorhermax4.
Produktparallelität/Races bleiben innerhalb der Szenarien geprüft.
[native-io-before.json](native-io-before.json), [Linux](native-io-linux-after.json),
[Windows](native-io-windows-after.json). Keine allgemeine Flakefreiheitsbehauptung
oder nachträgliche Ursachenerfindung für Hardware-/Cleanupdetails.

Die vollständige FR-/SC-/Q-Matrix bleibt [release.md](release.md). Konkrete noch offene
Owner-/Folgeaufträge stehen in [remaining-acceptance.md](remaining-acceptance.md):
Win11, Livefreigabe/-konten, fremder Integrator, eindeutiger Gemini-Kontextfehler,
historische Q06-Archivbewertung, Distributionsentscheidungen und Schlussreview.
Kein NuGet-Push, Merge, Deployment oder SharpClaw-Umbau.

