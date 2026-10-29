# Ein kanonischer Paketstand für beide nativen Konsumenten

Gemessen2026-10-29T22:24:04Z. Basis451ce8c, Sourcehash95780bfa75c745cd0adb804e0a1ac3dfcea6474169900c8ab402990248e40f5b.
ci.yml SHA256535bb139f571bbe4f677fcf898933a016e7b0302887696c67e006d4f19b6cb1e;
PackageSmoke.ps1 8a14781762922a367c5cbd6db8c52d95e4bf0e38dcd4c92e30060c3af1548705;
Invoke-Quality.ps1 5a51fbaf7cad2bb79091460c69d102948f966ddee8a820160ffb7a1dd8f25661.

## Tatsächlicher Befund

Native CI40962572345 besteht auf beiden Systemen mit identischem Sourcehash. Beide
Jobs packen jedoch eigenständig und konsumieren ihre jeweils eigenen sechs Archive.
Vergleich native-package-candidate.json: alle sechs Archivhashes unterscheiden sich.
Membervergleich unter artifacts/tdd/native-package-member-comparison.json zeigt auch
DLL-/XMLunterschiede; Nuspec/Coremetadata enthalten unter anderem LF versus CRLF.
Nicht behauptet wird, dass die Binärunterschiede ausschließlich daraus stammen.
Diese Läufe beweisen zwei native Builds, aber nicht den Konsum derselben konkreten
Paketbytes auf beiden Systemen. T096/T110 verlangen nachvollziehbare Paketidentität.

## Bestand und Entscheidung vor Umsetzung

PackageSmoke akzeptiert bereits PackageDirectory, prüft genau sechs Paketlayouts und
verwendet eine frische externe Anwendung mit leerem Cache und eigenem Adapter. Sein
Manifest enthält für jedes Archiv File/Sha256 und bestätigten Consumerstatus. Nichts
an Library, Paketversion, Provider oder Konsumvorlage muss geändert werden.

CI erhält nach erfolgreicher Qualitymatrix einen nachgeordneten nativen Consumerjob
je OS. Beide laden dasselbe unveränderliche packages-and-consumer-linux-Artefakt aus
GENAU DEMSELBEN Workflowrun. Ein kleiner separater Hashprüfer gleicht exakt sechs
Archivnamen/SHA256 gegen den erfolgreichen Produzentenmanifest ab, bevor restauriert
wird. PackageSmoke konsumiert danach genau dieses Verzeichnis. Producer-/Consumer-
Hashbeleg und die neuen Konsumreports werden archiviert. Keine Reproduktion anderer
Buildbytes behauptet; ein ausgewähltes kanonisches Artefakt wird tatsächlich zweimal
verwendet. Erforderliche Branchchecks nach echten grünen Läufen um die beiden neuen
Namen ergänzen, aktuelle Checks und strict/admin enforcement erhalten.

## Tests und Risiken

Hashprüfer erst ausführbar lax (alle Eingaben PASS), dann echte negative PowerShell-
Verhaltenstests: manipuliertes Archiv, fehlendes/zusätzliches Archiv, doppelte Manifest-
identität, ungültiger Digest und fehlgeschlagener Produzent müssen rot sein; korrektes
Sechserartefakt grün. Erst danach minimaler Prüfer. Kein Zip-/NuGetlayoutwissen duplizieren:
Test-PackageLayout bleibt dessen Autorität. Archivnamen müssen einfache Dateinamen sein,
keine Traversalpfade. Tests erzeugen ausschließlich eigene kleine Fixturedateien unter
UserProfile/dev/.agenttemp und werden in beiden nativen Qualitätstoren ausgeführt.
Workflow-/Buildmetadaten erfordern passende echte CIvalidierung, kein erfundenes
Produkt-TDD. Sourcescope/Grenzwerte/Pins/Baseline bleiben unverändert.
Die neue CI ist ein Buildinput und erhält deshalb einen neuen finalen Sourcehash;
Stundenbelege älterer Kandidaten bleiben ehrliche Checkpoints.
