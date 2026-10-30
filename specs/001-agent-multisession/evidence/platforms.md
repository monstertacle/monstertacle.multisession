# Native Plattformnachweise

Aktueller technischer Kandidat nach T125/T126: **fb1a167 / Source d51bf9f9...**,
native Gesamt-/Paket-/Guard-/Auditprüfungen erfolgreich. Verbindlicher aktueller
Index mit vollständigem Sourcehash, ausgewähltem Paketartefakt, Laufnummern und
Abnahmegrenzen: [delivery-candidate.md](delivery-candidate.md),
[build-native.json](build-native.json), [canonical-package-final.json](canonical-package-final.json).
Die älteren Zahlen/Quellstände in den folgenden Abschnitten bleiben historische
Checkpoints; sie ersetzen diesen aktuellen Bericht nicht. Die reale finale Stunde
und fehlenden externen Freigaben bleiben separat ausgewiesen.

## Aktueller Nachweis nach ABIrefactoring

Abgleich2026-10-29T23:00:40Z: Kandidat7489450/Source dba419f35bf40fd7282a5b2f3bfe7c685576e2c62ceb570d5fec2d0f4f84498b.
Quality40968636580:Ubuntu24.04.5LTS und WindowsServer2025 erfolgreich,
je14 framework-dependent publizierte Szenarien außerhalb des Quellbaums,
Runtime10.0.14/native SQLite3.53.3. Genaue Rohdaten/Hashes build-native.json;
Windows meldet OSDescription MicrosoftWindows10.0.26100, Runnerimage laut
Workflow WindowsServer2025, daraus wird kein Windows11-Host abgeleitet.
31Linux-/37Windowsplattformfälle, einschließlich fremder UID, Hardlinks,
echtem Zweitprozess, Kill/Recovery/Journal und dup-/idempotentem Dispose.

Zusätzliche echte native Bindalias-/ramfsprobe auf Debian13 mit genau dem finalen
kanonischen Linux-NuGet: [linux-mount-final.json](linux-mount-final.json).
Frischer leerer Cache, dann Locked Restore, Build0Warnungen/0Fehler;
worker als UID1000 im privaten Mountnamespace. Bindalias bleibt exklusiv und
nach Dispose wieder erwerbbar, doppelte alte Freigabe erhält neuen Besitzer;
unbekanntes korrekt privates ramfs wird vor Lock/Mutation abgewiesen.
Netzwerk-FS-Magics NFS/CIFS/SMB2/9p/Ceph gegen aktuelle Header und tatsächlich
verwendete geschlossene Allowlist geprüft. Kein echter NFS-/SMBserver behauptet.
Diese konkrete Negativmatrix ersetzt die frühere pauschale offene Linux-Mountfrage;
keine Zusage, unbekannte Dateisysteme zu unterstützen.

T088/T092 und Windowsimplementierung T091 sind damit technisch belegt.
T087/T093/T099 bleiben wegen fehlendem Windows11 offen; T100 wegen vollständiger
finaler Dreisystemstunde. Zusätzlich offener intermittierender Server-Recovery-
Besitzbefund T125: review.md/remaining-acceptance.md, native Diagnose separat.
Einzelne erfolgreiche SC009-Läufe werden nicht als dessen Ursachenklärung verkauft.
Der folgende Teil ist historisch, einschließlich damals noch laufender Stunden.

## Historische Zwischenstände

Abgleich 2026-10-29T21:21:56Z. Owner: ausführende Gesamtimplementierungssitzung.
T087/T088/T091/T092/T093/T099/T100 bleiben bis zu ihren vollständigen Kriterien offen.

| Umgebung | Tatsächlich geprüfter Umfang | Grenze |
| --- | --- | --- |
| Ubuntu 24.04 x64, GitHub Actions | Native Linuxlease, private Modi, kanonische Pfade, Datei-/Verzeichnissymlinks, ersetzte Besitzobjekte, echter zweiter Prozess, Kill/Wiedererwerb, privates SQLitejournal, Unknownreservation und 10 native Recoveryabbrüche; vollständiger Paket-/Publishkonsum | Vollständige Fremd-UID-/Netzwerkdateisystemmatrix separat abgleichen; Stundenlauf läuft |
| Windows Server 2025 x64, GitHub Actions windows-latest | Native Windowslease, private User-DACL, breite Datei-/Root-ACL abgewiesen, UNC/ADS/relative/reservierte Pfade, Junction/Hardlink, Lifetime-Rename, zweiter Prozess, Kill/Wiedererwerb, SQLite-/Journalrechte, 10 Recoveryabbrüche; vollständiger Paket-/Publishkonsum | Kein Windows-11-Beleg; Stundenlauf läuft |
| Debian 13.7 x64, lokaler Host | Vollständiger nativer Linuxgate, echte Lease-/Crash-/Diskfullproben und erster tatsächlicher Stunden-Prototyp | Weder als Ubuntu noch als Windows ausgegeben; Prototypstand siehe soak.md |
| Windows 11 25H2 x64 | NOT RUN | Kein entsprechender Testhost vorhanden; Serverrunner ersetzt ihn nicht |

Letzter vollständig grüner nativer Checkpoint: Commit 7e33651,
[quality 40954320723](https://github.com/monstertacle/monstertacle.multisession/actions/runs/40954320723),
[Guard 40954320725](https://github.com/monstertacle/monstertacle.multisession/actions/runs/40954320725).
Dieser Stand enthält 27 Linux- und 37 Windows-Plattformfälle. Die 30 neuen
Windows-Negativfälle sind unmittelbare Regressionen bereits implementierten Verhaltens;
sie werden nicht rückwirkend als neue Produkt-TDD ausgegeben. Der zuvor rote Junction-
Cleanup in 40951619323 wurde durch explizites nichtrekursives Entfernen des eigenen
Junctionfixtures korrigiert; die aktuelle Matrix besteht danach vollständig.

Aktuelle tatsächliche Stundenläufe: native-hour-soak 40954320848. Beginn des
Stundenschritts laut GitHub: Linux 2026-10-29T21:05:08Z, Windows 2026-10-29T21:09:19Z.
Noch kein PASS behauptet. Nach der Stopcallback-Produktänderung sind diese Läufe
Checkpoints; eine finale identische Source-/Paketmatrix benötigt neue Läufe.
Rohartefakte, Sourcehash und methodische Grenzen gehören in ci.md beziehungsweise soak.md.
