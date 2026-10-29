[CmdletBinding()]
param(
    [ValidateSet('windows', 'linux')]
    [string]$Platform = $(if ($IsWindows) { 'windows' } else { 'linux' })
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

function Invoke-Dotnet([string[]]$Arguments) {
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet command failed (exit $LASTEXITCODE): $($Arguments -join ' ')" }
}

function Get-SourceHash {
    $files = @(Get-Item -Force global.json, Directory.Build.props, Directory.Packages.props, .editorconfig,
        Monstertacle.Multisession.slnx, codemetr.json, .config/dotnet-tools.json, README.md)
    $files += @(Get-ChildItem src, tests, examples, scripts, eng, .github/workflows -Recurse -File |
        Where-Object {
            ($_.FullName -notmatch '[\\/](bin|obj|TestResults)[\\/]') -and
                ($_.Extension -in '.cs', '.csproj', '.props', '.runsettings', '.json', '.ps1', '.yml', '.sql', '.txt')
        })
    $manifest = ($files | Sort-Object FullName | ForEach-Object {
        "$([IO.Path]::GetRelativePath($root, $_.FullName).Replace('\', '/')) $((Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant())"
    }) -join "`n"
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($manifest))).ToLowerInvariant()
}

Push-Location $root
try {
    if (($Platform -eq 'windows' -and -not $IsWindows) -or ($Platform -eq 'linux' -and -not $IsLinux)) {
        throw 'Quality must run on the named native operating system, not only with a different target framework.'
    }
    $runDirectory = Join-Path $root ("artifacts/quality/$Platform/" + [Guid]::NewGuid().ToString('N'))
    $null = New-Item $runDirectory -ItemType Directory -Force
    $sourceHash = Get-SourceHash
    [ordered]@{ SourceHash = $sourceHash; Platform = $Platform; StartedUtc = [DateTimeOffset]::UtcNow.ToString('O') } |
        ConvertTo-Json | Set-Content (Join-Path $runDirectory 'run.json') -Encoding utf8

    Invoke-Dotnet @('restore', 'Monstertacle.Multisession.slnx', '--locked-mode', '--force')
    Invoke-Dotnet @('build', 'Monstertacle.Multisession.slnx', '-c', 'Release', '--no-restore')
    Invoke-Dotnet @('format', 'Monstertacle.Multisession.slnx', '--verify-no-changes', '--no-restore')
    & (Join-Path $PSHOME 'pwsh') -NoProfile -File tests/quality/Test-Coverage.Tests.ps1
    if ($LASTEXITCODE -ne 0) { throw 'Coverage gate behavior tests failed.' }
    & (Join-Path $PSHOME 'pwsh') -NoProfile -File tests/quality/Test-SourceHash.Tests.ps1
    if ($LASTEXITCODE -ne 0) { throw 'Source hash behavior tests failed.' }
    & (Join-Path $PSHOME 'pwsh') -NoProfile -File tests/quality/Test-CanonicalPackageHashes.Tests.ps1
    if ($LASTEXITCODE -ne 0) { throw 'Canonical package hash behavior tests failed.' }

    # A new directory per invocation prevents old reports from contributing to this run.
    foreach ($suite in @('UnitTests', 'ContractTests', 'IntegrationTests', 'PlatformTests')) {
        $project = "tests/Monstertacle.Multisession.$suite/Monstertacle.Multisession.$suite.csproj"
        $arguments = @('test', $project, '-c', 'Release', '--no-build', '--no-restore',
            '--settings', 'tests/coverage.runsettings', '--collect', 'XPlat Code Coverage',
            '--logger', 'trx', '--results-directory', (Join-Path $runDirectory $suite))
        if ($suite -eq 'PlatformTests') {
            $framework = if ($Platform -eq 'windows') { 'net10.0-windows' } else { 'net10.0' }
            $arguments += @('-f', $framework)
        }
        Invoke-Dotnet $arguments
    }

    # Restore already enforces advisory warnings as errors, including transitive packages.
    $audit = & dotnet list Monstertacle.Multisession.slnx package --vulnerable --include-transitive --no-restore --format json
    if ($LASTEXITCODE -ne 0) { throw 'Dependency audit command failed.' }
    $audit | Set-Content (Join-Path $runDirectory 'dependency-audit.json') -Encoding utf8

    if ((Get-SourceHash) -ne $sourceHash) { throw 'Sources changed during quality validation; rerun on one consistent state.' }
    $summary = & (Join-Path $PSHOME 'pwsh') -NoProfile -File scripts/Test-Coverage.ps1 -ReportDirectory $runDirectory -Platform $Platform
    $coverageExitCode = $LASTEXITCODE
    $summary | Write-Output
    $summary | Set-Content (Join-Path $runDirectory 'coverage-summary.json') -Encoding utf8
    Write-Output "Quality artifacts: $runDirectory"
    if ($coverageExitCode -ne 0) { throw 'Product coverage did not pass; release remains blocked.' }
}
catch {
    [Console]::Error.WriteLine($_.Exception.Message)
    exit 1
}
finally {
    Pop-Location
}
