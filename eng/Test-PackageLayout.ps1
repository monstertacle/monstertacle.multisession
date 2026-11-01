param([string]$PackageDirectory = 'artifacts/packages')
$ErrorActionPreference = 'Stop'
# The expected identity comes from the producing source, so a version change cannot pass against stale literals.
[xml]$properties = Get-Content -LiteralPath (Join-Path (Split-Path $PSScriptRoot -Parent) 'Directory.Build.props') -Raw
$version = @($properties.Project.PropertyGroup.Version | Where-Object { $_ })[0]
if ([string]::IsNullOrWhiteSpace($version)) { throw 'Directory.Build.props declares no package version.' }
$license = [IO.File]::ReadAllText((Join-Path (Split-Path $PSScriptRoot -Parent) 'LICENSE')).Replace("`r`n", "`n")
if (-not $license.StartsWith('MIT License')) { throw 'The repository LICENSE is not the MIT license.' }
$names = @(
    'Monstertacle.Multisession.Common',
    'Monstertacle.Multisession.AgentFramework',
    'Monstertacle.Multisession.Linux',
    'Monstertacle.Multisession.Windows',
    'Monstertacle.Multisession.Storage.Sqlite',
    'Monstertacle.Multisession.Providers.Conversations',
    'Monstertacle.Multisession.Providers.Interactions'
)
$packages = @(Get-ChildItem -LiteralPath $PackageDirectory -Filter '*.nupkg' -File)
if ($packages.Count -ne $names.Count) { throw 'Expected exactly seven library packages.' }
foreach ($name in $names) {
    $package = @($packages | Where-Object Name -EQ "$name.$version.nupkg")
    if ($package.Count -ne 1) { throw "Missing expected package: $name $version" }
    $zip = [System.IO.Compression.ZipFile]::OpenRead($package[0].FullName)
    try {
        $entry = $zip.GetEntry("$name.nuspec")
        if ($null -eq $entry) { throw "Missing nuspec: $name" }
        $reader = [System.IO.StreamReader]::new($entry.Open())
        try { [xml]$specification = $reader.ReadToEnd() } finally { $reader.Dispose() }
        $metadata = $specification.package.metadata
        if ($metadata.id -ne $name -or $metadata.version -ne $version) { throw "Wrong identity/version: $name" }
        if ($metadata.readme -ne 'README.md' -or $null -eq $zip.GetEntry('README.md')) { throw "Missing packaged README: $name" }
        if ([string]::IsNullOrWhiteSpace($metadata.authors) -or [string]::IsNullOrWhiteSpace($metadata.description)) { throw "Missing metadata: $name" }
        if ($metadata.repository.url -ne 'https://github.com/monstertacle/monstertacle.multisession' -or $metadata.repository.type -ne 'git') { throw "Wrong repository metadata: $name" }
        # MIT as an SPDX expression, with the copyright notice the license requires shipped beside every copy.
        if ($null -eq $metadata.license -or $metadata.license.type -ne 'expression' -or $metadata.license.'#text' -ne 'MIT') { throw "Missing MIT license expression: $name" }
        if ($metadata.copyright -ne 'Copyright (c) 2026 Alexander Bleissem') { throw "Missing copyright notice: $name" }
        $notice = $zip.GetEntry('LICENSE')
        if ($null -eq $notice) { throw "Missing packaged LICENSE: $name" }
        $reader = [System.IO.StreamReader]::new($notice.Open())
        try { $text = $reader.ReadToEnd() } finally { $reader.Dispose() }
        if ($text.Replace("`r`n", "`n") -cne $license) { throw "Packaged LICENSE differs from the repository MIT notice: $name" }
        $assemblies = @($zip.Entries | Where-Object FullName -Like 'lib/*/*.dll')
        if ($assemblies.Count -ne 1 -or $assemblies[0].Name -ne "$name.dll") { throw "Unexpected assembly contents: $name" }
        foreach ($dependency in $metadata.dependencies.group.dependency) {
            if ($dependency.id -eq 'Monstertacle.SharpQrs') {
                if ($name -ne 'Monstertacle.Multisession.Common' -or $dependency.version -ne '0.2.0-alpha.1') { throw "Unexpected CQRS dependency: $name" }
            } elseif ($dependency.id -like 'Monstertacle.*') {
                if ($dependency.id -notin $names -or $dependency.version -ne $version) { throw "Unexpected own dependency: $name" }
            } elseif ($dependency.id -notmatch '^(Microsoft\.|System\.|SQLitePCLRaw\.|OpenTelemetry\.)') { throw "Unexpected dependency: $name" }
        }
        if (@($zip.Entries | Where-Object FullName -Match '\.(cs|csproj|user|key|env)$').Count -gt 0) { throw "Unexpected source/private file: $name" }
        Write-Host "PASS $name $version metadata, MIT license, dependencies and contents"
    } finally { $zip.Dispose() }
}
