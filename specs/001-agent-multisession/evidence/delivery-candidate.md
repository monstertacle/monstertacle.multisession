# Eingefrorener technischer Lieferkandidat

Abgleich 2026-10-30T00:46:42Z. **Keine Produktfreigabe.**
Codecommit `fb1a16771a3ecd0e20bc6103c89c6f3ed2a65b71`,
Sourcehash `d51bf9f9efd33341e34037a6a027cac887abb117111fa6afa2d56d66d6d37cbf`.
[Entwurfs-PR1](https://github.com/monstertacle/monstertacle.multisession/pull/1).
Spätere reine Belegcommits verändern weder diesen Sourceumfang noch die Auswahl
der bereits gebauten Archive. Ein neuer Build wird nicht still als byteidentisch
mit dem ausgewählten Paketartefakt ausgegeben.

| Prüfung | Tatsächlicher Nachweis |
| --- | --- |
| Ubuntu 24.04.5 LTS x64 |1.036 Tests, 2.943/3.016 Produktzeilen = 97,5796 %, 14 publizierte Szenarien |
| Windows Server 2025 x64 |1.042 Tests, 3.004/3.080 Produktzeilen = 97,5325 %, 14 publizierte Szenarien; tatsächliches Runnerimage windows-2025-vs2026 |
| Beide Qualitätsläufe | [quality40974541005](https://github.com/monstertacle/monstertacle.multisession/actions/runs/40974541005), native TRX/Coverage/Audit/Host-/Consumerzusammenfassung [build-native.json](build-native.json) |
| PR-Gegenprüfung | [quality40974638685](https://github.com/monstertacle/monstertacle.multisession/actions/runs/40974638685) und Guard40974638699 ebenfalls erfolgreich |
| Strukturguard | [40974541014](https://github.com/monstertacle/monstertacle.multisession/actions/runs/40974541014),442 Dateien/459 Typen, 0 Regelverstöße/Parsefehler/Skips; semantische Grenzen codemetr-results.md |
| Bekannte Advisories | Keine direkten/transitiven Meldungen im Graphen aller elf Projekte; zusätzlicher [Audit40975671555](https://github.com/monstertacle/monstertacle.multisession/actions/runs/40975671555) erfolgreich |
| Kanonische Pakete | Genau sechs Archive des Linuxproduzenten aus40974541005, dieselben sechs File/SHA256 bei beiden nativen Konsumenten: [canonical-package-final.json](canonical-package-final.json); je zwei Sitzungen/fünf Requests/fünf Reopens |
| Laufzeiten | SDK 10.0.403, Runtime 10.0.14, native SQLite 3.53.3 |
| Native Mountprobe | [linux-mount-final.json](linux-mount-final.json), genau das gewählte Linuxarchiv auf Debian 13; echter Bindalias/ramfs, ergänzend zur Ubuntu-Suite |
| Stunden | [40974541000](https://github.com/monstertacle/monstertacle.multisession/actions/runs/40974541000) auf beiden OS erfolgreich; je 60 originale Requests in 60 monotonen Minutenfenstern und 60 nach Wiederöffnung bestätigte Erfolge, keine aktive Reservierung. Unabhängiger Roh-/Host-/SQLabgleich: [hour-final.json](hour-final.json) |
| Windows 11 25H2 | NOT RUN, vorhandener Serverrunner ersetzt keinen Clienthost |

Alle Mindest-Coverageschwellen bleiben 87 %, alle ursprünglichen Fristen/Assertions
unverändert. Die jeweils fremde Plattform ist im anderen nativen Bericht geprüft;
Tests/Host sind kein Produktnenner. Paket-/Host-/Projektquellumfang ist ausgewiesen.

T125 ist behoben: tatsächliche neue Kernelassertions waren auf Windows vor Green
2 rot/35 grün; danach600 native Kills/Reopens und 1.200 positive Kernelsignalprüfungen,
0 verfrühte Rückkehr. Main-/PR-Gesamtprüfungen einschließlich37 Plattformfällen grün.
[Red](windows-process-exit-regression-red.json), [Green](windows-process-exit-green.json).

T126 ist abgeglichen: alle 405 Integrationsfälle unverändert auf beiden OS grün,
tatsächliche TRX-Timeline maximal eine konkurrierende Klasse/Fall statt vorher maximal vier.
Produktparallelität/Races bleiben innerhalb der Szenarien geprüft.
[native-io-before.json](native-io-before.json), [Linux](native-io-linux-after.json),
[Windows](native-io-windows-after.json). Keine allgemeine Flakefreiheitsbehauptung
oder nachträgliche Ursachenerfindung für Hardware-/Cleanupdetails.

Die vollständige FR-/SC-/Q-Matrix bleibt [release.md](release.md). Konkrete noch offene
Owner-/Folgeaufträge stehen in [remaining-acceptance.md](remaining-acceptance.md):
Win11, Livefreigabe/-konten, fremder Integrator, eindeutiger Gemini-Kontextfehler,
historische Q06-Archivbewertung, Distributionsentscheidungen und Schlussreview.
Kein NuGet-Push, Merge, Deployment oder SharpClaw-Umbau.

Ubuntu maß 3.600,0000886 Sekunden, Windows 3.600,0067907 Sekunden monoton.
Die jeweilige Hostassembly ist byteidentisch mit der vorherigen endlichen nativen
Szenarioprüfung; beide Kandidaten nennen denselben vollständigen Sourcehash.
Die Stunde verwendet den nativen publizierten Projekt-Host. Der Konsum exakt der
gewählten NuGetarchive ist separat durch die beiden kanonischen Consumerjobs belegt.
UTC ist die lesbare Zeitkoordinate; Dauer und Receiptfenster werden monoton geprüft.
T124 ist damit abgeschlossen. T100/SC-007 bleiben wegen des fehlenden Windows-11-
Nachweises für die vollständige Dreisystemmatrix offen.

