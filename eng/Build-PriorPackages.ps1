param([Parameter(Mandatory)][string]$Workspace, [string]$Commit = '6127f2ec6bfcb279c6204c1de6e5e81b4a99bb3e')
# Rebuilds the prior producer (0.2.0-alpha.2) from its exact source commit with that tree's own global.json;
# no checked-in binary is trusted. Bytes may differ from earlier builds; the refused code is the prior source.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$source = Join-Path $Workspace 'prior-source'
$feed = Join-Path $Workspace 'prior-feed'
$null = New-Item $source, $feed -ItemType Directory -Force
$archive = Join-Path $Workspace 'prior-source.tar'
& git -C $root archive --format=tar -o $archive $Commit
if ($LASTEXITCODE -ne 0) { throw "Prior source $Commit is unavailable." }
& tar -xf $archive -C $source
if ($LASTEXITCODE -ne 0) { throw 'Prior source extraction failed.' }
$platform = if ($IsWindows) { 'Windows' } else { 'Linux' }
Push-Location $source
try {
    foreach ($name in @('Common', 'Storage.Sqlite', $platform)) {
        $project = Join-Path $source "src/Monstertacle.Multisession.$name/Monstertacle.Multisession.$name.csproj"
        & dotnet pack $project -c Release -o $feed -p:ContinuousIntegrationBuild=true *> (Join-Path $Workspace "prior-pack-$name.log")
        if ($LASTEXITCODE -ne 0) { throw "Prior package build failed for $name." }
    }
} finally {
    Pop-Location
}
[ordered]@{ Commit = $Commit; ArchiveSha256 = (Get-FileHash $archive -Algorithm SHA256).Hash.ToLowerInvariant() } |
    ConvertTo-Json | Set-Content (Join-Path $feed 'prior-source.json')
return $feed
