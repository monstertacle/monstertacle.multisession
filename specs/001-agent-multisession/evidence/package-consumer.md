# Tatsächlicher Konsum lokaler NuGets

Aktueller technischer Kandidat nach T125/T126: **fb1a167 / Source d51bf9f9...**,
native Gesamt-/Paket-/Guard-/Auditprüfungen erfolgreich. Verbindlicher aktueller
Index mit vollständigem Sourcehash, ausgewähltem Paketartefakt, Laufnummern und
Abnahmegrenzen: [delivery-candidate.md](delivery-candidate.md),
[build-native.json](build-native.json), [canonical-package-final.json](canonical-package-final.json).
Die älteren Zahlen/Quellstände in den folgenden Abschnitten bleiben historische
Checkpoints; sie ersetzen diesen aktuellen Bericht nicht. Die reale finale Stunde
und fehlenden externen Freigaben bleiben separat ausgewiesen.

Finaler technischer Kandidat7489450 mit Source dba419f35bf40fd7282a5b2f3bfe7c685576e2c62ceb570d5fec2d0f4f84498b:
quality40968636580 vollständig grün; [canonical-package-final.json](canonical-package-final.json)
enthält beide vollständigen Consumerreports und verifizierten gemeinsamen
Sechserhashsatz. Erneut zwei Sitzungen/fünf Requests/fünf Reopens je OS.
Das schließt den technischen Paketkonsum, nicht den fremden Integrator T106.

Aktueller nativer Checkpoint: quality40966982840 am Source2c5fd5. Ubuntu und Windows
haben dasselbe kanonische Sechserartefakt konsumiert; unabhängiger Digestvergleich
[canonical-package-checkpoint.json](canonical-package-checkpoint.json) PASS.
Alle sechs File/SHA256 stimmen beim Produzenten und beiden Konsumenten überein,
jeweils zwei Sitzungen/fünf Requests/fünf Reopens. Die sechs Kanonarchive stammen
aus genau einem Linuxproduzenten, nicht aus behaupteter binärer Reproduzierbarkeit
zweier Builds. Beide neuen Required Checks sind nach realem Green erzwungen.
Der folgende lokale Bericht bleibt historisch; finaler Stand siehe build-audit.md.

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
