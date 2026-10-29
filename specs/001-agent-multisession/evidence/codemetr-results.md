# T102: vollständiger Codemetr-Nachweis

Messstand 2026-10-29T22:48:18Z. Genehmigung: [codemetr-approval.md](codemetr-approval.md).
SDK 10.0.403, lokales Werkzeug Codemetr.CSharp.Tool 0.1.0.
Befehl vom Repositoryroot: `dotnet tool run codemetr-csharp -- check --format json`,
über `bash ~/dev/scripts/run-capped.sh -- ...`, Exit 0.
Sourcehash: `dba419f35bf40fd7282a5b2f3bfe7c685576e2c62ceb570d5fec2d0f4f84498b`.

440 eigene C#-Dateien, 458 Typen einschließlich Tests, ValidationHost, beiden
Plattformbibliotheken und gemeinsam verlinkten NativeHttp-Quellen. Null Verstöße,
neue/verschlechterte/baselined/stale Befunde, Parsefehler und übersprungene Dateien.
Rohbericht: `artifacts/tdd/native-abi-codemetr.json`.
Konfiguration: context=["."], exclude=[], overrides=[], keine Debt-Baseline.
Grenzen: Typzeilen 600, Konstruktorparameter 6, öffentliche Methoden 10, Felder 8,
zyklomatische Komplexität 25, MI mindestens 15, Kopplung 40, Vererbung 3,
Memberparameter 7, Verschachtelung 4; verbotene Suffixe unverändert.

Hotspots sind advisory und nicht verschwiegen: DiskFullWorker.RunAsync Komplexität23,
MI34, Kopplung37; ControlledRecoveryState.Resolve Komplexität22, MI27, Kopplung29;
WaveProbeFlow.ExecuteAsync Komplexität21, MI36, Kopplung30. Alle innerhalb der
genehmigten Grenzwerte. Eine zusätzliche Schicht allein zum Senken dieser Werte
ist nicht begründet. Messung ersetzt weder dynamische Tests noch Sicherheitsreview
oder Coverage. Native Gegenprüfung und finaler Lieferstand: [build-audit.md](build-audit.md).

Native Bestätigung40968636533: beide OS-Checks erfolgreich, jeweils440Dateien/
458Typen,0Verstöße/Parsefehler/Skips. Vollständiges Originallog lokal unter
artifacts/tdd/final-native-guard.log; derselbe Sourcehash wie build-native.json.

## Explizite semantische Messgrenze

Der Bericht nennt completeness.scope="selected types plus available measurable
partial parts; platform references only". Er enthält5309 interne Compilation-
Diagnosen (CS0029/CS0103/CS0165/CS0173/CS0234/CS0246/CS1061/CS1503/CS1729/CS8795),
unter anderem unaufgelöste xUnit-, Sqlite-, ILogger-/EventId-/FakeTimeProvider- und
NativeLease-Typen. UnmeasuredPartialDeclarations, inactiveFiles und
contextFilesWithLimits sind jeweils leer. Das gepinnte0.1.0 lädt damit nicht den
vollständigen MSBuild-/NuGet-Semantikgraphen. Null Parsefehler ist ausdrücklich
keine Behauptung von null semantischen Diagnosen oder vollständiger Fremdtypkopplung.
Die eigenen Dateien/Typen und syntaktischen Metriken sind gemessen; semantische
Kopplungs-/Abhängigkeitsaussagen bleiben auf auflösbare Typen begrenzt.
Echte Releasebuilds und alle nativen Tests kompilieren unabhängig erfolgreich;
die5309 Tooldiagnosen sind keine5309 SDK-Buildfehler. Diese bekannte Werkzeuggrenze
wird weder per Baseline noch Upgrade/Scopeverkleinerung versteckt. Der Bericht ist
das tatsächliche genehmigte0.1.0-Guardurteil, kein weitergehender Qualitätsbeweis.

