# Foundation — laufender Nachweis

**Stand**: 2026-10-28. Implementierung teilweise vorhanden; keine Produktfreigabe.

## SDK-Änderung mit ausdrücklicher Genehmigung

Beim Wiederaufnehmen war 10.0.402 nicht mehr installiert. `dotnet --version`
scheiterte zunächst am exakten Pin; verfügbar waren 10.0.303 und 10.0.403.
Der Projektverantwortliche bestätigte „10.0.403 nutzen“. `global.json`, aktueller
Plan, Tasks, Quickstart, Research und Skill wurden entsprechend aktualisiert,
ohne Roll-forward, Paketupgrade oder SDK-Installation. Historische 10.0.402-
Nachweise bleiben unverändert. OpenCode bleibt 1.19.0.

`dotnet --version` liefert danach 10.0.403. Locked-Restore erfolgreich.
`dotnet test tests/Monstertacle.Multisession.UnitTests/Monstertacle.Multisession.UnitTests.csproj
-c Release --no-restore --logger "trx;LogFileName=resume-sdk-403.trx"`:
76/76 bestehende Unit-Tests erfolgreich.

## Gemeinsame Verträge (T009/T010)

Öffentliche Runtime-/Facettensignaturen und fachliche atomare Storeoperationen sind
vorhanden und kompilieren. ISessionQueryStore trennt begrenzte fachliche Reads vom
Schreibvertrag, damit wachsende Storeoperationen nicht zu generischem CRUD werden.
SessionRuntime selbst enthält weiterhin NotImplementedException und ist kein fertiger
Host. Providervertrag ist transportneutral: Fähigkeiten, ExecutionKind, Workspace,
opaque Binding und awaitable Beobachtungen; kein HTTP-/SDK-/SharpClaw-Typ im Common-Kern.
Vollständige Verhaltensabnahme dieser Verträge bleibt T035/T089 und weiteren Tasks vorbehalten.

T011 ist **teilweise** umgesetzt: kontrollierter Provider mit expliziten Barrieren,
Lease-/Zeitdoubles und ein partieller In-Memory-Store. Der Zeitdouble besitzt noch
keine kontrollierten Timer; Request-/Recoveryoperationen des Stores sind Gerüste.
Keine Produktpersistenz oder native Berechtigungs-/Besitzprüfung behauptet.

## Tatsächlich beobachtete initiale Zyklen (2026-10-27)

Projektbefehl jeweils `dotnet test tests/Monstertacle.Multisession.UnitTests/Monstertacle.Multisession.UnitTests.csproj
-c Release --no-restore --filter FullyQualifiedName~<Testklasse>
--logger "trx;LogFileName=<datei>.trx"`.
Lokale TRX liegen unter dem Testprojekt in TestResults; Infrastrukturfehler wurden
nicht als Red ausgegeben. Die folgende Aufstellung ist ein Nachtrag anhand der
tatsächlichen Ausführungen, keine behauptete spätere Vorabanalyse.

| Verhalten / Bezug T012–T014 | Fachliches Red | Nachfolgendes Green / TRX |
| --- | --- | --- |
| Ungültige StateDirectory | 5 Assertions: erwartet Fehler, tatsächlich null | 5/5; options-path-red / options-path-green |
| Kapazitäts-/Fristdefaults | 9 einzelne Fälle: erwartet dokumentierter Wert, tatsächlich 0 bzw. TimeSpan.Zero | 15/15 mit bisheriger Regression; options-default-cases-red / options-defaults-green |
| Positive endliche Limits und DbBusyTimeout < ShutdownTimeout | 28 neue Abweisungen fehlten | 43/43; options-limits-red / options-limits-green |
| Explizite Retention und Netzwerkpfade | 8 neue Abweisungen fehlten; zwei positive Grenzfälle bereits grün | 53/53; options-retention-path-red / options-retention-path-green |
| Inklusive UTF-8-Bytegrenzen | 12 fehlende Abweisungen für leere Texte, Ein-Byte-Exzess und ungültige Limits | UTF8 17/17; utf8-all-cases-red / foundation-green |

PublicError-Datenträgerprüfungen waren bereits grüne Regressionen: keine erfundenen
Red-Nachweise für triviale passive Wertträger. Der Fehlerdatenträger hat keine rohen
Providertexte. Seine spätere operative Ausnahmeabbildung wird in den neuen
SessionOpeningTests tatsächlich Red/Green geprüft, siehe tdd-us1.md.

Initial protokollierte Red-Dateihashes unter anderem:
RuntimeOptions/path `7BD98691FCEA90A982147D95AA5B4C59FA862122739F856CE7675369E5F9DDAB`,
RuntimeOptionsTests/path `DC9A35A8845C06B32EC64604E76D3B8C318589AC9E05FD7607B877FA4DE57D4A`,
RuntimeOptions/limits `52A8CA361BF86FE768ED175714EFD8BE16830717BAAA96EF2A7FEF234DD7F375`,
RuntimeOptionsTests/limits `2D2AAA72873FAB1515EEA5E86CD64009627E54FD12C2D65E9DF3EF994BA66F02`.
Es existiert nicht für jede initiale Iteration ein vollständiges archiviertes
Source-Manifest. Das bleibt beim abschließenden Q-06-Audit offenzulegen; keine
vollständige historische Beweiskette oder nachträgliche frühere Testausführung erfinden.
Die neuen Zyklen vom 2026-10-28 enthalten jeweils vor dem Lauf angelegte sources.json.

## Regression und Refactoring am 2026-10-28

Die bestehende Pfadregel wurde nach 53/53 grünen RuntimeOptionsTests in
LocalDirectoryPath.IsValid extrahiert und für Workspacezulassung wiederverwendet.
Die anschließende vollständige Regression blieb grün. Keine neue Ausschlussregel,
Warnungsunterdrückung oder Debt-Baseline wurde eingeführt.

Letzter vollständiger Windows-Lauf: 102 Unit-Tests plus je ein Contract-/Integration-/
Platform-Infrastrukturtest, alle erfolgreich, 0 übersprungen. Formatprüfung Exit 0,
Release-Build 0 Warnungen/0 Fehler. Codemetr 0.1.0: 59 eigene Dateien/Typen,
0 Regelverletzungen, Exit 0. Seine bekannten NuGet-/Semantikgrenzen bleiben bestehen;
ein erfolgreicher SDK-Build ersetzt nicht die Bewertung nicht auflösbarer Fremdtypen.

T014 ist damit fachlich geprüft und dokumentiert, die initiale Auditlücke bleibt
explizit. T015-Konfiguration und elf Gate-Smokes sind vorhanden; die tatsächliche
Coverage liegt noch unter 90 % und reale Linux-/GitHub-CI-Nachweise fehlen.
Siehe coverage.md. Keine Phase oder Story als vollständig freigegeben dargestellt.
