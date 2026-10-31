param([string]$PackageDirectory = 'artifacts/packages')
$ErrorActionPreference = 'Stop'
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
    $package = @($packages | Where-Object Name -EQ "$name.0.2.0-alpha.3.nupkg")
    if ($package.Count -ne 1) { throw "Missing expected package: $name" }
    $zip = [System.IO.Compression.ZipFile]::OpenRead($package[0].FullName)
    try {
        $entry = $zip.GetEntry("$name.nuspec")
        if ($null -eq $entry) { throw "Missing nuspec: $name" }
        $reader = [System.IO.StreamReader]::new($entry.Open())
        try { [xml]$specification = $reader.ReadToEnd() } finally { $reader.Dispose() }
        $metadata = $specification.package.metadata
        if ($metadata.id -ne $name -or $metadata.version -ne '0.2.0-alpha.3') { throw "Wrong identity/version: $name" }
        if ($metadata.readme -ne 'README.md' -or $null -eq $zip.GetEntry('README.md')) { throw "Missing packaged README: $name" }
        if ([string]::IsNullOrWhiteSpace($metadata.authors) -or [string]::IsNullOrWhiteSpace($metadata.description)) { throw "Missing metadata: $name" }
        if ($metadata.repository.url -ne 'https://github.com/monstertacle/monstertacle.multisession' -or $metadata.repository.type -ne 'git') { throw "Wrong repository metadata: $name" }
        $assemblies = @($zip.Entries | Where-Object FullName -Like 'lib/*/*.dll')
        if ($assemblies.Count -ne 1 -or $assemblies[0].Name -ne "$name.dll") { throw "Unexpected assembly contents: $name" }
        foreach ($dependency in $metadata.dependencies.group.dependency) {
            if ($dependency.id -eq 'Monstertacle.SharpQrs') {
                if ($name -ne 'Monstertacle.Multisession.Common' -or $dependency.version -ne '0.2.0-alpha.1') { throw "Unexpected CQRS dependency: $name" }
            } elseif ($dependency.id -like 'Monstertacle.*') {
                if ($dependency.id -notin $names -or $dependency.version -ne '0.2.0-alpha.3') { throw "Unexpected own dependency: $name" }
            } elseif ($dependency.id -notmatch '^(Microsoft\.|System\.|SQLitePCLRaw\.|OpenTelemetry\.)') { throw "Unexpected dependency: $name" }
        }
        if (@($zip.Entries | Where-Object FullName -Match '\.(cs|csproj|user|key|env)$').Count -gt 0) { throw "Unexpected source/private file: $name" }
        Write-Host "PASS $name 0.2.0-alpha.3 metadata, dependencies and contents"
    } finally { $zip.Dispose() }
}
