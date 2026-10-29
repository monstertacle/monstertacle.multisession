[CmdletBinding()]
param(
    [string]$PackageDirectory = 'artifacts/packages',
    [string]$TempRoot = (Join-Path ([Environment]::GetFolderPath('UserProfile')) 'dev/.agenttemp')
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$feed = (Resolve-Path -LiteralPath $PackageDirectory).Path
& (Join-Path $PSScriptRoot 'Test-PackageLayout.ps1') -PackageDirectory $feed
$workspace = Join-Path ([IO.Path]::GetFullPath($TempRoot)) ('multisession-package-smoke-' + [Guid]::NewGuid().ToString('N'))
$null = [IO.Directory]::CreateDirectory((Split-Path $workspace -Parent))
if ($IsWindows) {
    $owner = [Security.Principal.WindowsIdentity]::GetCurrent().User
    $security = [Security.AccessControl.DirectorySecurity]::new()
    $security.SetOwner($owner)
    $security.SetAccessRuleProtection($true, $false)
    $security.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($owner, 'FullControl', 'ContainerInherit, ObjectInherit', 'None', 'Allow'))
    [IO.FileSystemAclExtensions]::Create([IO.DirectoryInfo]::new($workspace), $security)
} else {
    $null = [IO.Directory]::CreateDirectory($workspace, [IO.UnixFileMode]::UserRead -bor [IO.UnixFileMode]::UserWrite -bor [IO.UnixFileMode]::UserExecute)
}
$framework = if ($IsWindows) { 'net10.0-windows' } else { 'net10.0' }
$platformPackage = if ($IsWindows) { 'Windows' } else { 'Linux' }
$template = Join-Path $root 'tests/Monstertacle.Multisession.IntegrationTests/Fixtures/PackageConsumer.cs.txt'
$consumerSource = Join-Path $workspace 'Program.cs'
[IO.File]::Copy($template, $consumerSource)
$project = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType><TargetFramework>$framework</TargetFramework>
    <Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings><LangVersion>14.0</LangVersion>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors><NuGetAudit>true</NuGetAudit>
    <NuGetAuditMode>all</NuGetAuditMode><NuGetAuditLevel>low</NuGetAuditLevel>
    <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
  </PropertyGroup>
  <ItemGroup>
    <Using Include="Monstertacle.Multisession.$platformPackage.${platformPackage}StateDirectoryLease" Alias="NativeLease" />
    <PackageReference Include="Monstertacle.Multisession.Common" Version="0.1.0" />
    <PackageReference Include="Monstertacle.Multisession.Storage.Sqlite" Version="0.1.0" />
    <PackageReference Include="Monstertacle.Multisession.$platformPackage" Version="0.1.0" />
    <PackageReference Include="Monstertacle.Multisession.Providers.OpenAI" Version="0.1.0" />
    <PackageReference Include="Monstertacle.Multisession.Providers.Gemini" Version="0.1.0" />
  </ItemGroup>
</Project>
"@
$projectPath = Join-Path $workspace 'Consumer.csproj'
[IO.File]::WriteAllText($projectPath, $project)
$escapedFeed = [Security.SecurityElement]::Escape($feed)
$config = @"
<configuration>
  <packageSources><clear /><add key="local" value="$escapedFeed" /><add key="nuget" value="https://api.nuget.org/v3/index.json" /></packageSources>
  <packageSourceMapping>
    <packageSource key="local"><package pattern="Monstertacle.*" /></packageSource>
    <packageSource key="nuget"><package pattern="Microsoft.*" /><package pattern="System.*" /><package pattern="SQLitePCLRaw.*" /></packageSource>
  </packageSourceMapping>
</configuration>
"@
$configPath = Join-Path $workspace 'NuGet.Config'
[IO.File]::WriteAllText($configPath, $config)
function Invoke-SmokeDotnet([string[]]$Arguments, [string]$Log) {
    & dotnet @Arguments *> (Join-Path $workspace $Log)
    if ($LASTEXITCODE -ne 0) { throw "Package smoke failed: $Log; workspace: $workspace" }
}
Push-Location $root
try {
    Invoke-SmokeDotnet @('restore', $projectPath, '--configfile', $configPath, '--packages', (Join-Path $workspace 'packages'), '--use-lock-file') 'restore.log'
    Invoke-SmokeDotnet @('restore', $projectPath, '--configfile', $configPath, '--packages', (Join-Path $workspace 'packages'), '--locked-mode') 'locked-restore.log'
    Invoke-SmokeDotnet @('build', $projectPath, '-c', 'Release', '--no-restore') 'build.log'
    $consumerSource | & dotnet tool run codemetr-csharp -- check --files-from - --format json > (Join-Path $workspace 'codemetr.json')
    if ($LASTEXITCODE -ne 0) { throw "Consumer Codemetr failed: $workspace" }
    $guard = Get-Content -Raw -LiteralPath (Join-Path $workspace 'codemetr.json') | ConvertFrom-Json
    if ($guard.summary.types -ne 2 -or $guard.summary.skipped -ne 0) { throw 'Expected both consumer classes to be measured.' }
    Invoke-SmokeDotnet @((Join-Path $workspace "bin/Release/$framework/Consumer.dll"), (Join-Path $workspace 'state')) 'report.json'
    $report = Get-Content -Raw -LiteralPath (Join-Path $workspace 'report.json') | ConvertFrom-Json
    if ($report.Status -ne 'PASS' -or $report.Requests -ne 5 -or $report.ReopenedRequests -ne 5) { throw 'Incomplete public consumer proof.' }
    $hashes = @((Get-ChildItem -LiteralPath $feed -Filter '*.nupkg' -File) + (Get-Item -LiteralPath $template, $consumerSource, $projectPath, $configPath) |
        ForEach-Object { [ordered]@{ File = $_.Name; Sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() } })
    [ordered]@{ Status = 'PASS'; Platform = $platformPackage; CompletedUtc = [DateTimeOffset]::UtcNow.ToString('O'); Workspace = $workspace; Hashes = $hashes; Consumer = $report } |
        ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $workspace 'manifest.json')
    $reports = Join-Path $root ('artifacts/package-smoke/' + (Split-Path $workspace -Leaf))
    $null = New-Item -Path $reports -ItemType Directory -Force
    foreach ($name in @('manifest.json', 'codemetr.json', 'report.json', 'Program.cs', 'Consumer.csproj', 'NuGet.Config', 'packages.lock.json', 'restore.log', 'locked-restore.log', 'build.log')) {
        Copy-Item -LiteralPath (Join-Path $workspace $name) -Destination $reports
    }
    Write-Host "PASS package consumer: $workspace"
} finally { Pop-Location }
