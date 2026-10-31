[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Evidence,
    [Parameter(Mandatory)][string]$HostAssembly,
    [string]$Summary
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$proof = Get-Content -Raw -LiteralPath $Evidence | ConvertFrom-Json
if ($proof.ElapsedTicks -lt [TimeSpan]::FromHours(1).Ticks -or $proof.RequestIds.Count -ne 60 -or $proof.Acceptances.Count -ne 60 -or $proof.ReopenedRequests -ne 60) { throw 'Incomplete hour, receipt samples or reopened identities.' }
$ids = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($id in $proof.RequestIds) {
    $parsed = [Guid]::Empty
    if (-not [Guid]::TryParseExact($id, 'D', [ref]$parsed) -or -not $ids.Add($id)) { throw 'Invalid or duplicate original request identity.' }
}
$seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$minutes = [Collections.Generic.HashSet[int]]::new()
foreach ($sample in $proof.Acceptances) {
    if (-not $ids.Contains($sample.RequestId) -or -not $seen.Add($sample.RequestId) -or $sample.ElapsedTicks -lt 0 -or $sample.ElapsedTicks -ge [TimeSpan]::FromHours(1).Ticks) { throw 'Invalid monotonic receipt sample.' }
    $minute = [int][Math]::Floor($sample.ElapsedTicks / [double][TimeSpan]::TicksPerMinute)
    if (-not $minutes.Add($minute)) { throw 'Repeated or missing acceptance minute.' }
}
$hash = (Get-FileHash -LiteralPath $HostAssembly -Algorithm SHA256).Hash.ToLowerInvariant()
if ($hash -ne $proof.HostAssemblySha256) { throw 'Host assembly differs from the measured run.' }
$result = [ordered]@{ Status = 'PASS'; ValidatedUtc = [DateTimeOffset]::UtcNow.ToString('O'); ElapsedTicks = $proof.ElapsedTicks; Requests = $ids.Count; Minutes = $minutes.Count; ReopenedRequests = $proof.ReopenedRequests; HostAssemblySha256 = $hash }
$json = $result | ConvertTo-Json
if (-not [string]::IsNullOrWhiteSpace($Summary)) { [IO.File]::WriteAllText([IO.Path]::GetFullPath($Summary), $json) }
Write-Output $json
