# Monstertacle.Multisession

Providerneutrale C#/.NET-Bibliotheken zur Verwaltung paralleler Agent-Sitzungen.
**In Entwicklung, nicht produktionsreif.** Dispatcher, SQLite-Persistenz, Recovery,
native Plattformimplementierungen und Herstelleradapter sind noch nicht vollständig umgesetzt.

## Entwicklungsumgebung und Prüfungen

- .NET SDK **10.0.403** gemäß `global.json`, kein implizites Roll-forward.
- PowerShell 7 (`pwsh`), Git und NuGet-Zugriff; kein Python.
- Genehmigtes lokales `Codemetr.CSharp.Tool` **0.1.0**; keine Debt-Baseline.
- Mindestens **87 %** Zeilenabdeckung des gesamten nativen Produktumfangs je Plattform.

Vom Projektroot:

```powershell
dotnet tool restore
dotnet restore Monstertacle.Multisession.slnx --locked-mode
pwsh -NoProfile -File scripts/Invoke-Quality.ps1 -Platform windows
dotnet tool run codemetr-csharp -- check --format json
```

Auf einem nativen Linuxhost `-Platform linux` verwenden. Die Prüfungen dürfen nicht
durch einen bloßen TFM-Wechsel auf einem anderen Betriebssystem ersetzt werden.
Ausführliche Regeln stehen in [AGENTS.md](AGENTS.md), der
[Constitution](.specify/memory/constitution.md) und im
[Quickstart](specs/001-agent-multisession/quickstart.md).

## Übergabe an einen Remote-Agenten

Repository: <https://github.com/monstertacle/monstertacle.multisession>.
**Nächster begrenzter Implementierungsauftrag: T025** mit den zugehörigen noch fehlenden
Red-Fällen aus T016–T019, nicht alle offenen Tasks.
T112 (ReservedDispatch-Vertrag) ist umgesetzt; Nachweise und Grenzen stehen in
[evidence/tdd-us1.md](specs/001-agent-multisession/evidence/tdd-us1.md).
Der kontrollierte Store ist weiterhin keine SQLite-Persistenz, die Runtime noch kein fertiger Dispatcher.

1. Eigenen Arbeitsbranch aus dem aktuellen Remote-Stand erstellen; keine parallelen
   Änderungen mehrerer Agenten im selben Arbeitsbaum.
2. AGENTS.md, Constitution, [Plan](specs/001-agent-multisession/plan.md),
   [Tasks](specs/001-agent-multisession/tasks.md) und den
   [Orchestrierungsvertrag](specs/001-agent-multisession/contracts/orchestration-details.md) lesen.
3. Featurekontext lokal setzen, da `.specify/feature.json` absichtlich nicht versioniert wird:

```powershell
$env:SPECIFY_FEATURE_DIRECTORY = 'specs/001-agent-multisession'
& ./.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireSpec -RequireTasks -IncludeTasks
```

Für eine Bash-gestartete Agentensitzung vorab:

```sh
export SPECIFY_FEATURE_DIRECTORY=specs/001-agent-multisession
```

Die Codex-Skills liegen bereits unter `.agents/skills/`. Bei einem Client mit dieser
Skill-Unterstützung lautet der Agentenauftrag (kein Shellbefehl):

```text
$speckit-implement Nur den nächsten T025-Dispatcherzyklus samt passenden Red-Fällen bearbeiten.
Vor Umsetzung aktuellen Deep-Dive dokumentieren,
fachliches Red vor Green beobachten, Regression, native Qualitätsprüfung und vollständigen
Codemetr-Guard ausführen. T112 wiederverwenden; keine anderen Storys umsetzen.
Änderungen und Prüfnachweise
auf einem Arbeitsbranch zur Überprüfung zurückliefern; kein Merge, Deployment oder NuGet-Push.
```

Falls der Client Skills nicht automatisch erkennt, den Skill
`.agents/skills/speckit-implement/SKILL.md` mit genau diesem begrenzten Auftrag laden.
Die projektlokalen Spec-Kit-Skripte verwenden PowerShell, auch unter Linux.

## Nachweise und Repositorygrenzen

Arbeitsnachweise liegen unter [evidence](specs/001-agent-multisession/evidence/README.md).
Die [Repository-/Clone-Prüfung](specs/001-agent-multisession/evidence/repository-bootstrap.md)
dokumentiert den getesteten Initialcommit und die Grenzen dieser lokalen Prüfung.
Rohberichte/TRX/Source-Manifeste unter `artifacts/` und `TestResults/` bleiben lokal
beziehungsweise werden als CI-Artefakte übertragen; ein frischer Clone enthält sie nicht.
Fehlende Originalartefakte dürfen nicht rekonstruiert und als historische Ausführung
ausgegeben werden. Die dokumentierte anfängliche Manifestlücke bleibt offen.

Frühere Aussagen „kein Git-Repository“ in historischen Nachweisen beschreiben den damaligen
Stand. Die Initialisierung und der erste Push wurden anschließend ausdrücklich freigegeben.
Das ist keine Erfüllung von T111/Required Checks oder der noch offenen Produktabnahmen.
SharpClaw bleibt ein späterer Konsument, OpenCode **1.19.0** eine optionale Referenz.

`.gitignore` basiert auf der [offiziellen GitHub-VisualStudio-Vorlage](https://github.com/github/gitignore/blob/main/VisualStudio.gitignore),
ergänzt um lokale Agentdaten, SQLite-Zustand und die ausdrückliche Aufnahme des Coverageinventars.
Es werden keine Zugangsdaten, nativen Sessions oder produktiven Zustandsspeicher versioniert.
