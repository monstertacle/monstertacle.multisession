$ErrorActionPreference = 'Stop'
$repository = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$script = Join-Path $repository 'scripts/Invoke-Quality.ps1'
$tokens = $null
$errors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile($script, [ref]$tokens, [ref]$errors)
if ($errors.Count -ne 0) { throw 'Quality script could not be parsed.' }
$function = $ast.Find({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Get-SourceHash' }, $true)
if ($null -eq $function) { throw 'Missing source hash function.' }
. ([scriptblock]::Create($function.Extent.Text))
$root = Join-Path ([Environment]::GetFolderPath('UserProfile')) ('dev/.agenttemp/multisession-sourcehash-' + [Guid]::NewGuid().ToString('N'))
$null = New-Item -Path $root -ItemType Directory
try {
    foreach ($directory in @('.config', 'src', 'tests', 'examples', 'scripts', 'eng', '.github/workflows', 'artifacts', 'src/bin')) {
        $null = New-Item -Path (Join-Path $root $directory) -ItemType Directory -Force
    }
    foreach ($file in @('global.json', 'Directory.Build.props', 'Directory.Packages.props', '.editorconfig', 'Monstertacle.Multisession.slnx', 'codemetr.json', '.config/dotnet-tools.json', 'README.md')) {
        Set-Content -LiteralPath (Join-Path $root $file) -Value 'fixture'
    }
    Push-Location $root
    try {
        foreach ($file in @('README.md', 'eng/PackageSmoke.ps1', 'tests/PackageConsumer.cs.txt')) {
            $before = Get-SourceHash
            Set-Content -LiteralPath (Join-Path $root $file) -Value 'changed package input'
            if ((Get-SourceHash) -eq $before) { throw "Source hash omitted package input: $file" }
            Write-Host "PASS package input changes source hash: $file"
        }
        $before = Get-SourceHash
        Set-Content -LiteralPath (Join-Path $root 'artifacts/unrelated.cs') -Value 'output'
        Set-Content -LiteralPath (Join-Path $root 'src/bin/Generated.cs') -Value 'output'
        if ((Get-SourceHash) -ne $before) { throw 'Build outputs unexpectedly changed source hash.' }
        Write-Host 'PASS generated outputs do not invalidate source hash'
    } finally { Pop-Location }
} finally { Remove-Item -LiteralPath $root -Recurse -Force }
