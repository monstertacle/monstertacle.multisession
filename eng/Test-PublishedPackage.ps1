[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Canonical,
    [Parameter(Mandatory)][string]$Published
)
# Compares a package served by a feed with the canonical package. A repository countersignature adds the entry .signature.p7s and
# rewrites the archive, so archive bytes differ; every other entry must have the same name and content. Anything else is a conflict.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$signatureEntry = '.signature.p7s'

function Get-EntryDigests([string]$path) {
    $archive = [IO.Compression.ZipFile]::OpenRead($path)
    try {
        $digests = [Collections.Generic.SortedDictionary[string, string]]::new([StringComparer]::Ordinal)
        foreach ($entry in $archive.Entries) {
            if ($entry.FullName.EndsWith('/', [StringComparison]::Ordinal)) { continue }
            if ($digests.ContainsKey($entry.FullName)) { throw "Duplicate archive entry $($entry.FullName) in $path" }
            $stream = $entry.Open()
            try { $digests[$entry.FullName] = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)).ToLowerInvariant() }
            finally { $stream.Dispose() }
        }
        return $digests
    }
    finally { $archive.Dispose() }
}

$canonicalEntries = Get-EntryDigests $Canonical
$publishedEntries = Get-EntryDigests $Published
if ($canonicalEntries.ContainsKey($signatureEntry)) { throw 'The canonical package must not carry a signature.' }
$signed = $publishedEntries.ContainsKey($signatureEntry)
$publishedContent = @($publishedEntries.Keys | Where-Object { $_ -cne $signatureEntry })
$differences = @(
    @($canonicalEntries.Keys | Where-Object { -not $publishedEntries.ContainsKey($_) } | ForEach-Object { "missing $_" })
    @($publishedContent | Where-Object { -not $canonicalEntries.ContainsKey($_) } | ForEach-Object { "added $_" })
    @($canonicalEntries.Keys | Where-Object { $publishedEntries.ContainsKey($_) -and $publishedEntries[$_] -ne $canonicalEntries[$_] } | ForEach-Object { "changed $_" })
)
$result = [ordered]@{
    Status = if ($differences.Count -eq 0) { 'EQUIVALENT' } else { 'CONFLICT' }
    CanonicalSha256 = (Get-FileHash -LiteralPath $Canonical -Algorithm SHA256).Hash.ToLowerInvariant()
    PublishedSha256 = (Get-FileHash -LiteralPath $Published -Algorithm SHA256).Hash.ToLowerInvariant()
    RepositorySigned = $signed
    ComparedEntries = $canonicalEntries.Count
    Differences = $differences
}
$result | ConvertTo-Json -Depth 3
if ($differences.Count -ne 0) { throw "Published package differs from the canonical package: $($differences -join '; ')" }
