# Abhängigkeitsprüfung

Aktueller Zusatzabgleich 2026-10-29T22:55:03Z: vollständiger Graph aller elf
Projekte im lokalen Tor8530362e86bf4cfcaf1669f640cd3f24 auditiert, keine gemeldeten
direkten/transitiven Advisories. Separater echter Workflow40969224192 erfolgreich
am Kandidaten7489450. Die restaurierten Nuspecs von coverlet.collector10.1.0 und
Microsoft.Extensions.TimeProvider.Testing10.7.0 nennen jeweils MIT und ihre
offiziellen Coverlet-/dotnet/extensions-Repositories; beide sind test-only und
PrivateAssets. Native SQLitecheckpoint3.53.3 auf Ubuntu und Server2025.
DEP-001 bleibt eine zuständige Maintenanceentscheidung vor erster Freigabe;
die eigene Produktlizenz ist weiterhin nicht gewählt. Folgeplan remaining-acceptance.md.
Die folgenden ursprünglichen Prüfdaten werden als historische Belege erhalten.

**Datum**: 2026-10-27. **T003**: initiale Pins und Prüfung abgeschlossen.
**Q-02**: finale Prüfung am Produkt-Lieferstand weiterhin offen (T104).

## Vorgehen und tatsächliche Ergebnisse

Die fünf vorgegebenen Versionen wurden im offiziellen NuGet-Flatcontainer-Index
gefunden; deren Nuspecs wurden gelesen. `Directory.Packages.props` pinnt diese
Versionen zentral, keine stillen Ersetzungen. Ein temporäres net10.0-SDK-Projekt
außerhalb des Produktquellbaums referenzierte alle fünf Pakete gemeinsam.

- Restore mit zentralen Projektprops: Exit 0.
- `dotnet list <Probeprojekt> package --vulnerable --include-transitive --no-restore
  --format json`: Exit 0, keine Pakete mit bekannten Schwachstellen gemeldet.
- `dotnet list <Probeprojekt> package --deprecated --include-transitive --no-restore
  --format json`: Exit 0; xUnit-v2-Pakete sind als Legacy/deprecated markiert (siehe unten).
- Native In-Memory-SQLite-Probe über `Microsoft.Data.Sqlite.SqliteConnection.ServerVersion`:
  erfolgreicher Lauf unter Windows x64, native SQLite-Version **3.53.3**.
- Alle aufgelösten Nuspecs aus dem Restoregraphen wurden auf Lizenz und Herkunft gelesen.
  Kein Produktcode, keine Providerzugänge und keine Produktdaten wurden verwendet.

Probeaufrufe: `dotnet restore <Probeprojekt>`, `dotnet run --project <Probeprojekt>
--no-restore`, jeweils mit `-p:DirectoryBuildPropsPath=<Root>/Directory.Build.props`
und `-p:DirectoryPackagesPropsPath=<Root>/Directory.Packages.props`.
Der erste Probeaufbau importierte zentrale Props zu spät (NU1015), danach doppelt
(MSB4011); die Probe wurde korrigiert und Restore ohne Warnungen wiederholt.
Das Test-SDK erzeugte zunächst einen konkurrierenden Einstiegspunkt (CS7022);
die reine Konsolenprobe deaktivierte dessen GenerateProgramFile und lief danach erfolgreich.
Dies sind Infrastrukturfehler, keine fachlichen Red-Tests.

## Direkte und transitive Lizenzen / Herkunft

| Pakete | Version | Lizenz / Herkunft |
| --- | --- | --- |
| Microsoft.Data.Sqlite, Microsoft.Data.Sqlite.Core | 10.0.14 | MIT; Microsoft / dotnet/dotnet |
| Microsoft.Extensions.Logging.Abstractions, Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.14 | MIT; Microsoft / dotnet/dotnet |
| Microsoft.NET.Test.Sdk, Microsoft.CodeCoverage, Microsoft.TestPlatform.ObjectModel, Microsoft.TestPlatform.TestHost | 18.11.0 | MIT; Microsoft / microsoft/vstest |
| SQLitePCLRaw.bundle_e_sqlite3, SQLitePCLRaw.core, SQLitePCLRaw.lib.e_sqlite3, SQLitePCLRaw.provider.e_sqlite3 | 2.1.13 | Apache-2.0 laut Nuspec; ericsink/SQLitePCL.raw |
| xunit, xunit.assert, xunit.core, xunit.extensibility.core, xunit.extensibility.execution | 2.9.3 | Apache-2.0; xunit/xunit |
| xunit.analyzers | 1.18.0 | Apache-2.0; xunit/xunit.analyzers |
| xunit.runner.visualstudio | 3.1.5 | Apache-2.0; xunit/visualstudio.xunit |
| xunit.abstractions | 2.0.3 | Legacy-Nuspec verweist auf xunit/xunit/license.txt: Apache-2.0 mit separat benannten MIT-Anteilen; kein SPDX-Ausdruck im alten Nuspec |

Lizenztexte und Notices müssen bei Paket-/Produktweitergabe berücksichtigt werden;
Paket-Lizenzmetadaten ersetzen keine abschließende Distributionsprüfung.
Quellen: `https://api.nuget.org/v3-flatcontainer/<id>/<version>/<id>.nuspec`,
die tatsächlich restaurierten Nuspecs und `https://raw.githubusercontent.com/xunit/xunit/master/license.txt`.

## Wartung und Risiken

- Microsoft-Pakete stammen aus der gepinnten unterstützten .NET-10-Linie; Testplattform
  und Runner besitzen verifizierbare Upstream-Repositories und Release-Metadaten.
- SQLitePCLRaw 2.1.13 ist die transitiv durch Microsoft.Data.Sqlite 10.0.14 ausgewählte
  Version; die native Laufzeit wurde tatsächlich ermittelt, nicht aus dem Paketnamen geraten.
- **DEP-001: xUnit v2 ist deprecated/Legacy**, betrifft xunit 2.9.3 sowie die vier
  oben genannten transitiven v2-Pakete. NuGet empfiehlt xunit.v3. Es wurde keine
  bekannte Sicherheitslücke gemeldet; das ist ein Wartungsrisiko, kein Beleg für
  gegenwärtige Unsicherheit oder vollständige Wartungsfreiheit. Die ausdrücklich
  geplanten Pins bleiben unverändert. Verantwortlich: Projektverantwortlicher;
  Entscheidung über v3-Migration bzw. dokumentierte Beibehaltung spätestens bei
  T104/T108 vor erster Freigabe. Keine Migration oder Risikoannahme wird hier erfunden.
- Audit ist eine Momentaufnahme der verfügbaren Advisories, keine Sicherheitsgarantie.
  Den konkreten Solutiongraphen samt späterem Collector in T007/T104 erneut prüfen;
  Linux-Native-SQLite und Paketkonsum bleiben separate echte Nachweise.
