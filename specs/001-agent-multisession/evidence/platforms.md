# Native Plattformnachweise

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
