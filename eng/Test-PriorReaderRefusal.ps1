param(
    [Parameter(Mandatory)][string]$Workspace, [Parameter(Mandatory)][string]$PriorFeed, [Parameter(Mandatory)][string]$StateDirectory,
    [Parameter(Mandatory)][string]$TrustScopeId, [Parameter(Mandatory)][string]$Framework, [Parameter(Mandatory)][string]$PlatformPackage)
# Runs the prior package code, rebuilt from its exact source commit, against a store written by the current packages.
# Store opening is the first step of any composition; a refused open leaves no store for a runtime or provider to use.
$ErrorActionPreference = 'Stop'
$priorVersion = '0.2.0-alpha.2'
$probe = Join-Path $Workspace 'prior-reader'
$null = New-Item $probe -ItemType Directory -Force
Copy-Item (Join-Path $PSScriptRoot 'PriorReaderConsumer.cs.txt') (Join-Path $probe 'Program.cs')
$project = Join-Path $probe 'PriorReader.csproj'
[IO.File]::WriteAllText($project, @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType><TargetFramework>$Framework</TargetFramework>
    <Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings><LangVersion>14.0</LangVersion>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors><RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
  </PropertyGroup>
  <ItemGroup>
    <Using Include="Monstertacle.Multisession.$PlatformPackage.${PlatformPackage}StateDirectoryLease" Alias="NativeLease" />
    <PackageReference Include="Monstertacle.Multisession.Common" Version="$priorVersion" />
    <PackageReference Include="Monstertacle.Multisession.Storage.Sqlite" Version="$priorVersion" />
    <PackageReference Include="Monstertacle.Multisession.$PlatformPackage" Version="$priorVersion" />
  </ItemGroup>
</Project>
"@)
$feed = [Security.SecurityElement]::Escape((Resolve-Path -LiteralPath $PriorFeed).Path)
$config = Join-Path $probe 'NuGet.Config'
[IO.File]::WriteAllText($config, @"
<configuration>
  <packageSources><clear /><add key="prior" value="$feed" /><add key="nuget" value="https://api.nuget.org/v3/index.json" /></packageSources>
  <packageSourceMapping>
    <packageSource key="prior"><package pattern="Monstertacle.Multisession.*" /></packageSource>
    <packageSource key="nuget"><package pattern="Monstertacle.SharpQrs" /><package pattern="Microsoft.*" /><package pattern="System.*" /><package pattern="SQLitePCLRaw.*" /><package pattern="OpenTelemetry.*" /><package pattern="Google.Protobuf" /></packageSource>
  </packageSourceMapping>
</configuration>
"@)
$packages = Join-Path $probe 'packages'
& dotnet restore $project --configfile $config --packages $packages *> (Join-Path $probe 'restore.log')
if ($LASTEXITCODE -ne 0) { throw "Prior reader restore failed: $probe" }
& dotnet build $project -c Release --no-restore *> (Join-Path $probe 'build.log')
if ($LASTEXITCODE -ne 0) { throw "Prior reader build failed: $probe" }
$resolved = Get-ChildItem (Join-Path $packages 'monstertacle.multisession.storage.sqlite') -Directory | Select-Object -ExpandProperty Name
if (@($resolved) -join ',' -ne $priorVersion) { throw "Prior reader did not resolve exactly $priorVersion." }
function Get-StoreManifest {
    Get-ChildItem -LiteralPath $StateDirectory -File -Recurse | Sort-Object FullName |
        ForEach-Object { "$([IO.Path]::GetRelativePath($StateDirectory, $_.FullName)) $((Get-FileHash $_.FullName -Algorithm SHA256).Hash)" }
}
$before = Get-StoreManifest
& dotnet (Join-Path $probe "bin/Release/$Framework/PriorReader.dll") $StateDirectory $TrustScopeId *> (Join-Path $probe 'run.log')
$exit = $LASTEXITCODE
$after = Get-StoreManifest
if ($exit -ne 0 -or (Get-Content (Join-Path $probe 'run.log') -Raw) -notmatch 'PRIOR-READER-REFUSED') { throw "Prior package reader did not refuse the current store: $probe" }
if ((@($before) -join "`n") -ne (@($after) -join "`n")) { throw 'Prior package reader changed the refused store.' }
$priorHashes = Get-ChildItem -LiteralPath $PriorFeed -Filter '*.nupkg' -File | Sort-Object Name |
    ForEach-Object { [ordered]@{ File = $_.Name; Sha256 = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() } }
[ordered]@{ Status = 'PASS'; PriorVersion = $priorVersion; StoreFilesUnchanged = @($after); PriorPackages = @($priorHashes) } |
    ConvertTo-Json -Depth 5 | Set-Content (Join-Path $probe 'prior-reader.json')
Write-Host 'PASS actual prior package refuses current store'
