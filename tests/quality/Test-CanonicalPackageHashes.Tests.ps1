$ErrorActionPreference = 'Stop'
$repository = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$validator = Join-Path $repository 'eng/Test-CanonicalPackageHashes.ps1'
$root = Join-Path ([Environment]::GetFolderPath('UserProfile')) ('dev/.agenttemp/multisession-canonical-hashes-' + [Guid]::NewGuid().ToString('N'))
$null = New-Item -Path $root -ItemType Directory
$failures = @()
try {
    foreach ($scenario in @('valid', 'producer-input-hashes', 'tampered', 'missing', 'extra', 'duplicate', 'malformed', 'failed-producer')) {
        $case = Join-Path $root $scenario
        $feed = Join-Path $case 'packages'
        $null = New-Item -Path $feed -ItemType Directory -Force
        $hashes = @(foreach ($index in 0..6) {
            $name = "Fixture.$index.0.1.0.nupkg"
            $path = Join-Path $feed $name
            Set-Content -LiteralPath $path -Value "synthetic package bytes $index"
            [pscustomobject]@{ File = $name; Sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() }
        })
        $manifest = [pscustomobject]@{ Status = 'PASS'; Hashes = $hashes }
        switch ($scenario) {
            'producer-input-hashes' {
                $manifest.Hashes = @($hashes) + @('PackageConsumer.cs.txt', 'Program.cs', 'Consumer.csproj', 'NuGet.Config' | ForEach-Object {
                    [pscustomobject]@{ File = $_; Sha256 = ('f' * 64) }
                })
            }
            'tampered' { Add-Content -LiteralPath (Join-Path $feed $hashes[0].File) -Value 'changed bytes' }
            'missing' { Move-Item -LiteralPath (Join-Path $feed $hashes[0].File) -Destination (Join-Path $case 'not-in-feed') }
            'extra' { Set-Content -LiteralPath (Join-Path $feed 'unexpected.nupkg') -Value 'extra' }
            'duplicate' { $hashes[1].File = $hashes[0].File }
            'malformed' { $hashes[0].Sha256 = 'invalid-digest' }
            'failed-producer' { $manifest.Status = 'FAIL' }
        }
        $manifestPath = Join-Path $case 'producer.json'
        $manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $manifestPath
        & (Join-Path $PSHOME 'pwsh') -NoProfile -File $validator -ProducerManifest $manifestPath -PackageDirectory $feed *> (Join-Path $case 'validator.log')
        $accepted = $LASTEXITCODE -eq 0
        if ($accepted -ne ($scenario -in @('valid', 'producer-input-hashes'))) {
            Write-Output "FAIL canonical package hashes: $scenario"
            $failures += $scenario
        } else { Write-Output "PASS canonical package hashes: $scenario" }
    }
    if ($failures.Count -gt 0) { throw "$($failures.Count) canonical package hash cases failed: $($failures -join ', ')" }
}
finally { Remove-Item -LiteralPath $root -Recurse }
