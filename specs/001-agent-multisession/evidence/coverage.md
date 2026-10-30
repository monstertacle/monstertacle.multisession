# Coverage — aktueller nicht freigegebener Arbeitsstand

Aktueller technischer Kandidat nach T125/T126: **fb1a167 / Source d51bf9f9...**,
native Gesamt-/Paket-/Guard-/Auditprüfungen erfolgreich. Verbindlicher aktueller
Index mit vollständigem Sourcehash, ausgewähltem Paketartefakt, Laufnummern und
Abnahmegrenzen: [delivery-candidate.md](delivery-candidate.md),
[build-native.json](build-native.json), [canonical-package-final.json](canonical-package-final.json).
Die älteren Zahlen/Quellstände in den folgenden Abschnitten bleiben historische
Checkpoints; sie ersetzen diesen aktuellen Bericht nicht. Die reale finale Stunde
und fehlenden externen Freigaben bleiben separat ausgewiesen.

## Aktueller nativer Kandidat

Prüfstand2026-10-29T23:00:40Z: gleicher Sourcehash
`dba419f35bf40fd7282a5b2f3bfe7c685576e2c62ceb570d5fec2d0f4f84498b`
auf beiden Systemen, tatsächlicher Qualityrun40968636580. Rohzusammenfassungen,
TRX-Digests und eigenes Modul-/Projektinventar in [build-native.json](build-native.json).
Ubuntu24.04:2943/3016 Zeilen=97,5796 %,1036Tests; WindowsServer2025:
3004/3080=97,5325 %,1042Tests. Beide PASS bei unverändert87 %, keine Skips.
Pro OS Common/SQLite/beide Provider plus eigene native Plattformbibliothek;
fremde Plattform ausschließlich im anderen nativen Bericht. Shared-NativeHttp
ist in beiden Providerassemblies tatsächlich gemessener Produktcode.
Tests/ValidationHost nicht im Produktnenner; eigener nicht ausgeführter Produktcode
wird nicht ausgeblendet. Acht tatsächlich erzeugte Coverletberichte je Lauf,
keine alten Reports aus vorigen Verzeichnissen zusammengezählt. SDK10.0.403,
Collector10.1.0, Runtime10.0.14, VSTest, tests/coverage.runsettings.
Keine zusätzliche Exklusion oder Schwellenabsenkung. Fehlendes Windows11 bleibt
Plattformabnahmeblocker, keine Behauptung eigener Win11-Coveragemessung.

## Historische Zwischenmessungen

**Datum**: 2026-10-28. **Plattform**: Windows x64. **SDK**: 10.0.403.
Collector: coverlet.collector 10.1.0, VSTest; Einstellungen `tests/coverage.runsettings`.
Inventar `tests/coverage-scope.json`, ausdrücklich genehmigter Grenzwert **87 %**
gemäß Constitution v2.0.0 (Nutzerentscheid 2026-10-28).

Ausgeführt: `pwsh -NoProfile -File scripts/Invoke-Quality.ps1 -Platform windows`.
Der Runner erstellt eine neue Artefaktablage, prüft Sourcehash vor/nach dem Lauf,
führt Locked-Restore, Release-Build, Format, Gate-Smokes, alle passenden Tests,
transitiven Vulnerability-Audit sowie zeilenweise Reportunion aus.

Sourcehash des geprüften Code-/Build-/Test-/Qualitätsumfangs:
`08afb7af04991cf78784e666f5d72be7a5349c0be8f483f8c9556692eef3e269`.
Umfang gemäß scripts/Invoke-Quality.ps1:Get-SourceHash; Metadaten und Rohberichte:
`artifacts/quality/windows/5319ba17b61146ba99842aaadfb2c6fd/`.

| Ergebnis | Tatsächlicher Stand |
| --- | --- |
| Locked-Restore / Release-Build / Format | erfolgreich; 0 Warnungen und Fehler im Build |
| Unit-Tests | 158 bestanden |
| Contract | 16 fachliche Ausgabegrenzenfälle und 1 Infrastrukturtest bestanden |
| Integration / native Windows-TFM | je 1 Infrastrukturtest bestanden |
| Coverage-Gate-Verhaltenstests | 13 bestanden, einschließlich exakt 87 %, unter 87 % und ungültiger Konfigurationsgrenzen |
| Übersprungene Tests | 0 |
| Produktzeilen gesamt / abgedeckt | 264 / 233 |
| Zeilenabdeckung | **88,2576 %** |
| Gate / vollständiger lokaler Windowsrunner | **PASS / Exit 0** bei Mindestwert 87 %; keine Produktfreigabe |

