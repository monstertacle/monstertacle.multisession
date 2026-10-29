# Tatsächlicher Konsum lokaler NuGets

Prüfstand dokumentiert am 2026-10-29T19:45:52Z. Befehl:
`bash /home/user1/dev/scripts/run-capped.sh -- pwsh -NoProfile -File eng/PackageSmoke.ps1`.
Erfolgreicher frischer Workspace:
`/home/user1/dev/.agenttemp/multisession-package-smoke-4041d491e5ea4ddfb46d29f49c055628`.

Der Konsument liegt außerhalb des Produktquellbaums. Er besitzt ausschließlich
PackageReferences auf lokale Pakete 0.1.0 und genehmigte Abhängigkeiten, keine
ProjectReferences oder kopierten Bibliotheksquellen. Seine eigene Providerklasse
stammt aus der Konsumvorlage; sie verwendet nur öffentliche Verträge. Beide
Restores verwenden denselben anfangs leeren Cache. Quellenzuordnung bindet eigene
Pakete an den lokalen Feed und erlaubt Microsoft/System/SQLitePCLRaw aus nuget.org.
Der erste Versuch war ebenfalls grün, verwendete beim zweiten Restore aber noch
den Standardcache und ist deshalb nicht der hier verwendete Nachweis.

PASS: zwei Sessions, zwei überlappende erste Sends, zwei Folgeanfragen mit festem
Binding/Head, öffentliche Status-/Historyabfragen, ein bestätigter laufender
Abbruch, kontrollierter Stop und native Store-Wiederöffnung aller fünf
ursprünglichen Requests ohne offene Reservierung. JSON-Report: Sessions=2,
Requests=5, ReopenedRequests=5. Keine Anbieterzugänge oder Netzwerkaufrufe des
Adapters. Restorezugriffe auf NuGet sind separat hiervon zu verstehen.

Codemetr 0.1.0 misst den materialisierten Konsumenten explizit: eine Datei,
zwei Typen, null Verstöße/Parsefehler/übersprungene Dateien. Der vollständige
Repositoryguard bleibt unverändert im Umfang; es wird keine Ausnahme für
konsumierenden Code eingeführt. Paket-/Vorlagen-/Projekt-/Konfigurationsdigests
und gemessene Abschlusszeit stehen im [Manifest](package-consumer-manifest.json).
Lokaler Linux-Vollstand und Hash: [Pakete](packages.md).

Dies ist ein technischer Konsumnachweis. Er ersetzt weder einen unabhängigen
Integrator nach T106 noch native Windows-Paketprüfung und finale identische
Source-/Paket-/Zielsystembelege T099/T104/T110. Kein SharpClaw-Code wurde geändert.
