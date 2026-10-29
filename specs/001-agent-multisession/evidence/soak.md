# Tatsächlicher Stundenlauf und verbleibende Zielsystemabnahme

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
