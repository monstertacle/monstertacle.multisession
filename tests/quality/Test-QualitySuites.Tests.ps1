$ErrorActionPreference = 'Stop'
# The quality run must test every test project of the solution: a project that exists but is not a suite, or a suite
# that does not exist, has to fail instead of being skipped silently.
$repository = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$script = Join-Path $repository 'scripts/Invoke-Quality.ps1'
$tokens = $null
$errors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile($script, [ref]$tokens, [ref]$errors)
if ($errors.Count -ne 0) { throw 'Quality script could not be parsed.' }
$function = $ast.Find({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Get-QualitySuites' }, $true)
if ($null -eq $function) { throw 'Missing quality suite function.' }
. ([scriptblock]::Create($function.Extent.Text))

$temporary = Join-Path ([Environment]::GetFolderPath('UserProfile')) ('dev/.agenttemp/multisession-suites-' + [Guid]::NewGuid().ToString('N'))
function New-Fixture([string]$Name, [string[]]$Listed, [string[]]$OnDisk) {
    $fixture = Join-Path $temporary $Name
    foreach ($suite in $OnDisk) {
        $directory = Join-Path $fixture "tests/Monstertacle.Multisession.$suite"
        $null = New-Item -Path $directory -ItemType Directory -Force
        Set-Content -LiteralPath (Join-Path $directory "Monstertacle.Multisession.$suite.csproj") -Value '<Project />'
    }
    $projects = ($Listed | ForEach-Object { "    <Project Path=`"tests/Monstertacle.Multisession.$_/Monstertacle.Multisession.$_.csproj`" />" }) -join "`n"
    $null = New-Item -Path $fixture -ItemType Directory -Force
    Set-Content -LiteralPath (Join-Path $fixture 'Monstertacle.Multisession.slnx') -Value @"
<Solution>
  <Folder Name="/src/">
    <Project Path="src/Monstertacle.Multisession.Common/Monstertacle.Multisession.Common.csproj" />
  </Folder>
  <Folder Name="/tests/">
$projects
  </Folder>
</Solution>
"@
    return $fixture
}
function Assert-Refused([string]$Name, [string[]]$Listed, [string[]]$OnDisk) {
    $fixture = New-Fixture $Name $Listed $OnDisk
    try { $null = Get-QualitySuites $fixture } catch { Write-Host "PASS refused: $Name"; return }
    throw "Case '$Name' was not refused."
}
try {
    $fixture = New-Fixture 'complete' @('UnitTests', 'Checkpoints.Tests', 'Checkpoints.Delivery.Tests') @('UnitTests', 'Checkpoints.Tests', 'Checkpoints.Delivery.Tests')
    $suites = @(Get-QualitySuites $fixture)
    if (($suites -join ',') -cne 'UnitTests,Checkpoints.Tests,Checkpoints.Delivery.Tests') { throw "Unexpected suites: $($suites -join ',')" }
    Write-Host 'PASS suites follow the solution test projects'
    Assert-Refused 'project-not-in-solution' @('UnitTests') @('UnitTests', 'Checkpoints.Tests')
    Assert-Refused 'solution-project-missing' @('UnitTests', 'Checkpoints.Tests') @('UnitTests')
    Assert-Refused 'no-test-projects' @() @()
    $actual = @(Get-QualitySuites $repository)
    foreach ($required in @('UnitTests', 'ContractTests', 'IntegrationTests', 'PlatformTests', 'Checkpoints.Tests', 'Checkpoints.Delivery.Tests')) {
        if ($required -cnotin $actual) { throw "The repository suites lack $required." }
    }
    Write-Host "PASS repository suites: $($actual -join ', ')"
} finally {
    if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Recurse -Force }
}
