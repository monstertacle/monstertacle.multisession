# Codemetr: Genehmigung und Setupnachweis

**Datum**: 2026-10-27. **T001**: erledigt. **Q-01 Produktprüfung**: NOT RUN.

## Ausdrückliche Entscheidungen des Projektverantwortlichen

- Zustimmung im Dialog: „Ja, verbindlich einrichten“ zu offiziellem Guard,
  lokal gepinntem `Codemetr.CSharp.Tool` 0.1.0, NuGet-Installation und dokumentierten
  Standardgrenzwerten ohne Debt-Baseline oder abgeschwächte Regeln.
- SDK-Entscheidung: „Vorhandenes 10.0.402 nutzen“ statt des geplanten 10.0.403.
- Constitution v1.6.0 bildet diese Entscheidung ab; das ursprüngliche Ratifikationsdatum
  wurde dadurch nicht bestätigt und bleibt offen.

## Prüfer, Regeln und Messumfang

- Offizielles Paket: `Codemetr.CSharp.Tool` 0.1.0, Befehl `codemetr-csharp` aus
  `codemetr/Codemetr.CSharp`. Lokal gepinnt in `.config/dotnet-tools.json`.
- Genehmigte Regeln: `codemetr.json`, alle dokumentierten Standardgrenzwerte,
  leere Overrides und zusätzliche Ausschlüsse; keine `codemetr.baseline.json`.
- Ganzer Projektroot einschließlich eigener Test-/Hostklassen, keine bloße Diff-Prüfung.
  Automatische Toolausschlüsse (bin/obj, generierte Dateien usw.), inaktive Präprozessor-
  Zweige und unaufgelöste externe Typen sind Messgrenzen und im Review offenzulegen.
- OpenCode-Adaption des offiziellen Guard-Skills aus `codemetr/skills`, Plugin 0.2.0,
  Quellcommit `b3d1f948e536fe18903ed9e4fcad5962ec0e0a88`.
  `.opencode/skills/codemetr-guard/SKILL.md` enthält Herkunft und projektlokalen Ablauf;
  Upstream-MIT-Lizenz ist beigelegt. Keine Installation eines Claude-Code-Plugins behauptet.
- Full-Check vom Projektroot:
  `dotnet tool run codemetr-csharp -- check --format json`.
  Exit 0 gilt nur für gemessenen Code; Exit 1/2 blockiert. Null gemessene Klassen
  ist kein Produktnachweis. Nach Refactoring Tests und Full-Check wiederholen.

## Tatsächlich ausgeführte Setupvalidierung (Windows)

| Prüfung | Tatsächliches Ergebnis |
| --- | --- |
| `dotnet --info` | SDKs 10.0.402 und 10.0.303 vorhanden; 10.0.403 fehlt |
| `dotnet --version` nach `global.json` | 10.0.402 |
| `dotnet tool restore` | Exit 0; Tool 0.1.0 erfolgreich wiederhergestellt |
| `dotnet tool run codemetr-csharp -- --help` | Exit 0; Befehle und Exitcodes bestätigt |
| Full-Check am jetzigen Root | Exit 0, Version 0.1.0, 0 Dateien/0 Typen; ausdrücklich KEIN Produkt-PASS |
| Temporäre eigene saubere C#-Fixture mit Projektkonfiguration | Exit 0, 1 Datei/1 Typ, 0 Befunde |
| Temporäre eigene `SampleHelper`-Fixture mit Projektkonfiguration | Exit 1, 1 Datei/1 Typ, 1 Befund (verbotenes Suffix) |
| Temporäre eigene syntaktisch defekte C#-Fixture | Exit 2, 1 Parsefehler |
| CI-Prädikat gegen leeren Root-Bericht | Leere Messung wird wie vorgesehen abgewiesen |
| JSON-Parsing aller drei Konfigurationsdateien | Erfolgreich |

Fixtureaufrufe: `dotnet tool run codemetr-csharp -- check --root <temporärer Fallordner>
--config <absoluter Projektpfad>/codemetr.json --format json`.
Fälle: einfache `Sample.Add`-Klasse; identische Klasse namens `SampleHelper`;
unvollständige Klassendeklaration. Fixtures lagen außerhalb des Projektquellbaums und
wurden nach der Prüfung entfernt. Diese Infrastruktur-Smokes sind keine Produkt-/TDD-Tests.

## CI und verbleibende Freigaben

`.github/workflows/codemetr-guard.yml` führt Restore und Full-Check unter Ubuntu/Windows
aus, erhält Fehlerexitcodes und weist leere Messungen zusätzlich ab.
Der lokale Skill ist vom Harness bereits als verfügbar aufgelistet worden.

- Echte GitHub-CI-Läufe und erforderliche Branchchecks: NOT RUN / nicht eingerichtet,
  da dieses Arbeitsverzeichnis noch kein angebundenes Git-Repository ist.
- Eigene Produktklassen/Tests existieren noch nicht: Q-01 bleibt bis T102 offen.
- Coverage-Collector, Aggregation und 90-%-Gate werden in T005/T015 implementiert und
  in T104 nachgewiesen; der Codemetr-Check misst keine Testabdeckung.
- Keine Produktfreigabe, Live-Provider-Abnahme oder Linux-Ausführung behauptet.

## Implementierungsbereitschaft

`.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks`
wurde erfolgreich ausgeführt und findet das Feature sowie Research, Datenmodell,
Verträge, Quickstart und Tasks. Checklisten ausschließlich gelesen, nicht verändert:
`requirements.md`: 16 erledigt / 0 offen; `multisession.md`: 40 erledigt / 0 offen.
Damit kann `/speckit.implement` ab T002 fortfahren. Die übrigen Implementierungs- und
Freigabeaufgaben sind dadurch nicht erledigt.
