[CmdletBinding()]
param(
    [string]$PackageDirectory = 'artifacts/packages',
    [string]$TempRoot = (Join-Path ([Environment]::GetFolderPath('UserProfile')) 'dev/.agenttemp')
)
# Isolated base-contract conformance: restores only the packed checkpoint contract package (exact bytes, lock file, private
# package folder), implements the contract in an independent console host and checks that nothing else is resolved.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
[xml]$properties = Get-Content -LiteralPath (Join-Path $root 'Directory.Build.props') -Raw
$version = @($properties.Project.PropertyGroup.Version | Where-Object { $_ })[0]
$name = 'Monstertacle.Multisession.Checkpoints'
$package = Get-Item -LiteralPath (Join-Path $PackageDirectory "$name.$version.nupkg")
$workspace = Join-Path ([IO.Path]::GetFullPath($TempRoot)) ('checkpoints-consumer-' + [Guid]::NewGuid().ToString('N'))
$feed = Join-Path $workspace 'feed'
$null = New-Item -Path $feed -ItemType Directory -Force
Copy-Item -LiteralPath $package.FullName -Destination $feed
$source = Join-Path $workspace 'Program.cs'
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'CheckpointsConsumer.cs.txt') -Destination $source
$project = Join-Path $workspace 'ImportResume.csproj'
[IO.File]::WriteAllText($project, @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings><LangVersion>14.0</LangVersion>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors><NuGetAudit>true</NuGetAudit>
    <NuGetAuditMode>all</NuGetAuditMode><NuGetAuditLevel>low</NuGetAuditLevel>
    <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
  </PropertyGroup>
  <ItemGroup><PackageReference Include="$name" Version="[$version]" /></ItemGroup>
</Project>
"@)
$config = Join-Path $workspace 'NuGet.Config'
[IO.File]::WriteAllText($config, @"
<configuration>
  <packageSources><clear /><add key="checkpoints" value="$([Security.SecurityElement]::Escape($feed))" /></packageSources>
  <packageSourceMapping>
    <packageSource key="checkpoints"><package pattern="$name" /></packageSource>
  </packageSourceMapping>
</configuration>
"@)
function Invoke-Consumer([string[]]$Arguments, [string]$Log) {
    & dotnet @Arguments *> (Join-Path $workspace $Log)
    if ($LASTEXITCODE -ne 0) { throw "Checkpoints consumer step failed: $Log; workspace: $workspace" }
}
$packages = Join-Path $workspace 'packages'
$evidence = Join-Path $root ('artifacts/checkpoints-consumer/' + (Split-Path $workspace -Leaf))
try {
    Invoke-Consumer @('restore', $project, '--configfile', $config, '--packages', $packages, '--use-lock-file') 'restore.log'
    Invoke-Consumer @('restore', $project, '--configfile', $config, '--packages', $packages, '--locked-mode') 'locked-restore.log'
    $lock = Get-Content -Raw -LiteralPath (Join-Path $workspace 'packages.lock.json') | ConvertFrom-Json -AsHashtable
    $resolved = @($lock.dependencies['net10.0'].Keys)
    # The base package must stand alone: no delivery, SQLite, session, agent-framework or host package may come with it.
    if ($resolved.Count -ne 1 -or $resolved[0] -ne $name) { throw "The base package resolved more than itself: $($resolved -join ', ')" }
    $entry = $lock.dependencies['net10.0'][$name]
    $sha512 = [Convert]::ToBase64String([Security.Cryptography.SHA512]::HashData([IO.File]::ReadAllBytes($package.FullName)))
    if ($entry.type -ne 'Direct' -or $entry.resolved -ne $version -or $entry.contentHash -ne $sha512) { throw 'The lock file does not bind the exact packed base package.' }
    Invoke-Consumer @('build', $project, '-c', 'Release', '--no-restore') 'build.log'
    $source | & dotnet tool run codemetr-csharp -- check --files-from - --format json > (Join-Path $workspace 'codemetr.json')
    if ($LASTEXITCODE -ne 0) { throw "Consumer Codemetr failed: $workspace" }
    $guard = Get-Content -Raw -LiteralPath (Join-Path $workspace 'codemetr.json') | ConvertFrom-Json
    if ($guard.summary.types -lt 2 -or $guard.summary.skipped -ne 0) { throw 'Expected the consumer classes to be measured.' }
    Invoke-Consumer @((Join-Path $workspace 'bin/Release/net10.0/ImportResume.dll')) 'report.json'
    $report = Get-Content -Raw -LiteralPath (Join-Path $workspace 'report.json') | ConvertFrom-Json
    $expected = @('typed-read', 'released-once', 'cancelled-read-refused-and-released')
    if ($report.Status -ne 'PASS' -or @(Compare-Object $expected @($report.Cases | Where-Object Passed | ForEach-Object Name)).Count -ne 0) {
        throw 'Incomplete checkpoint contract consumer conformance.'
    }
    [ordered]@{ Status = 'PASS'; Platform = $(if ($IsWindows) { 'windows' } else { 'linux' }); CompletedUtc = [DateTimeOffset]::UtcNow.ToString('O')
        Package = [ordered]@{ File = $package.Name; Sha256 = (Get-FileHash -LiteralPath $package.FullName -Algorithm SHA256).Hash.ToLowerInvariant(); LockContentHash = $sha512 }
        Consumer = $report } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $workspace 'manifest.json')
    Write-Host "PASS checkpoint contract package consumer: $workspace"
} finally {
    $null = New-Item -Path $evidence -ItemType Directory -Force
    foreach ($file in @('manifest.json', 'report.json', 'codemetr.json', 'ImportResume.csproj', 'NuGet.Config', 'packages.lock.json', 'restore.log', 'locked-restore.log', 'build.log')) {
        $path = Join-Path $workspace $file
        if (Test-Path -LiteralPath $path) { Copy-Item -LiteralPath $path -Destination $evidence }
    }
}
