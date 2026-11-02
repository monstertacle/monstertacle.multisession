[CmdletBinding()]
param([Parameter(Mandatory)][string]$Package)
# Verifies a served package's signature with the .NET SDK (`dotnet nuget verify --all`) and requires a repository primary signature
# by the nuget.org repository certificate. Returns the signer and its certificate fingerprint for the publication receipt.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repositorySigner = 'CN=NuGet.org Repository by Microsoft,'
# The output is parsed, so it must be the English output whatever the machine's language.
$env:DOTNET_CLI_UI_LANGUAGE = 'en'
$output = @(dotnet nuget verify --all $Package --verbosity detailed 2>&1 | ForEach-Object ToString)
if ($LASTEXITCODE -ne 0) { $output | ForEach-Object { Write-Host $_ }; throw "Signature verification failed for $Package" }
$signatureType = @($output | Select-String -Pattern '^\s*Signature type:\s*(\S+)' | ForEach-Object { $_.Matches[0].Groups[1].Value })
$signerLine = @($output | Select-String -Pattern 'Verifying the repository primary signature with certificate' | Select-Object -First 1)
if ($signatureType.Count -lt 1 -or $signatureType[0] -cne 'Repository' -or $signerLine.Count -ne 1) { throw "No repository primary signature on $Package" }
# The certificate block follows the line that names the repository primary signature: subject first, then its fingerprints.
$block = @($output[($signerLine[0].LineNumber)..([Math]::Min($signerLine[0].LineNumber + 4, $output.Count - 1))])
$subject = @($block | Select-String -Pattern '^\s*Subject Name:\s*(.+)$' | Select-Object -First 1 | ForEach-Object { $_.Matches[0].Groups[1].Value })
$fingerprint = @($block | Select-String -Pattern '^\s*SHA256 hash:\s*([0-9A-F]{64})\s*$' | Select-Object -First 1 | ForEach-Object { $_.Matches[0].Groups[1].Value })
if ($subject.Count -ne 1 -or -not $subject[0].StartsWith($repositorySigner, [StringComparison]::Ordinal) -or $fingerprint.Count -ne 1) {
    throw "Repository signature of $Package is not by the nuget.org repository certificate"
}
[ordered]@{ SignatureType = 'Repository'; Signer = $subject[0]; CertificateSha256 = $fingerprint[0].ToLowerInvariant() } | ConvertTo-Json -Compress
