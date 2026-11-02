[CmdletBinding()]
param([string]$WorkRoot = (Join-Path ([IO.Path]::GetTempPath()) "publication-tooling-$([Guid]::NewGuid().ToString('N'))"))
# Behavioral tests of the release input check, the served-package comparison and the publication plan/push/verify steps against
# synthetic packages and a local test feed. No network access beyond 127.0.0.1, no credentials, no real feed.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$version = '9.9.9-test.1'
$commit = 'a' * 40
$ids = @('Monstertacle.Multisession.Common', 'Monstertacle.Multisession.Linux')
$failures = [Collections.Generic.List[string]]::new()
$passed = 0
New-Item -ItemType Directory -Force -Path $WorkRoot | Out-Null

function New-TestPackage([string]$path, [string]$id, [string]$packageVersion, [string]$sourceCommit, [hashtable]$extra = @{}) {
    if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path }
    $archive = [IO.Compression.ZipFile]::Open($path, 'Create')
    try {
        $entries = [ordered]@{
            "$id.nuspec" = "<?xml version=`"1.0`"?><package xmlns=`"http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd`"><metadata><id>$id</id><version>$packageVersion</version><authors>test</authors><description>test</description><repository type=`"git`" url=`"https://example.invalid/repo`" commit=`"$sourceCommit`" /></metadata></package>"
            "lib/net10.0/$id.dll" = "assembly $id"
            'README.md' = 'readme'
        }
        foreach ($key in $extra.Keys) { $entries[$key] = $extra[$key] }
        foreach ($name in $entries.Keys) {
            $writer = [IO.StreamWriter]::new($archive.CreateEntry($name).Open())
            try { $writer.Write($entries[$name]) } finally { $writer.Dispose() }
        }
    }
    finally { $archive.Dispose() }
}

function Add-Entry([string]$path, [string]$name, [string]$content) {
    $archive = [IO.Compression.ZipFile]::Open($path, 'Update')
    try {
        $existing = $archive.GetEntry($name); if ($existing) { $existing.Delete() }
        $writer = [IO.StreamWriter]::new($archive.CreateEntry($name).Open())
        try { $writer.Write($content) } finally { $writer.Dispose() }
    }
    finally { $archive.Dispose() }
}

function New-Release([string]$root) {
    $packages = Join-Path $root 'artifact/packages'
    $manifestDir = Join-Path $root 'artifact/package-smoke/run'
    New-Item -ItemType Directory -Force -Path $packages, $manifestDir | Out-Null
    $hashes = @(foreach ($id in $ids) {
        $file = "$id.$version.nupkg"
        New-TestPackage (Join-Path $packages $file) $id $version $commit
        [ordered]@{ File = $file; Sha256 = (Get-FileHash -LiteralPath (Join-Path $packages $file) -Algorithm SHA256).Hash.ToLowerInvariant() }
    })
    $manifest = Join-Path $manifestDir 'manifest.json'
    [ordered]@{ Status = 'PASS'; Hashes = $hashes } | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath $manifest
    $record = Join-Path $root 'record.json'
    [ordered]@{
        schema = 1; version = $version; producerRepository = 'owner/repo'; producerCommit = $commit; producerRun = 1
        producerWorkflow = '.github/workflows/ci.yml'; artifact = 'packages'
        producerManifestSha256 = (Get-FileHash -LiteralPath $manifest -Algorithm SHA256).Hash.ToLowerInvariant()
        packages = @(foreach ($h in $hashes) { [ordered]@{ id = $h.File.Substring(0, $h.File.Length - ".$version.nupkg".Length); file = $h.File; sha256 = $h.Sha256 } })
    } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $record
    return [pscustomobject]@{ Root = $root; Record = $record; Artifact = (Join-Path $root 'artifact'); Packages = $packages; Manifest = $manifest }
}

function Invoke-Case([string]$name, [bool]$expectSuccess, [scriptblock]$action) {
    $succeeded = $true
    $reason = ''
    try { & $action *> $null } catch { $succeeded = $false; $reason = ": $($_.Exception.Message)" }
    if ($succeeded -eq $expectSuccess) { $script:passed++ } else { $script:failures.Add("$name (expected $(if ($expectSuccess) { 'success' } else { 'refusal' }))$reason") }
}

