# T095/T096: Pakete und frischer Konsument

Bestandsprüfung 2026-10-29T19:37:30Z gegen 04f0b2f plus Soak-Schnitt.
Directory.Build.props/Directory.Packages.props, alle sechs Bibliotheksprojekte,
Test-/Host-Packable-Regeln sowie ConsumerProbeFlow und ConsumerOwnedProvider
wurden gelesen. Version ist bisher der SDK-Default, Paket-README und eigene
Metadaten fehlen. Es existiert kein PackageSmoke-Skript und kein echter
NuGet-Konsument. Quellprojektreferenzen beweisen diesen Vertrag nicht.

Ausgangshashes SHA256:
Directory.Build.props 6041a4ba30fdf027aceea90230c4e8f6d986e092eba659667fc0b8e304f165fc;
README.md 82e395840ba4e2342c8b9c9733fc07cbcb65662c02341e5db9cf1fe2d5c3cfdc;
Common.csproj 5bd33b409f21209c5d626a5df89a4050e827000767b16af77a52299df3af3c8c;
Storage.Sqlite.csproj 329e102c6eabaee4fde50351a8bbfec63693bfff5ff62b84bf54bbbab824a0ed.

Entscheidung vor Implementierung: Version 0.1.0 und neutrale Paketmetadaten
zentral setzen, README in die sechs Bibliothekspakete aufnehmen. Tests und Host
bleiben nicht packbar. Keine Lizenz erfinden; fehlende Eignerentscheidung zur
Produktlizenz bleibt eine Veröffentlichungsgrenze unter T104/T110. Es erfolgt
weder NuGet-Push noch Release oder SharpClaw-Änderung.

Ein PowerShell-Smoke erzeugt unter der freigegebenen Agenttemp-Ablage einen
frischen Konsumenten aus einer eigenen C#-Vorlage. Nur PackageReferences,
keine ProjectReferences oder kopierten Bibliotheksquellen. Ein dort definierter
Provider muss zwei Sessions, überlappende erste Sends, Folgeanfragen, öffentliche
Status/History-Abfragen, bestätigten Cancel, Stop und native Store-Wiederöffnung
bestehen. Plattformalias und TFM werden explizit für den nativen Host gewählt.
NuGet-Quellenzuordnung bindet Monstertacle.* ausschließlich an den lokalen Feed;
Microsoft.*, System.* und SQLitePCLRaw.* dürfen aus nuget.org kommen. Ein eigener
leerer Packagecache verhindert zufällige lokale Restore-Erfolge. Hashmanifest
enthält die sechs nupkg-Dateien, Vorlage, resultierende Consumerdateien und
UTC-Messung. Der materialisierte eigene C#-Code wird zusätzlich mit Codemetr
gemessen und nicht aus Qualitätsbetrachtung ausgeblendet.

Zuerst vorhandene Release-Artefakte packen und fehlende README-/Versionsmetadaten
fachlich rot prüfen. Danach Metadaten, Rebuild/Pack/Layoutprüfung und frischen
Konsum ausführen. Ein bereits korrekt funktionierender öffentlicher Ablauf darf
sofort grün sein; kein nachträglich erfundenes Library-Red. Fehlgeschlagene echte
Paket- oder Einbettungslücken erhalten einen separaten Red/Green-Nachweis.

Vor Ergänzung der Hashabdeckung geprüft am 2026-10-29T19:42:47Z:
Invoke-Quality.ps1 3cb2ed3d3bc2aab7cb45d5a030b4de7ef73c8b6c3ae6343903afaaa617880ddc;
PackageSmoke.ps1 2ee92ccffb0c159b52165a8cd2651741dce2604ddf138468f548ee950b80b66e;
PackageConsumer.cs.txt e17984c0c4a8c0b8f053beb3d19a9f2646b89ef5f0e69027ae2696f7a97af615.
Der bisherige Quality-Sourcehash erfasst weder neue eng-Skripte noch C#-Vorlagen
mit Endung .txt oder das jetzt gepackte README. Vor finalem Paketnachweis wird
sein Inputumfang um diese tatsächlichen Build-/Konsumeingaben erweitert; keine
bestehende Eingabe entfällt. Der Hashalgorithmus bleibt gleich. Bestehende
historische Hashwerte werden nicht umgeschrieben. Die neuen Eingaben werden
zusätzlich im Paketmanifest einzeln gehasht. Der erste Konsumlauf war grün,
aber sein zweiter Restore verwendete versehentlich wieder den Standardcache;
beide Restores erhalten jetzt denselben frischen Cache und werden erneut geprüft.
