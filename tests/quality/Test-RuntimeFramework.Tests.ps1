$ErrorActionPreference = 'Stop'
$path = Join-Path $PSScriptRoot '../../scripts/Invoke-Quality.ps1'
$tokens = $null; $errors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile($path, [ref]$tokens, [ref]$errors)
if ($errors.Count -ne 0) { throw 'Quality script failed to parse.' }
$function = $ast.Find({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Get-QualityFramework' }, $true)
if ($null -eq $function) { throw 'Missing framework selector.' }
. ([scriptblock]::Create($function.Extent.Text))
foreach ($major in @(10, 11)) {
    foreach ($platform in @('linux', 'windows')) {
        foreach ($suite in @('UnitTests', 'ContractTests', 'IntegrationTests', 'PlatformTests', 'Checkpoints.Tests', 'Checkpoints.Delivery.Tests')) {
            $expected = "net$major.0"
            if ($platform -eq 'windows' -and $suite -in @('UnitTests', 'IntegrationTests', 'PlatformTests')) { $expected += '-windows' }
            $actual = Get-QualityFramework $suite $platform $major
            if ($actual -cne $expected) { throw "Wrong runtime: $suite/$platform/$major selected $actual; expected $expected." }
        }
    }
}
Write-Output 'PASS all native suite/runtime routing combinations.'
