[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Release,
    [Parameter(Mandatory)][string]$ArtifactDirectory
)
# Verifies that a downloaded producer artifact is exactly the canonical release recorded in the repository: the producer manifest
# digest, the package file set and bytes, and each package's id, version and source commit. Nothing is rebuilt or repacked.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$record = Get-Content -Raw -LiteralPath $Release | ConvertFrom-Json
if ($record.schema -ne 1 -or $record.version -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z.]+)?$' -or $record.producerCommit -notmatch '^[0-9a-f]{40}$' -or
    $record.producerManifestSha256 -notmatch '^[0-9a-f]{64}$') { throw 'Invalid release record.' }
$manifests = @(Get-ChildItem -LiteralPath (Join-Path $ArtifactDirectory 'package-smoke') -Filter manifest.json -Recurse -File)
if ($manifests.Count -ne 1) { throw 'Expected exactly one producer manifest in the artifact.' }
$manifestHash = (Get-FileHash -LiteralPath $manifests[0].FullName -Algorithm SHA256).Hash.ToLowerInvariant()
if ($manifestHash -ne $record.producerManifestSha256) { throw 'Producer manifest differs from the release record.' }
$packageDirectory = Join-Path $ArtifactDirectory 'packages'
# The producer's own check: a passing manifest and the exact package bytes it lists.
& (Join-Path $PSScriptRoot 'Test-CanonicalPackageHashes.ps1') -ProducerManifest $manifests[0].FullName -PackageDirectory $packageDirectory | Out-Null
# Every file in the package directory must be a recorded package: a symbol package or any other file beside them is refused, because a
# push would publish it without a record.
$files = @(Get-ChildItem -LiteralPath $packageDirectory -File -Recurse -Force | ForEach-Object {
    if ($_.DirectoryName -ne (Get-Item -LiteralPath $packageDirectory).FullName) { throw "Unrecorded file below the package directory: $($_.FullName)" }
    $_
})
$recordedFiles = @($record.packages | ForEach-Object file)
$unrecorded = @($files | Where-Object { $recordedFiles -cnotcontains $_.Name })
if ($unrecorded.Count -ne 0) { throw "Unrecorded files in the package directory: $($unrecorded.Name -join ', ')" }
if ($files.Count -ne $recordedFiles.Count) { throw 'Package count differs from the release record.' }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$verified = @(foreach ($package in $record.packages) {
    if ($package.id -notmatch '^Monstertacle\.Multisession(\.[A-Za-z0-9]+)+$' -or $package.file -cne "$($package.id).$($record.version).nupkg") {
        throw "Invalid package identity in the release record: $($package.id)"
    }
    $path = Join-Path $packageDirectory $package.file
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Package missing from the artifact: $($package.file)" }
    $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($hash -ne $package.sha256) { throw "Package bytes differ from the release record: $($package.file)" }
    $archive = [IO.Compression.ZipFile]::OpenRead($path)
    try {
        $entries = @($archive.Entries | Where-Object { $_.FullName -notmatch '/' -and $_.FullName.EndsWith('.nuspec', [StringComparison]::Ordinal) })
        if ($entries.Count -ne 1) { throw "Expected one nuspec in $($package.file)" }
        if (@($archive.Entries | Where-Object { $_.FullName -ceq '.signature.p7s' }).Count -ne 0) { throw "Canonical package is already signed: $($package.file)" }
        $reader = [IO.StreamReader]::new($entries[0].Open())
        try { [xml]$nuspec = $reader.ReadToEnd() } finally { $reader.Dispose() }
    }
    finally { $archive.Dispose() }
    $metadata = $nuspec.package.metadata
    if ($metadata.id -cne $package.id -or $metadata.version -cne $record.version -or $metadata.repository.commit -cne $record.producerCommit) {
        throw "Package metadata differs from the release record: $($package.file)"
    }
    [ordered]@{ Id = $package.id; File = $package.file; Sha256 = $hash }
})
[ordered]@{
    Status = 'PASS'; Version = $record.version; ProducerCommit = $record.producerCommit; ProducerRun = $record.producerRun
    ProducerManifestSha256 = $manifestHash; Packages = $verified
} | ConvertTo-Json -Depth 4
