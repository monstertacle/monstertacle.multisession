# Bootstrap-Nachweis

## T002 — SDK, Build- und Formatierungsregeln (2026-10-27)

SDK 10.0.402 ist in `global.json` ohne Roll-forward und ohne Prereleases gepinnt.
`Directory.Build.props` legt C# 14, Nullable, implizite Usings, .NET-Analyzer,
Analyselevel 10.0-recommended, Build-Styleprüfung, Warnungen als Fehler,
deterministische Builds, Paket-Lockfiles und transitiven NuGet-Audit fest.
`.editorconfig` definiert Microsoft-konforme C#-Formatierung und Namenskonventionen.
`.gitignore` nimmt Build-/IDE-Artefakte und lokale Geheimnisse aus, nicht Paket-Lockfiles.
Das Arbeitsverzeichnis ist weiterhin kein Git-Repository; keines wurde angelegt.

Validierung: temporäres verhaltensloses SDK-Projekt außerhalb des Produktquellbaums,
`dotnet msbuild <SetupValidation.csproj> -p:DirectoryBuildPropsPath=<Root>/Directory.Build.props
-getProperty:NETCoreSdkVersion,LangVersion,Nullable,ImplicitUsings,EnableNETAnalyzers,AnalysisLevel,EnforceCodeStyleInBuild,TreatWarningsAsErrors,Deterministic,RestorePackagesWithLockFile,NuGetAudit,NuGetAuditMode,NuGetAuditLevel`.
Exit 0: SDK 10.0.402, LangVersion 14.0, Nullable enable, ImplicitUsings enable,
AnalysisLevel 10.0-recommended, alle genannten booleschen Schalter true,
NuGetAuditMode all und NuGetAuditLevel low.

Dies ist Buildmetadaten-Validierung, kein fachlicher Red-/Green-Nachweis.
Solution-Restore, Produktbuild, Testentdeckung und Locked-Restore aus T007 stehen noch aus.

## T004–T008 — Setup-Checkpoint (2026-10-27, Windows x64)

Die obige Aussage zum ausstehenden T007 beschreibt den früheren T002-Stand.
Inzwischen sind sechs verhaltenslose Bibliotheken, vier xUnit-Testprojekte und der
nicht packbare ValidationHost angelegt. Alle elf Projekte sind in der Solution;
Abhängigkeiten zeigen zu Common, nicht umgekehrt. Die zwei Plattform-TFMs sind
explizit und bedingt referenziert. Noch kein fachliches Produktverhalten.

Ausgeführt mit SDK 10.0.402:

| Befehl / Umfang | Ergebnis |
| --- | --- |
| `dotnet restore Monstertacle.Multisession.slnx` | Exit 0; elf `packages.lock.json` erzeugt |
| `dotnet build Monstertacle.Multisession.slnx -c Release --no-restore` | Exit 0; 0 Warnungen, 0 Fehler |
| `dotnet restore Monstertacle.Multisession.slnx --locked-mode` | Exit 0 |
| `dotnet test tests/Monstertacle.Multisession.<Suite>/Monstertacle.Multisession.<Suite>.csproj -c Release --no-build --no-restore --logger trx` für UnitTests, ContractTests, IntegrationTests | Je Exit 0; je 1 Infrastrukturtest, 0 übersprungen |
| PlatformTests mit obigem Befehl und `-f net10.0-windows` | Exit 0; 1 Infrastrukturtest, 0 übersprungen |
| `dotnet format Monstertacle.Multisession.slnx --verify-no-changes --no-restore` | Exit 0 |
| `dotnet list Monstertacle.Multisession.slnx package --vulnerable --include-transitive --no-restore --format json` | Exit 0; keine bekannten Schwachstellen gemeldet |
| `dotnet tool run codemetr-csharp -- check --format json` | Exit 0; 5 eigene Klassen/Dateien, keine Regelverletzung |

Der erste Build scheiterte an Roslyns Voraussetzung für die konfigurierte IDE0005-
Buildprüfung. `GenerateDocumentationFile=true` und passende XML-Kommentare beheben
die Ursache; keine Warnung unterdrückt. Danach alle genannten Prüfungen erfolgreich.
Codemetr meldet wegen fehlender NuGet-Semantik im eigenständigen Syntaxkontext
unaufgelöste xUnit-Typen (`Assert`, `Fact`, 16 Kompilationsdiagnosen im Toolkontext).
Der echte SDK-Build bestätigt fehlerfreie Kompilation; Toolmessgrenzen bleiben offengelegt.

Collector **coverlet.collector 10.1.0**, MIT laut offiziellem Nuspec, ist zentral gepinnt.
VSTest-Modus bleibt explizit aktiv; kein Wechsel zu MTP. Collector-/Scope-Smoke:

`dotnet test tests/Monstertacle.Multisession.IntegrationTests/Monstertacle.Multisession.IntegrationTests.csproj
-c Release --no-restore --no-build --settings tests/coverage.runsettings
--collect "XPlat Code Coverage" --results-directory artifacts/bootstrap-coverage`.

Exit 0; JSON-/Cobertura-Berichte erstellt. Das JSON enthält die vier referenzierten
gemeinsamen Produktassemblies mit leeren Quellmengen, obwohl der Infrastrukturtest
diese nicht verwendet. Kein ausführbarer Produktcode vorhanden, daher **keine
90-%-Coverage behauptet**. `tests/coverage-scope.json` nennt alle sechs Bibliotheken
und den jeweils nativen Plattformumfang; Tests, nichtproduktiver Host und generierte
obj-Dateien sind begründet abgegrenzt. T015 muss leere Messungen abweisen.

Setup-Sourcehash: `de91e13f644b250c029a04bd4b5eb7a20ac100bab35095b6af3444166fae2685`.
36 Manifestdateien: Root-SDK-/Build-/Paket-/Editor-/Solution-/Codemetr-Konfiguration
sowie alle eigenen `.cs`, `.csproj`, `.props`, `.runsettings` und `.json` unter
src/tests/examples einschließlich Lockfiles, ohne bin/obj/TestResults.

Keine Linux-, Windows-Server-, Live-, Crash- oder GitHub-CI-Abnahme erfolgt.
Checklisten bleiben unverändert. Das Nachweisschema und Governance-Zuständigkeiten
sind in `evidence/README.md` dokumentiert. T003-Wartungsbefund DEP-001 bleibt offen.