# The canonical producer manifest check expects the real release size; the input check is exercised through a copy of it that only
# differs in that constant, so the remaining behavior is the shipped script's.
$canonicalHashes = Join-Path $PSScriptRoot 'Test-CanonicalPackageHashes.ps1'
$toolDir = Join-Path $WorkRoot 'tools'
New-Item -ItemType Directory -Force -Path $toolDir | Out-Null
foreach ($script in 'Test-ReleaseInput.ps1', 'Test-PublishedPackage.ps1', 'Publish-CanonicalRelease.ps1', 'Test-RepositorySignature.ps1') {
    $source = Join-Path $PSScriptRoot $script
    if (Test-Path -LiteralPath $source) { Copy-Item -LiteralPath $source -Destination $toolDir }
}
(Get-Content -Raw -LiteralPath $canonicalHashes) -replace '\$expectedPackageCount = 8', "`$expectedPackageCount = $($ids.Count)" |
    Set-Content -LiteralPath (Join-Path $toolDir 'Test-CanonicalPackageHashes.ps1')
$inputCheck = Join-Path $toolDir 'Test-ReleaseInput.ps1'
$compare = Join-Path $toolDir 'Test-PublishedPackage.ps1'
$publish = Join-Path $toolDir 'Publish-CanonicalRelease.ps1'

# Release input.
$release = New-Release (Join-Path $WorkRoot 'input-ok')
Invoke-Case 'input: exact release passes' $true { & $inputCheck -Release $release.Record -ArtifactDirectory $release.Artifact }
$changed = New-Release (Join-Path $WorkRoot 'input-byte'); Add-Content -LiteralPath (Join-Path $changed.Packages "$($ids[1]).$version.nupkg") 'x'
Invoke-Case 'input: changed package byte refused' $false { & $inputCheck -Release $changed.Record -ArtifactDirectory $changed.Artifact }
$manifestChanged = New-Release (Join-Path $WorkRoot 'input-manifest'); Add-Content -LiteralPath $manifestChanged.Manifest ' '
Invoke-Case 'input: changed producer manifest refused' $false { & $inputCheck -Release $manifestChanged.Record -ArtifactDirectory $manifestChanged.Artifact }
$wrongCommit = New-Release (Join-Path $WorkRoot 'input-commit')
(Get-Content -Raw $wrongCommit.Record | ConvertFrom-Json) | ForEach-Object { $_.producerCommit = 'b' * 40; $_ } | ConvertTo-Json -Depth 4 | Set-Content $wrongCommit.Record
Invoke-Case 'input: nuspec commit differing from the record refused' $false { & $inputCheck -Release $wrongCommit.Record -ArtifactDirectory $wrongCommit.Artifact }
$symbols = New-Release (Join-Path $WorkRoot 'input-symbols'); Set-Content -LiteralPath (Join-Path $symbols.Packages "$($ids[0]).$version.snupkg") 'symbols'
Invoke-Case 'input: unrecorded symbol package refused' $false { & $inputCheck -Release $symbols.Record -ArtifactDirectory $symbols.Artifact }
$hidden = New-Release (Join-Path $WorkRoot 'input-hidden'); New-Item -ItemType Directory -Path (Join-Path $hidden.Packages 'nested') | Out-Null
Set-Content -LiteralPath (Join-Path $hidden.Packages 'nested/.extra.snupkg') 'symbols'
Invoke-Case 'input: hidden or nested file refused' $false { & $inputCheck -Release $hidden.Record -ArtifactDirectory $hidden.Artifact }
$signedInput = New-Release (Join-Path $WorkRoot 'input-signed')
Add-Entry (Join-Path $signedInput.Packages "$($ids[0]).$version.nupkg") '.signature.p7s' 'sig'
Invoke-Case 'input: signed canonical package refused' $false { & $inputCheck -Release $signedInput.Record -ArtifactDirectory $signedInput.Artifact }

# Served-package comparison.
$canonical = Join-Path $release.Packages "$($ids[0]).$version.nupkg"
$served = Join-Path $WorkRoot 'served.nupkg'
Copy-Item $canonical $served; Add-Entry $served '.signature.p7s' 'sig'
Invoke-Case 'compare: added signature entry is equivalent' $true { & $compare -Canonical $canonical -Published $served }
Copy-Item $canonical $served
Invoke-Case 'compare: unsigned served package is refused' $false { & $compare -Canonical $canonical -Published $served }
Copy-Item $canonical $served; Add-Entry $served '.signature.p7s' 'sig'; Add-Entry $served 'README.md' 'changed'
Invoke-Case 'compare: changed entry refused' $false { & $compare -Canonical $canonical -Published $served }
Copy-Item $canonical $served; Add-Entry $served '.signature.p7s' 'sig'; Add-Entry $served 'lib/net10.0/extra.dll' 'x'
Invoke-Case 'compare: added entry refused' $false { & $compare -Canonical $canonical -Published $served }

