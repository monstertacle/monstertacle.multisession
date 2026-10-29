# SQLite-Recovery: ausgeführte Nachweise und offene Abnahme

Abgleich am2026-10-29T17:21:17Z. Historische Red-/Greenbefehle, Manifesthashes und
Grenzen bleiben in[tdd-us5.md](tdd-us5.md); dieser Bericht ordnet sie den aktuellen
Abnahmepunkten zu. Keine Exactly-once- oder automatische Migrationszusage.

| Probe | Tatsächlicher Nachweis | Grenze/Folgeauftrag |
| --- | --- | --- |
| SC-009 | PersistenceRecoveryTests mit zehn benannten Iterationen0..9: je zwei fertige Ergebnisse, drei Queued inA, ein Running inB, Limit2; echter Kill, A danach FIFO, B Interrupted/Unknown mit erhaltener Reservation und null Replay | Kontrollierte Lease; ursprüngliches Quellenmanifest20c112419300aef8237fbb48bd21ea629278822a6d8e6da9737d35af5eac94b7; vollständiger T055-Zugangs-/Modellaudit offen |
| Annahme-/Dispatchgrenzen | CrashBoundaryTests: vor/nach Annahme, nach Commit vor Wecksignal, nach Reservation, nach externer Annahme vor Operations-ID, vor/nach Abschlusscommit | Eigene markierte Testablagen; jedes Mal Originalkennungen, fertige Ergebnisse und Unknownbelegung prüfen; kein Hersteller-Livebeleg |
| Hotjournal | CrashJournalTests: echter uncommitteter Write und Prozesskill; unbekanntes Schema bleibt mit Journal unverändert | Native Rechte sind zusätzlicher T093-Auftrag |
| Linuxbesitz mit SQLite | OwnedStoreRecoveryTests: Zweitprozess abgewiesen, Kind hält native Lease und realen SQLitehotjournal, Kill, Wiedererwerb; DB/Journal0600, Unknownrequest, Dispatchfall, belegter Slot und History erhalten | Lokal Linuxx64; vollständige Zielsystem-/FremdUID-/Mountmatrix offen, T088/T093 |
| Aufbewahrung/Löschung | SessionRetentionPolicy, RetentionTests und RetentionTransactions: ausdrückliche Hostregel, ClosedUtc, keine aktive/ungewisse Arbeit oder offenen Fälle; inklusive Frist, graphweise Transaktion, Fehlerrollback, unabhängige Sitzung erhalten | Nur logische lokale Löschung; keine sichere physische, externe oder Backuplöschung |
| Speicherfehler | Read-only/Diskfull/BUSY/Queue-/Commitfehler und konsistenzwidrige Requests/Sessions/Submissions/Results/Reservations/Dispatches/Cases/Decisions abgewiesen, keine sichere Bestätigung ungeklärter Writes | T065-Gesamtaudit bleibt offen; echte Windows-/Linux-Diskfull-CI bereits grün, siehe tdd-us5.md |

Aktueller lokaler Gesamtlauf: `artifacts/quality/linux/ae3b46d115ea4d2199372bb286d4fef9/`,
Sourcehash `6fcd0afddbc352935e248c7e6497f392cb50d74c8237ce2f9aed0911451cb269`.
937/937Tests,0Fehler/Skips;352Unit/203Contract/356Integration/26Plattform.
Alle zehn SC-009-Fälle und sieben CrashBoundaryfälle darin erneut ausgeführt.
2804/2880Produktzeilen=97.3611%; Codemetr376Dateien/387Typen,0Befunde/Parsefehler/Skips.
Befehl: `run-capped.sh -- ... dotnet format ... && dotnet tool run codemetr-csharp -- check --format json && pwsh -NoProfile -File scripts/Invoke-Quality.ps1 -Platform linux`.
Letzter vollständig nativer CI-Stand davor075d4e2: Qualität40919459445 undGuard40919459473
beidseitiggrün; neuer Prozess-/Journalstand benötigt eigeneCI. VollständigeUS5-/T066-
Freigabe wird daraus nicht abgeleitet.

T064 nach aktuellem Code-/Testabgleich abgeschlossen. Offene umsetzbare Punkte bleiben
mitOwnerausführendeGesamtimplementierung in den vorhandenen konkreten AufgabenT049–T056,
T065/T066/T068/T069 undT088/T093; die jeweiligen Tasktexte liefern Auftrag, nächsteSchritte,
Abhängigkeiten undprüfbareAbnahme. T107 beurteilt historische TDDlücken; spätere grüne
Regressionen werden nicht nachträglich als ursprünglichesRed ausgegeben.
