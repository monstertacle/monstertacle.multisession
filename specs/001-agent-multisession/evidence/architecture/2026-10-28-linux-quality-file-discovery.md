# Native Linuxprüfung: versteckte Quelldateien erfassen

Created: 2026-10-28T22:58:51Z
Owner: aktuelle Implementierungssitzung; Branch `feature/reserved-dispatch-contract`.
Basis: Commit `0cd4ac8`; Constitution IV, notwendige T112-Validierung.

Vor Änderung: `scripts/Invoke-Quality.ps1`, SHA256
`6720fcb3aac62ab2a405c6a7f40bf553a102065c7175fadecfa3472e4e136ae0`.
Aufrufer ist `.github/workflows/ci.yml:30`; das Skript ruft Get-SourceHash vor und nach
Restore/Build/Format/Tests/Audit auf und schreibt SourceHash in run.json. Get-SourceHash
ermittelt eine feste Rootdateiliste mit Get-Item; darunter liegen .editorconfig und
.config/dotnet-tools.json. Unter Linux blendet PowerShell versteckte Dateien standardmäßig
aus, auch bei explizitem Pfad. Fehler beendet den Runner vor jedem Qualitätsschritt.

Tatsächlich beobachtet: Runner Exit 1 mit `Could not find item .../.editorconfig`;
`Get-Item .editorconfig` reproduziert denselben Fehler; `Get-Item -Force .editorconfig`
liefert die vorhandene Datei. PowerShell 7.6.9 auf Debian GNU/Linux 13, .NET SDK 10.0.403.
Kein fachlicher TDD-Red für Produktcode: Fehler in Build-/Quellmanifestmetadaten.

Entscheidung vor Änderung: nur `-Force` an der festen Get-Item-Liste ergänzen; Scope,
Hashformat, Checks, Plattformprüfung und Pins erhalten. Kein neuer Dateiscanner oder
Workaround am Produkt. Danach originalen Linuxrunner vollständig ausführen; Manifest-
und Coverageergebnisse im T112-Nachweis dokumentieren. Keine offene Architekturfrage.
Die aktuelle Distribution ist kein Ubuntu-24.04-Zielsystemnachweis.
