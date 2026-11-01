[CmdletBinding()]
param(
    [string]$PackageDirectory = 'artifacts/packages',
    [string]$TempRoot = (Join-Path ([Environment]::GetFolderPath('UserProfile')) 'dev/.agenttemp')
)
# Isolated host conformance: restores only the packed delivery package (exact bytes, lock file, private package folder),
# builds an independent console host with its own payloads and policy and checks its typed outcomes.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
[xml]$properties = Get-Content -LiteralPath (Join-Path $root 'Directory.Build.props') -Raw
$version = @($properties.Project.PropertyGroup.Version | Where-Object { $_ })[0]
$name = 'Monstertacle.Multisession.Delivery'
$package = Get-Item -LiteralPath (Join-Path $PackageDirectory "$name.$version.nupkg")
$workspace = Join-Path ([IO.Path]::GetFullPath($TempRoot)) ('delivery-consumer-' + [Guid]::NewGuid().ToString('N'))
$feed = Join-Path $workspace 'feed'
$null = New-Item -Path $feed -ItemType Directory -Force
Copy-Item -LiteralPath $package.FullName -Destination $feed
$source = Join-Path $workspace 'Program.cs'
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'DeliveryConsumer.cs.txt') -Destination $source
$project = Join-Path $workspace 'InvoiceOutbox.csproj'
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
  <packageSources><clear /><add key="delivery" value="$([Security.SecurityElement]::Escape($feed))" /><add key="nuget" value="https://api.nuget.org/v3/index.json" /></packageSources>
  <packageSourceMapping>
    <packageSource key="delivery"><package pattern="$name" /></packageSource>
    <packageSource key="nuget"><package pattern="Microsoft.*" /><package pattern="System.*" /><package pattern="SQLitePCLRaw.*" /></packageSource>
  </packageSourceMapping>
</configuration>
"@)
function Invoke-Consumer([string[]]$Arguments, [string]$Log) {
    & dotnet @Arguments *> (Join-Path $workspace $Log)
    if ($LASTEXITCODE -ne 0) { throw "Delivery consumer step failed: $Log; workspace: $workspace" }
}
$packages = Join-Path $workspace 'packages'
$evidence = Join-Path $root ('artifacts/delivery-consumer/' + (Split-Path $workspace -Leaf))
try {
    Invoke-Consumer @('restore', $project, '--configfile', $config, '--packages', $packages, '--use-lock-file') 'restore.log'
    Invoke-Consumer @('restore', $project, '--configfile', $config, '--packages', $packages, '--locked-mode') 'locked-restore.log'
    $lock = Get-Content -Raw -LiteralPath (Join-Path $workspace 'packages.lock.json') | ConvertFrom-Json -AsHashtable
    $entry = $lock.dependencies['net10.0'][$name]
    $sha512 = [Convert]::ToBase64String([Security.Cryptography.SHA512]::HashData([IO.File]::ReadAllBytes($package.FullName)))
    if ($entry.resolved -ne $version -or $entry.contentHash -ne $sha512) { throw 'The lock file does not bind the exact packed delivery package.' }
    $direct = @($lock.dependencies['net10.0'].Keys | Where-Object { $lock.dependencies['net10.0'][$_].type -eq 'Direct' })
    if ($direct.Count -ne 1 -or $direct[0] -ne $name) { throw 'The consumer must reference the delivery package only.' }
    if (@($lock.dependencies['net10.0'].Keys | Where-Object { $_ -like 'SharpClaw*' -or $_ -like 'Monstertacle.Multisession.Common*' -or $_ -like 'Microsoft.Agents*' }).Count -ne 0) {
        throw 'The delivery package pulled a host, session-runtime or agent-framework dependency.'
    }
    Invoke-Consumer @('build', $project, '-c', 'Release', '--no-restore') 'build.log'
    $source | & dotnet tool run codemetr-csharp -- check --files-from - --format json > (Join-Path $workspace 'codemetr.json')
    if ($LASTEXITCODE -ne 0) { throw "Consumer Codemetr failed: $workspace" }
    $guard = Get-Content -Raw -LiteralPath (Join-Path $workspace 'codemetr.json') | ConvertFrom-Json
    if ($guard.summary.types -lt 2 -or $guard.summary.skipped -ne 0) { throw 'Expected the consumer classes to be measured.' }
    Invoke-Consumer @((Join-Path $workspace 'bin/Release/net10.0/InvoiceOutbox.dll'), (Join-Path $workspace 'state')) 'report.json'
    $report = Get-Content -Raw -LiteralPath (Join-Path $workspace 'report.json') | ConvertFrom-Json
    $expected = @('accepted-advances-position', 'second-owner-refused', 'crash-in-sending-is-uncertain', 'uncertain-blocks-automatic-send',
        'resend-authorized-once-atomically', 'second-resend-refused', 'attempt-grant-and-stale-key', 'route-move-recorded', 'moved-custody-intact')
    if ($report.Status -ne 'PASS' -or @(Compare-Object $expected @($report.Cases | Where-Object Passed | ForEach-Object Name)).Count -ne 0) {
        throw 'Incomplete delivery consumer conformance.'
    }
    [ordered]@{ Status = 'PASS'; Platform = $(if ($IsWindows) { 'windows' } else { 'linux' }); CompletedUtc = [DateTimeOffset]::UtcNow.ToString('O')
        Package = [ordered]@{ File = $package.Name; Sha256 = (Get-FileHash -LiteralPath $package.FullName -Algorithm SHA256).Hash.ToLowerInvariant(); LockContentHash = $sha512 }
        Consumer = $report } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $workspace 'manifest.json')
    Write-Host "PASS delivery package consumer: $workspace"
} finally {
    $null = New-Item -Path $evidence -ItemType Directory -Force
    foreach ($file in @('manifest.json', 'report.json', 'codemetr.json', 'InvoiceOutbox.csproj', 'NuGet.Config', 'packages.lock.json', 'restore.log', 'locked-restore.log', 'build.log')) {
        $path = Join-Path $workspace $file
        if (Test-Path -LiteralPath $path) { Copy-Item -LiteralPath $path -Destination $evidence }
    }
}
