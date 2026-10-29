[CmdletBinding()]
param([Parameter(Mandatory)][string]$ProducerManifest, [Parameter(Mandatory)][string]$PackageDirectory)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$producer = Get-Content -Raw -LiteralPath $ProducerManifest | ConvertFrom-Json
$packages = @($producer.Hashes | Where-Object { $_.File -is [string] -and $_.File.EndsWith('.nupkg', [StringComparison]::Ordinal) })
$files = @(Get-ChildItem -LiteralPath $PackageDirectory -File -Filter '*.nupkg')
if ($producer.Status -cne 'PASS' -or $packages.Count -ne 6 -or $files.Count -ne 6) { throw 'Incomplete or failed canonical package producer.' }
$names = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($entry in $packages) {
    if ($entry.File -isnot [string] -or [string]::IsNullOrWhiteSpace($entry.File) -or $entry.File.IndexOfAny([char[]]@('/', '\')) -ge 0 -or
        -not $entry.File.EndsWith('.nupkg', [StringComparison]::Ordinal) -or -not $names.Add($entry.File) -or
        $entry.Sha256 -isnot [string] -or $entry.Sha256 -notmatch '^[0-9a-f]{64}$') { throw 'Invalid canonical package identity or digest.' }
}
if (-not $names.SetEquals([string[]]$files.Name)) { throw 'Canonical package file set differs from the producer.' }
$verified = @(foreach ($entry in $packages) {
    $path = Join-Path $PackageDirectory $entry.File
    $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($hash -ne $entry.Sha256) { throw 'Canonical package bytes differ from the producer.' }
    [ordered]@{ File = $entry.File; Sha256 = $hash }
})
[ordered]@{ Status = 'PASS'; ValidatedUtc = [DateTimeOffset]::UtcNow.ToString('O'); ProducerManifestSha256 = (Get-FileHash -LiteralPath $ProducerManifest -Algorithm SHA256).Hash.ToLowerInvariant(); Hashes = $verified } | ConvertTo-Json -Depth 4