Gemessen: Common, Storage.Sqlite, Providers.OpenAI, Providers.Gemini und Windows.
Linux ist dem eigenen nativen Linuxlauf zugeordnet, nicht auf Windows als bestanden
behauptet. Testcode, nichtproduktiver ValidationHost und SDK-generierte obj-Dateien
sind gemäß dokumentiertem Scope abgegrenzt; keine zusätzlichen Ausschlüsse.
Die Collector-Smokes erfassen auch nicht verwendete Produktassemblies. Doppelte
Collectorberichte werden je Modul/Datei/Zeile vereinigt, Prozentwerte nicht gemittelt.
264 Zeilen sind der gesamte gemessene Produktumfang, nicht nur neue/geänderte Zeilen.

Noch ungetestete Vertrags-/Runtimegerüste sind enthalten; keine Tests lediglich zum
Aufrufen von NotImplementedException und kein Herausfiltern dieser Gerüste zur
Verbesserung der Zahl. Nächste Schritte brauchen echte Verhaltensfälle und Umsetzung.
Wartungsbefund DEP-001 sowie Linux-/Zielsystem-/Live-/Paket-/CI-Abnahmen bleiben offen.
GitHub-Workflows sind konfiguriert, aber mangels angebundenem Repository nicht gelaufen;
erforderliche Branchchecks sind nicht aktiv eingerichtet.

Historischer Vergleich: 2026-10-27 55/119 Zeilen = 46,2185 %; vor der Admission-
Erweiterung 142/188 = 75,5319 % (Lauf `2fb5fada4698425fb896e64c5f603410`);
nach Admission 181/220 = 82,2727 % (Lauf `ad21d6f9f7c346a3805a63ed2c26725e`,
Sourcehash `60e2231396269599394119dbfe3105328fc5f44f726a339d55e146a3abaed196`);
nach Ausgabegrenze 203/235 = 86,3830 % (Lauf `ad8b75557062446d98187fa301b7fbfd`,
Sourcehash `b4f7b2df75e280b83e64ea110eafc1ccf47af1f630cfad555bbf91f0bfdcb611`);
unter dem damaligen 90-%-Gate jeweils FAIL. Letzter 90-%-Lauf nach FIFO:
233/264 = 88,2576 %, `53582390efb54d0e8c568b3d84615c9f`, Sourcehash
`5f6ef71246cb3758190f8cc6debdca98587fbfb3f8168e71cc46ed2e33ad7fc6`, FAIL.
Diese historischen Ergebnisse wurden nicht umgeschrieben. Der neue PASS entsteht
durch die genehmigte Schwellenänderung, nicht durch zusätzlich abgedeckten Produktcode.
Ein Fortschritt der Coverage ist keine Abnahme unimplementierter Funktionen.

## T112: nativer Linuxlauf nach Repositoryübernahme

SDK 10.0.403 / Debian GNU/Linux 13, vollständiger Runner
`pwsh -NoProfile -File scripts/Invoke-Quality.ps1 -Platform linux`: Exit 0.
Sourcehash `617d3f67adf713058c7a60950846899f93893a58ddc4fd23ac3af55e2db4417f`,
Artefakte `artifacts/quality/linux/b83d5853697d45c5aa01d3a26f4d8883/`.
**241/271 = 88,9299 %** des gesamten nativen Produktumfangs, Mindestwert 87 % erfüllt.
158 Unit-, 26 Contract-, 1 Integration- und 1 Linux-Infrastrukturtest bestanden;
13 Coverage-Gateverhaltensfälle bestanden. Vollständiger Codemetr-Guard: 76 Typen,
0 Verstöße/Parsefehler/übersprungene Typen. TDD und Grenzen: [tdd-us1.md](tdd-us1.md).
Kein Windows-/Ubuntu-Zielsystem-/CI-Nachweis für diesen Stand; keine Produktfreigabe.

## T025: Dispatcher

Vollständiger nativer Linuxrunner: Exit 0, **376/400 = 94,0 %** Produkt-Zeilenabdeckung.
Sourcehash `790529a286d607b236aa25aabba02462a5f17a4ed289d3ca61143404349f8916`;
Originalberichte `artifacts/quality/linux/00ee4f74a76e4f64be51e24f8308be76/`.
223 .NET-Tests und 13 Gateverhaltensfälle bestanden, 0 fehlgeschlagen/nicht ausgeführt.
Full-Codemetr 0.1.0: 88 Dateien/89 Typen, 0 Verstöße/Parsefehler/übersprungene Typen.
TDD, Selbstprüfung und Freigabegrenzen: [tdd-us1.md](tdd-us1.md), Abschnitt T025.
Kein neuer Windows-/CI-/Zielsystem-/Liveprovider-/Paketabnahmenachweis.
