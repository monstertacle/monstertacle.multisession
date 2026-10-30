# Quickstart
Resume the SpecKit context in a fresh checkout:
```powershell
$env:SPECIFY_FEATURE_DIRECTORY = "specs/002-sharpqrs-cqrs"
pwsh .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks
```
The workflow persists this selection locally in ignored `.specify/feature.json`.

After normal SessionRuntime.StartAsync:
```csharp
using Monstertacle.Multisession.Cqrs;
var result = await runtime.Queries.DispatchAsync(new GetSessionQuery(sessionId), token);
var receipt = await runtime.Commands.DispatchAsync(new SubmitRequestCommand(request), token);
```
Existing runtime.Sessions/Requests/Recovery APIs remain available and traverse the same CQRS routes.
`dotnet tool restore` restores both static metrics and coverage tools. Run `pwsh scripts/Invoke-Quality.ps1` natively on Linux or Windows for coverage and `dotnet tool run codemetr-csharp -- check --format json` for static metrics.

