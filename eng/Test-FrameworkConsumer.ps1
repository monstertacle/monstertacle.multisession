param([Parameter(Mandatory)][string]$Workspace, [Parameter(Mandatory)][string]$ProjectText, [Parameter(Mandatory)][string]$ConfigPath, [Parameter(Mandatory)][string]$Framework,
    [Parameter(Mandatory)][string]$PriorFeed, [Parameter(Mandatory)][string]$PlatformPackage)
$ErrorActionPreference = 'Stop'
$probe = Join-Path $Workspace 'framework-consumer'
$null = New-Item $probe -ItemType Directory -Force
$source = Join-Path $probe 'Program.cs'
Copy-Item (Join-Path $PSScriptRoot 'FrameworkConsumer.cs.txt') $source
$project = Join-Path $probe 'FrameworkConsumer.csproj'
[IO.File]::WriteAllText($project, $ProjectText)
& dotnet restore $project --configfile $ConfigPath --packages (Join-Path $Workspace 'packages') *> (Join-Path $probe 'restore.log')
if ($LASTEXITCODE -ne 0) { throw "Framework consumer restore failed: $probe" }
& dotnet restore $project --configfile $ConfigPath --packages (Join-Path $Workspace 'packages') --locked-mode *> (Join-Path $probe 'locked-restore.log')
if ($LASTEXITCODE -ne 0) { throw "Framework consumer locked restore failed: $probe" }
& dotnet build $project -c Release --no-restore *> (Join-Path $probe 'build.log')
if ($LASTEXITCODE -ne 0) { throw "Framework consumer build failed: $probe" }
# Preserve the root rules, but measure this independent consumer in its own compilation context.
$guard = Get-Content (Join-Path (Split-Path $PSScriptRoot -Parent) 'codemetr.json') -Raw | ConvertFrom-Json -AsHashtable
$guard.context = @($probe)
$guardPath = Join-Path $probe 'codemetr.json'
$guard | ConvertTo-Json -Depth 8 | Set-Content $guardPath
$source | & dotnet tool run codemetr-csharp -- check --files-from - --config $guardPath --format json > (Join-Path $probe 'metrics.json')
if ($LASTEXITCODE -ne 0) { throw "Framework consumer metrics failed: $probe" }
$result = Get-Content (Join-Path $probe 'metrics.json') -Raw | ConvertFrom-Json
if ($result.summary.types -ne 3 -or $result.summary.skipped -ne 0) { throw 'All three framework consumer types must be measured.' }
$trustScope = [Guid]::NewGuid().ToString("D")
$state = Join-Path $probe "state"
& dotnet (Join-Path $probe "bin/Release/$Framework/FrameworkConsumer.dll") $state $trustScope *> (Join-Path $probe 'run.log')
if ($LASTEXITCODE -ne 0) { throw "Framework consumer execution failed: $probe" }
if ((Get-Content (Join-Path $probe 'run.log') -Raw) -notmatch 'FRAMEWORK-PASS') { throw 'Missing framework qualification output.' }
& (Join-Path $PSScriptRoot "Test-PriorReaderRefusal.ps1") -Workspace $Workspace -PriorFeed $PriorFeed -StateDirectory $state -TrustScopeId $trustScope -Framework $Framework -PlatformPackage $PlatformPackage
Copy-Item (Join-Path $Workspace "prior-reader/prior-reader.json") (Join-Path $probe "prior-reader.json")
Copy-Item (Join-Path $Workspace "prior-reader/run.log") (Join-Path $probe "prior-reader-run.log")
$evidence = Join-Path $Workspace 'framework-evidence'
$null = New-Item $evidence -ItemType Directory -Force
Get-ChildItem $probe -File | ForEach-Object {
    $name = if ($_.Extension -eq '.cs') { $_.Name + '.txt' } else { $_.Name }
    Copy-Item $_.FullName (Join-Path $evidence $name)
}
Write-Host 'PASS framework package-only consumer'
