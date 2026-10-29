# Lokale Bibliothekspakete 0.1.0

Aktueller kanonischer Stand nach ABIrefactoring: Source dba419f35bf40fd7282a5b2f3bfe7c685576e2c62ceb570d5fec2d0f4f84498b,
Produzent quality40968636580. Exakte sechs Dateinamen/SHA256 beim Produzenten und
beiden nativen Konsumenten in [canonical-package-final.json](canonical-package-final.json),
jeweils PASS. Native Library-/Hostprüfung build-native.json, keine Test-/Hostpakete,
kein NuGet-Push. Windows konsumiert dasselbe Linuxproduzentenarchivset, statt
zwei unabhängig gebaute unterschiedliche Archive als identisch auszugeben.
Der folgende lokale Ursprungsbeleg bleibt für seinen damaligen Stand erhalten.

Prüfstand dokumentiert am 2026-10-29T19:45:52Z. Vorabanalyse:
[package-consumer](architecture/2026-10-29-package-consumer.md).
`dotnet pack Monstertacle.Multisession.slnx -c Release --no-build -o artifacts/packages -p:PackageVersion=0.1.0`
erzeugte genau sechs nupkg-Dateien. Testprojekte und ValidationHost wurden nicht
gepackt. Der erste Layoutlauf gegen den vorherigen Stand war fachlich rot:
`Missing packaged README`. Red-Archive und Paketdigests stehen unter
`artifacts/tdd/package-layout-red-*`.

Nach zentraler Version/Metadaten und Release-Rebuild besteht
`pwsh -NoProfile -File eng/Test-PackageLayout.ps1`: Identität/Version, enthaltenes
README, Repositorymetadaten, genau eigene Bibliotheksassembly, korrekte
Abhängigkeiten und keine Quell-/privaten Dateien. Alle eigenen Paketabhängigkeiten
verweisen auf 0.1.0. Gegen unbekannte Paketabhängigkeiten schlägt die Prüfung fehl.

Die konkreten SHA256 aller sechs Archive stehen im versionierten
[Manifest](package-consumer-manifest.json). Die Produktlizenz ist nicht festgelegt;
keine Lizenz wurde erfunden und keine Veröffentlichung ausgeführt. Diese Grenze
bleibt T104/T110 zugeordnet. Fremdlizenz-/native SQLite-Abnahme bleibt davon getrennt.

Volltor Linux `15b459ed027a487a84e16788972d8675`: 990 Tests, 97.5677 %,
Sourcehash `ee3b9c21381727066e30f34af464fe99c424bf5fa9b904ec73f45d34845c6396`.
Der Hashumfang enthält jetzt auch README, eng und C#-Konsumvorlagen. Dafür wurde
zuerst das Auslassen des README gegen die alte Funktion fachlich rot beobachtet;
anschließend sind alle drei Paketinput- und der Output-Ausschlussfall grün.
Keine historischen Hashes oder Messgrenzen wurden geändert.