# Local test feed: serves flat-container files and answers pushes: 201; 409 for ids listed in conflict.txt; 403 for ids listed in
# forbidden.txt, with a reason phrase that contains "409" so a loose match on the output would misread it.
$feedRoot = Join-Path $WorkRoot 'feed'
New-Item -ItemType Directory -Force -Path $feedRoot | Out-Null
$listenerAttempts = 10
$listener = $null
for ($attempt = 1; $null -eq $listener -and $attempt -le $listenerAttempts; $attempt++) {
    $candidate = [Net.HttpListener]::new()
    $port = Get-Random -Minimum 20000 -Maximum 60000
    $candidate.Prefixes.Add("http://127.0.0.1:$port/")
    try { $candidate.Start(); $listener = $candidate } catch { $candidate.Close() }
}
if ($null -eq $listener) { throw 'No free local port for the test feed.' }
$server = Start-ThreadJob -ArgumentList $listener, $feedRoot -ScriptBlock {
    param($listener, $feedRoot)
    while ($listener.IsListening) {
        try { $context = $listener.GetContext() } catch { break }
        $path = Join-Path $feedRoot ($context.Request.Url.AbsolutePath.TrimStart('/'))
        if ($context.Request.HttpMethod -eq 'PUT') {
            $conflicts = Join-Path $feedRoot 'conflict.txt'
            $body = [IO.StreamReader]::new($context.Request.InputStream).ReadToEnd()
            $forbidden = Join-Path $feedRoot 'forbidden.txt'
            if ((Test-Path $forbidden) -and @(Get-Content $forbidden | Where-Object { $body.Contains($_) }).Count -gt 0) {
                $context.Response.StatusCode = 403; $context.Response.StatusDescription = 'Forbidden for quota 409'
            }
            else { $context.Response.StatusCode = if ((Test-Path $conflicts) -and @(Get-Content $conflicts | Where-Object { $body.Contains($_) }).Count -gt 0) { 409 } else { 201 } }
            Add-Content -LiteralPath (Join-Path $feedRoot 'pushes.log') $context.Response.StatusCode
        }
        elseif (Test-Path -LiteralPath $path -PathType Leaf) { $bytes = [IO.File]::ReadAllBytes($path); $context.Response.OutputStream.Write($bytes, 0, $bytes.Length) }
        else { $context.Response.StatusCode = 404 }
        $context.Response.Close()
    }
}
function Set-Served([string]$id, [string]$package) {
    $lower = $id.ToLowerInvariant()
    $dir = Join-Path $feedRoot "$lower/$version"
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    Copy-Item $package (Join-Path $dir "$lower.$version.nupkg")
    '{"versions":["' + $version + '"]}' | Set-Content (Join-Path $feedRoot "$lower/index.json")
}
function Clear-Feed { Get-ChildItem -LiteralPath $feedRoot -Force | Remove-Item -Recurse -Force }
$content = "http://127.0.0.1:$port"
$pushSource = "http://127.0.0.1:$port"
try {
    $work = Join-Path $WorkRoot 'plan-absent'
    Invoke-Case 'plan: absent packages are planned for push' $true {
        & $publish -Mode Plan -Release $release.Record -PackageDirectory $release.Packages -WorkDirectory $work -PackageContent $content
        if (@((Get-Content -Raw (Join-Path $work 'publication-plan.json') | ConvertFrom-Json).Packages | Where-Object State -eq 'absent').Count -ne $ids.Count) { throw 'not absent' }
    }
    $equivalent = Join-Path $WorkRoot 'equivalent.nupkg'; Copy-Item $canonical $equivalent; Add-Entry $equivalent '.signature.p7s' 'sig'
    Set-Served $ids[0] $equivalent
    $work = Join-Path $WorkRoot 'plan-present'
    Invoke-Case 'plan: served equivalent package is present' $true {
        & $publish -Mode Plan -Release $release.Record -PackageDirectory $release.Packages -WorkDirectory $work -PackageContent $content
        $states = @((Get-Content -Raw (Join-Path $work 'publication-plan.json') | ConvertFrom-Json).Packages | ForEach-Object State)
        if ($states[0] -ne 'present' -or $states[1] -ne 'absent') { throw "states $states" }
    }
    $conflicting = Join-Path $WorkRoot 'conflicting.nupkg'; Copy-Item $equivalent $conflicting; Add-Entry $conflicting 'README.md' 'other'
    Clear-Feed; Set-Served $ids[0] $conflicting
    Invoke-Case 'plan: served conflicting package stops the run' $false {
        & $publish -Mode Plan -Release $release.Record -PackageDirectory $release.Packages -WorkDirectory (Join-Path $WorkRoot 'plan-conflict') -PackageContent $content
    }

    # Push: the absent packages are pushed without symbols; a 409 (accepted earlier, not yet served) is pending, not fatal.
    Clear-Feed
    $work = Join-Path $WorkRoot 'push'
    & $publish -Mode Plan -Release $release.Record -PackageDirectory $release.Packages -WorkDirectory $work -PackageContent $content *> $null
    Set-Content -LiteralPath (Join-Path $feedRoot 'conflict.txt') $ids[0]
    $env:NUGET_API_KEY = 'test-key'
    Invoke-Case 'push: a 409 for an accepted package is pending and the rest are pushed' $true {
        & $publish -Mode Push -Release $release.Record -PackageDirectory $release.Packages -WorkDirectory $work -FeedIndex $pushSource -PackageContent $content -AllowInsecureTestFeed
        $codes = @(Get-Content (Join-Path $feedRoot 'pushes.log'))
        if ($codes.Count -ne $ids.Count -or $codes -notcontains '201') { throw "pushes $codes" }
        $states = @((Get-Content -Raw (Join-Path $work 'push-result.json') | ConvertFrom-Json).Packages | ForEach-Object State)
        if ($states -notcontains 'pending' -or $states -notcontains 'pushed') { throw "states $states" }
    }
    Clear-Feed
    Set-Content -LiteralPath (Join-Path $feedRoot 'forbidden.txt') $ids[0]
    Invoke-Case 'push: a refused push (403) fails even when its message contains 409' $false {
        & $publish -Mode Push -Release $release.Record -PackageDirectory $release.Packages -WorkDirectory $work -FeedIndex $pushSource -PackageContent $content -AllowInsecureTestFeed
    }
    Remove-Item Env:NUGET_API_KEY

    # Verify: every package must be served, equivalent and signed; an unsigned served package is refused.
    Clear-Feed; Set-Served $ids[0] $equivalent
    $second = Join-Path $WorkRoot 'second.nupkg'; Copy-Item (Join-Path $release.Packages "$($ids[1]).$version.nupkg") $second
    Set-Served $ids[1] $second
    Invoke-Case 'verify: an unsigned served package is refused' $false {
        & $publish -Mode Verify -Release $release.Record -PackageDirectory $release.Packages -WorkDirectory (Join-Path $WorkRoot 'verify-unsigned') -PackageContent $content -VerifyTimeoutMinutes 0 -SkipSignatureVerification
    }
    $secondSigned = Join-Path $WorkRoot 'second-signed.nupkg'; Copy-Item $second $secondSigned; Add-Entry $secondSigned '.signature.p7s' 'sig'
    Clear-Feed; Set-Served $ids[0] $equivalent; Set-Served $ids[1] $secondSigned
    Invoke-Case 'verify: served equivalent packages pass with a receipt per package' $true {
        $receipt = & $publish -Mode Verify -Release $release.Record -PackageDirectory $release.Packages -WorkDirectory (Join-Path $WorkRoot 'verify-ok') -PackageContent $content -VerifyTimeoutMinutes 0 -SkipSignatureVerification | ConvertFrom-Json
        if ($receipt.Status -ne 'PASS' -or @($receipt.Packages | Where-Object { $_.Status -eq 'EQUIVALENT' -and $_.RepositorySigned }).Count -ne $ids.Count) { throw 'receipt' }
    }
    Clear-Feed; Set-Served $ids[0] $equivalent
    Invoke-Case 'verify: a package that is never served fails within the timeout' $false {
        & $publish -Mode Verify -Release $release.Record -PackageDirectory $release.Packages -WorkDirectory (Join-Path $WorkRoot 'verify-missing') -PackageContent $content -VerifyTimeoutMinutes 0 -SkipSignatureVerification
    }
}
finally {
    $listener.Stop(); $listener.Close()
    $server | Wait-Job -Timeout 5 | Out-Null; $server | Remove-Job -Force
}

Remove-Item -LiteralPath $WorkRoot -Recurse -Force
[ordered]@{ Status = if ($failures.Count -eq 0) { 'PASS' } else { 'FAIL' }; Passed = $passed; Failed = @($failures) } | ConvertTo-Json -Depth 3
if ($failures.Count -ne 0) { exit 1 }
