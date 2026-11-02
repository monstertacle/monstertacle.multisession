[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Plan', 'Push', 'Verify')][string]$Mode,
    [Parameter(Mandatory)][string]$Release,
    [Parameter(Mandatory)][string]$PackageDirectory,
    [Parameter(Mandatory)][string]$WorkDirectory,
    [string]$FeedIndex = 'https://api.nuget.org/v3/index.json',
    [string]$PackageContent = 'https://api.nuget.org/v3-flatcontainer',
    [int]$VerifyTimeoutMinutes = 60,
    # Only for the local behavioral tests: their synthetic packages carry no real repository signature, and their feed is plain HTTP
    # on 127.0.0.1.
    [switch]$SkipSignatureVerification,
    [switch]$AllowInsecureTestFeed
)
# Publishes the recorded canonical packages without rebuilding them.
# Plan: classifies each package on the feed as absent, or present and equivalent; a present package with other content stops the run.
# Push: pushes the absent packages from the plan without symbols (the short-lived key comes from NUGET_API_KEY). A 409 means the feed
# already holds that version (for example accepted by an earlier run, not yet served); the package is marked pending and is not
# skipped as a duplicate: Verify still requires it to be served with the canonical content.
# Verify: waits until every package is served, compares it with the canonical one and verifies the nuget.org repository signature.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$record = Get-Content -Raw -LiteralPath $Release | ConvertFrom-Json
$version = $record.version
$planPath = Join-Path $WorkDirectory 'publication-plan.json'
$pollSeconds = 30
New-Item -ItemType Directory -Force -Path $WorkDirectory | Out-Null

function Get-ServedPackage([string]$id) {
    $lowerId = $id.ToLowerInvariant()
    $lowerVersion = $version.ToLowerInvariant()
    try { $index = Invoke-RestMethod -Uri "$PackageContent/$lowerId/index.json" }
    catch {
        if ($_.Exception.Response -and [int]$_.Exception.Response.StatusCode -eq 404) { return $null }
        throw
    }
    if (@($index.versions) -notcontains $lowerVersion) { return $null }
    $target = Join-Path $WorkDirectory "served-$lowerId.$lowerVersion.nupkg"
    Invoke-WebRequest -Uri "$PackageContent/$lowerId/$lowerVersion/$lowerId.$lowerVersion.nupkg" -OutFile $target
    return $target
}

function Compare-Served([object]$package, [string]$served) {
    $comparison = & (Join-Path $PSScriptRoot 'Test-PublishedPackage.ps1') -Canonical (Join-Path $PackageDirectory $package.file) -Published $served | ConvertFrom-Json
    return [ordered]@{
        Id = $package.id; Version = $version; CanonicalSha256 = $comparison.CanonicalSha256; ServedSha256 = $comparison.PublishedSha256
        RepositorySigned = $comparison.RepositorySigned; ComparedEntries = $comparison.ComparedEntries; Status = $comparison.Status
    }
}

switch ($Mode) {
    'Plan' {
        $entries = @(foreach ($package in $record.packages) {
            $served = Get-ServedPackage $package.id
            if ($null -eq $served) { [ordered]@{ Id = $package.id; File = $package.file; State = 'absent' } }
            else { (Compare-Served $package $served) + [ordered]@{ File = $package.file; State = 'present' } }
        })
        $plan = [ordered]@{ Version = $version; ProducerCommit = $record.producerCommit; Packages = $entries }
        $plan | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $planPath
        $plan | ConvertTo-Json -Depth 4
    }
    'Push' {
        if ([string]::IsNullOrEmpty($env:NUGET_API_KEY)) { throw 'NUGET_API_KEY is not set.' }
        $plan = Get-Content -Raw -LiteralPath $planPath | ConvertFrom-Json
        $results = @(foreach ($entry in @($plan.Packages | Where-Object { $_.State -eq 'absent' })) {
            $path = Join-Path $PackageDirectory $entry.File
            $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
            $expected = @($record.packages | Where-Object { $_.id -ceq $entry.Id })[0].sha256
            if ($hash -ne $expected) { throw "Package bytes changed before the push: $($entry.File)" }
            $pushArguments = @('nuget', 'push', $path, '--api-key', $env:NUGET_API_KEY, '--source', $FeedIndex, '--no-symbols')
            if ($AllowInsecureTestFeed) { $pushArguments += '--allow-insecure-connections' }
            $output = @(dotnet @pushArguments 2>&1 | ForEach-Object ToString)
            $state = if ($LASTEXITCODE -eq 0) { 'pushed' }
                elseif (@($output | Select-String -SimpleMatch '409').Count -gt 0) { 'pending' }
                else { $output | Write-Output; throw "Push failed: $($entry.File)" }
            [ordered]@{ Id = $entry.Id; Sha256 = $hash; State = $state }
        })
        $result = [ordered]@{ Version = $version; Packages = $results }
        $result | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $WorkDirectory 'push-result.json')
        $result | ConvertTo-Json -Depth 3
    }
    'Verify' {
        $deadline = [DateTimeOffset]::UtcNow.AddMinutes($VerifyTimeoutMinutes)
        $receipts = @(foreach ($package in $record.packages) {
            $served = Get-ServedPackage $package.id
            while ($null -eq $served) {
                if ([DateTimeOffset]::UtcNow -gt $deadline) { throw "Package not served within $VerifyTimeoutMinutes minutes: $($package.id)" }
                Start-Sleep -Seconds $pollSeconds
                $served = Get-ServedPackage $package.id
            }
            $receipt = Compare-Served $package $served
            if (-not $SkipSignatureVerification) {
                $signature = & (Join-Path $PSScriptRoot 'Test-RepositorySignature.ps1') -Package $served | ConvertFrom-Json
                $receipt.Signer = $signature.Signer
                $receipt.SignerCertificateSha256 = $signature.CertificateSha256
            }
            $receipt
        })
        [ordered]@{ Status = 'PASS'; Version = $version; ProducerCommit = $record.producerCommit; VerifiedUtc = [DateTimeOffset]::UtcNow.ToString('O'); Packages = $receipts } |
            ConvertTo-Json -Depth 4
    }
}
