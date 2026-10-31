# Tatsächlicher Stundenlauf und verbleibende Zielsystemabnahme

## Eingefrorener Lieferstand: beide verfügbaren nativen Stunden bestanden

Unabhängig geprüft 2026-10-30T00:46:42Z. Codecommit fb1a167,
Source d51bf9f9efd33341e34037a6a027cac887abb117111fa6afa2d56d66d6d37cbf.
[native-hour-soak40974541000](https://github.com/monstertacle/monstertacle.multisession/actions/runs/40974541000)
erfolgreich auf Ubuntu24.04 und WindowsServer2025; vollständiger Rohproof mit allen
Original-IDs/Receipt-Offsets/Hostdigests und Readonly-SQLzahlen in [hour-final.json](hour-final.json).

| Plattform | Monotone TimeSpan-Ticks | Sekunden | Originale / Minuten / Reopens |
| --- | --- | --- | --- |
| Ubuntu | 36000000886 | 3600.0000886 | 60 / 60 / 60 |
| Windows Server | 36000067907 | 3600.0067907 | 60 / 60 / 60 |

Beide Rohkandidaten haben exakt den ausgewählten Sourcehash. Die jeweilige
Hostassembly SHA256 stimmt zusätzlich mit dem endlichen nativen 14-Szenarienlauf
aus quality40974541005 überein: Linux
`d1e794191ab5d4caf4fc12411a922f1adb3e3168165396ed1e6cc84e81760f43`, Windows
`96147ac03783c554809ffa62af7e09131296f6fa1722ef97cc9fccec68021e30`.
eng/Test-SoakProof.ps1 prüfte die tatsächlich heruntergeladenen unveränderten
Assemblies, monotone Stunde und je ein Receipt in jedem Minutenfenster0..59.
Zusätzlicher externer PowerShell-/sqlite3-readonly-Abgleich bestätigte je60eindeutige
ursprüngliche IDs, alle Succeeded/ConfirmedEnded,60Results/Historyzeilen und0aktive
Reservierungen. Die60freigegebenen historischen Reservationszeilen bleiben erhalten.
Keine Dauer aus UTC-Buckets rekonstruiert; die Windows-UTC-Differenz ist bewusst
kein Dauerkriterium. Es gab keinen Neustart während der Stunde; danach kontrollierter
Stop und echte native Wiederöffnung unter denselben Originalkennungen.

Die Stunde läuft gegen den nativen publizierten Projekt-Host desselben Sourcehashes.
Der bytegenaue NuGetkonsum ist separat mit dem kanonischen Sechserartefakt aus
quality40974541005 auf beiden Plattformen geprüft. Fehlendes Windows11 bleibt
NOT RUN und sperrt T100/SC007/Q03 als vollständige Dreisystemabnahme. T124 kann
für die gemeinsame Source-/Paket-/verfügbare Stundenprüfung abgeschlossen werden;
kein Live-Provider oder kostenpflichtiger Zugriff war beteiligt. Ältere Prototypen,
Checkpoints und abgebrochene Quellstände darunter bleiben historische Teilbelege.

## Native Stundencheckpoints mit vollständigen Receipt-Rohdaten

Unabhängig geprüft 2026-10-29T22:51:14.7751785Z laut JSON-Messung: Workflow40954320848 bestand auf Ubuntu24.04
und WindowsServer2025. [hour-checkpoint.json](hour-checkpoint.json) enthält sämtliche
Original-IDs/monotonen Receipt-Offsets, Hostdigests, Originalzeiten und getrennt
readonly geprüfte SQLitegraphzahlen. Linux36000001186Ticks=3600.0001186s,
Windows36000148308Ticks=3600.0148308s; jeweils60 eindeutige Originalanfragen,
60 unterschiedliche Minutenfenster0..59,60 nach Wiederöffnung bestätigte Erfolge,
60 Result-/Historyzeilen und keine aktive Reservierung. Die60 freigegebenen
Reservationshistorienzeilen bleiben korrekt erhalten; Gesamtzeilenzahl ist kein
Maß für aktive Reservierungen.

Quellstand322c7a7c ist ein Checkpoint, kein Beleg für spätere Änderungen.
Prüfer eng/Test-SoakProof.ps1 gegen heruntergeladene unveränderte Hostassembly;
zusätzlicher externer BCL-PowerShell-/sqlite3-readonly-Abgleich unter
/home/user1/dev/.agenttemp/multisession-acceptance-checks.ps1. Ein erster
Hilfsskriptversuch interpretierte mehrzeiliges JSON falsch, ein zweiter zählte
freigegebene statt aktive Reservationszeilen. Beide korrigiert; dies sind
Prüfgerüstfehler, keine Produkt-Redfälle oder verschwiegene verlorene Requests.
Kein Herstellerzugang genutzt. T120 ist damit für das messbare Receiptformat
nachgewiesen; T100 verlangt weiter die vollständige finale Dreisystemmatrix.

## Älterer lokaler Prototyp

Belegstand 2026-10-29T20:43:15Z. Ein eingefrorener Zwischenstand bestand den
öffentlichen CLI-Aufruf `--scenario soak` auf dem lokalen Debian 13 x64.
Start 2026-10-29T19:38:57.204832Z, Ende 2026-10-29T20:38:57.2071284Z;
monotone Dauer 36000023019 TimeSpan-Ticks = 3600.0023019 Sekunden.
Die UTC-Werte stammen unverändert aus dem Rohbeleg und sind nur die lesbare
Zeitkoordinate, nicht die monotone Dauermessung.

Quellhash des eingefrorenen Buildstands:
`3a9fd0d19eae5743832fac534a4b198e038a22831a65ca6105a9e75748b92ad9`.
Hostassembly SHA256:
`582bbb5c72e79cf856c576a3e0c49b2924ee300f10e7c8738959170637d29d2a`.
Rohbeleg mit sämtlichen 60 Request-IDs: [soak-local-prototype.json](soak-local-prototype.json).
Original unter `/home/user1/dev/.agenttemp/multisession-soak-20261029-7HD2H3`.

Der Ablauf nahm 60 Requests in 60 monoton überwachten Minutenfenstern an,
stoppte kontrolliert und öffnete den nativen Store erneut. Jeder ursprüngliche
Request wurde über die öffentliche API als Succeeded/ConfirmedEnded gelesen.
Eine zusätzliche unabhängige Readonly-SQLite-Prüfung nach Prozessende bestätigt
60 eindeutige Original-IDs, 60 erfolgreiche bestätigte Ergebnisse, 60 Historyzeilen,
eine Sitzung und keine offene Reservierung. Die eingefrorene Assembly ist
unverändert. Befehle/Resultat: artifacts/tdd/check-soak-proof.ps1 und
soak-independent-green.log.

Grenze des Rohbelegs: Er enthält bisher keine einzelnen monotonen
Annahmeoffsets. Ein Versuch, Minutenfenster aus AcceptedUtc zu rekonstruieren,
war methodisch ungeeignet: Wanduhr und Transaktionszeitstempel sind kein
Ersatz für monotone Receiptmessung. Der verworfene Versuch bleibt unter
soak-utc-buckets-inapplicable.log sichtbar. Der Runtime-Zeitplan hat die
Receiptfenster geprüft; die zusätzliche unabhängige Prüfung beansprucht nur
Dauer, Identitäten, Ergebnisse und Reservationsfreiheit. T120 ergänzt
nachprüfbare monotone Stichproben je Receipt für alle folgenden Läufe.

Dies ist kein finaler Source-/Pakethash und kein Zielsystemnachweis für Ubuntu
24.04 oder Windows. T099/T100 verlangen denselben finalen Stand auf Ubuntu 24.04,
Windows Server 2025 und Windows 11 25H2; diese Stundenmatrix bleibt offen.
Kein Herstellerzugang oder kostenpflichtiger Aufruf war beteiligt.
