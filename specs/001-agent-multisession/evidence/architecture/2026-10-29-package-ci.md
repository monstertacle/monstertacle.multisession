# Native CI für den Paketkonsumenten

Bestandsprüfung 2026-10-29T19:46:51Z gegen 9632f7e. CI baut bereits die ganze
Solution je nativem OS; ein anschließendes --no-build-Pack kann genau diese
Assemblies verwenden. PackageSmoke läuft bisher lokal und behält seine Reports
nur im privaten temporären Workspace. Geprüfte SHA256:
ci.yml 58161d9ab567d7bd70265b53e60a12ba6a44b2f0f72d48cbf85ae6a2b3a6ea6e;
PackageSmoke.ps1 cffe6a38e45b91b5696bdd52b4e25e31f2692d9b52ec0ec47e628bea82c573b1.

Entscheidung: nach erfolgreicher nativer Qualität lokal packen und denselben
Smoke auf Windows/Linux ausführen, keine Publikation. Manifest, eigener
Consumercode, Projekt/Config/Lock, endlicher Ergebnisreport und separater Guard
werden unter artifacts/package-smoke archiviert. Paketcache/Bibliotheksquellen
werden nicht in diesen Nachweis kopiert. Beide Plattformen laden diese Reports
und die sechs nupkg-Dateien zusätzlich hoch. Bestehende Qualitätsschritte und
Gategrenzen bleiben erhalten. Lokaler Smoke überprüft Reportkopie und gültige
PowerShell-Syntax; echte Windows-Ausführung erfolgt in CI.

2026-10-29T19:54:41Z: Linux-Job 123692087909 aus Lauf 40943329086 besteht die
volle Qualität und alle sechs Paketlayoutprüfungen. Der Konsument scheitert am
fehlenden lokalen Toolrestore; Log verlangt ausdrücklich `dotnet tool restore`.
Der Quality-Job hatte bislang kein Codemetr benötigt, der separate Guard-Job
restauriert seine Tools in einer anderen Maschine. Entscheidung: im Paket-
Schritt den unveränderten gepinnten Toolmanifest restaurieren und Exit prüfen.
Das ist ein Infrastrukturfehler, kein fachlicher Red-Beleg. Zusätzlich bekannte
Consumerdateien/Logs auch bei Fehlschlag archivieren; keine Rohcredentials sind
konfiguriert. Paket-/Produktregeln und Qualitätsgrenzen bleiben unverändert.

2026-10-29T19:55:51Z: Wiederholte lokale Smokes zeigen einen zusätzlichen
Diagnosebefund: archivierte Kopien von Program.cs unter artifacts werden vom
unveränderten Full-Context erneut eingelesen. Dadurch entstehen doppelte
PackageConsumer-Deklarationen und eine ausdrücklich gemeldete Mehrdeutigkeit.
Entscheidung: ausschließlich die unveränderte Belegkopie als Program.cs.txt
archivieren. Das echte Program.cs im privaten frischen Konsumenten bleibt
kompiliert und explizit mit --files-from gemessen (zwei Typen). Keine Änderung
an codemetr.json, Kontext, Regeln oder Ausschlüssen. Frühere doppelte eigene
Artefaktkopien werden gleichartig umbenannt; historische Messberichte bleiben.
