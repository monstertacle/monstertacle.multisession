[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repository = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$gate = Join-Path $repository 'scripts/Test-Coverage.ps1'
$scopePath = Join-Path $repository 'tests/coverage-scope.json'
$scope = Get-Content $scopePath -Raw | ConvertFrom-Json
$modules = @($scope.commonAssemblies) + $scope.platformAssemblies.windows
$allModules = @($scope.commonAssemblies) + $scope.platformAssemblies.windows + $scope.platformAssemblies.linux
$base = Join-Path ([IO.Path]::GetTempPath()) ('multisession-coverage-tests-' + [Guid]::NewGuid().ToString('N'))
$null = New-Item $base -ItemType Directory
$failures = 0

function New-Case([string]$Name) {
    $root = Join-Path $base $Name
    $reports = Join-Path $root 'reports'
    $null = New-Item $reports -ItemType Directory -Force
    foreach ($module in $allModules) {
        $directory = Join-Path $root "src/$module"
        $null = New-Item $directory -ItemType Directory -Force
        [IO.File]::WriteAllText((Join-Path $directory "$module.csproj"), '<Project />')
    }
    return @{ Root = $root; Reports = $reports }
}

function New-Report([hashtable]$Case, [int]$Covered = 9, [int]$Offset = 0, [int]$Total = 10) {
    $report = @{}
    foreach ($module in $modules) {
        $lines = @{}
        foreach ($number in 1..$Total) {
            $lines["$number"] = [int]($number -gt $Offset -and $number -le ($Offset + $Covered))
        }
        $file = Join-Path $Case.Root "src/$module/Example.cs"
        $report["$module.dll"] = @{ $file = @{ 'Fixture.Type' = @{ 'System.Void Fixture.Type::Run()' = @{ Lines = $lines; Branches = @() } } } }
    }
    return $report
}

function Write-Report([hashtable]$Case, [hashtable]$Report, [string]$Name = 'coverage.json') {
    # Synthetic collector data tests gate behavior; it is not a real product coverage result.
    [IO.File]::WriteAllText((Join-Path $Case.Reports $Name), ($Report | ConvertTo-Json -Depth 12))
}

function Assert-Gate([string]$Name, [hashtable]$Case, [int]$Expected, [int]$ExpectedLines = 0) {
    $caseScope = if ($Case.ContainsKey('ScopePath')) { $Case.ScopePath } else { $scopePath }
    $output = & (Join-Path $PSHOME 'pwsh') -NoProfile -File $gate -ReportDirectory $Case.Reports -Platform windows -ProjectRoot $Case.Root -ScopePath $caseScope 2>&1
    $code = $LASTEXITCODE
    if ($code -ne $Expected) {
        Write-Output "FAIL $Name`: expected exit $Expected, actual $code. $($output -join ' ')"
        $script:failures++
        return
    }
    if ($ExpectedLines -gt 0) {
        if ([string]::IsNullOrWhiteSpace(($output -join "`n"))) {
            Write-Output "FAIL $Name`: successful gate returned no measured line counts."
            $script:failures++
            return
        }
        $summary = ($output -join "`n") | ConvertFrom-Json
        if ($summary.TotalLines -ne $ExpectedLines -or $summary.CoveredLines -ne 45) {
            Write-Output "FAIL $Name`: duplicate/union counts are incorrect."
            $script:failures++
            return
        }
    }
    Write-Output "PASS $Name"
}

$case = New-Case 'exact-87'
Write-Report $case (New-Report $case -Covered 87 -Total 100)
Assert-Gate 'exactly 87 percent passes' $case 0

$case = New-Case 'below-87'
Write-Report $case (New-Report $case -Covered 86 -Total 100)
Assert-Gate 'below 87 percent fails' $case 1

foreach ($invalidThreshold in @(86, 101)) {
    $case = New-Case "invalid-threshold-$invalidThreshold"
    Write-Report $case (New-Report $case -Covered 10)
    $case.ScopePath = Join-Path $case.Root 'scope.json'
    $invalidScope = Get-Content $scopePath -Raw | ConvertFrom-Json
    $invalidScope.thresholdPercent = $invalidThreshold
    $invalidScope | ConvertTo-Json -Depth 8 | Set-Content $case.ScopePath
    Assert-Gate "threshold $invalidThreshold is rejected" $case 1
}

$case = New-Case 'missing'
$case.Reports = Join-Path $case.Root 'not-created'
Assert-Gate 'missing reports fail' $case 1

$case = New-Case 'no-reports'
Assert-Gate 'empty report directory fails' $case 1

$case = New-Case 'zero-lines'
$report = @{}
foreach ($module in $modules) { $report["$module.dll"] = @{} }
Write-Report $case $report
Assert-Gate 'zero measured lines fail' $case 1

$case = New-Case 'invalid-json'
[IO.File]::WriteAllText((Join-Path $case.Reports 'coverage.json'), '{')
Assert-Gate 'invalid JSON fails' $case 1

$case = New-Case 'missing-native-assembly'
$report = New-Report $case
$report.Remove("$($scope.platformAssemblies.windows).dll")
Write-Report $case $report
Assert-Gate 'missing native product assembly fails' $case 1

$case = New-Case 'unregistered-project'
Write-Report $case (New-Report $case)
$extra = Join-Path $case.Root 'src/Extra'
$null = New-Item $extra -ItemType Directory
[IO.File]::WriteAllText((Join-Path $extra 'Extra.csproj'), '<Project />')
Assert-Gate 'product projects outside inventory fail' $case 1

$case = New-Case 'duplicate-reports'
$report = New-Report $case
Write-Report $case $report
$subdirectory = Join-Path $case.Reports 'second-suite'
$null = New-Item $subdirectory -ItemType Directory
[IO.File]::WriteAllText((Join-Path $subdirectory 'coverage.json'), ($report | ConvertTo-Json -Depth 12))
Assert-Gate 'duplicate reports count source lines only once' $case 0 50

$case = New-Case 'complementary-reports'
Write-Report $case (New-Report $case 5)
$subdirectory = Join-Path $case.Reports 'second-suite'
$null = New-Item $subdirectory -ItemType Directory
[IO.File]::WriteAllText((Join-Path $subdirectory 'coverage.json'), ((New-Report $case 4 5) | ConvertTo-Json -Depth 12))
Assert-Gate 'complementary suites merge line hits rather than percentages' $case 0 50

$case = New-Case 'invalid-hits'
$report = New-Report $case
$firstModule = "$($modules[0]).dll"
$file = @($report[$firstModule].Keys)[0]
$report[$firstModule][$file]['Fixture.Type']['System.Void Fixture.Type::Run()'].Lines['1'] = -1
Write-Report $case $report
Assert-Gate 'negative hit counts fail' $case 1

# Only remove this process's unique, synthetic fixture tree.
[IO.Directory]::Delete($base, $true)
if ($failures -gt 0) { throw "$failures coverage-gate behavior tests failed." }
Write-Output 'All coverage-gate behavior tests passed.'
