param(
    [Parameter(Mandatory)][string]$ReportDirectory,
    [Parameter(Mandatory)][ValidateSet('windows', 'linux')][string]$Platform,
    [string]$ProjectRoot = (Split-Path $PSScriptRoot -Parent),
    [string]$ScopePath = (Join-Path (Split-Path $PSScriptRoot -Parent) 'tests/coverage-scope.json')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

try {
    $scope = Get-Content -LiteralPath $ScopePath -Raw | ConvertFrom-Json -AsHashtable
    $threshold = [decimal]$scope.thresholdPercent
    if ($threshold -lt 87 -or $threshold -gt 100) { throw 'Coverage threshold must be between 87 and 100 percent.' }
    $allModules = @($scope.commonAssemblies) + @($scope.platformAssemblies.Values)
    if (@($allModules | Sort-Object -Unique).Count -ne $allModules.Count) { throw 'Duplicate product modules in scope inventory.' }
    $expected = @($scope.commonAssemblies) + $scope.platformAssemblies[$Platform]
    $sourceDirectory = Join-Path $ProjectRoot $scope.productDirectory
    $projects = @(Get-ChildItem -LiteralPath $sourceDirectory -Filter '*.csproj' -Recurse -File |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } |
        ForEach-Object { $_.BaseName })
    if ($projects.Count -ne $allModules.Count -or @(Compare-Object $allModules $projects).Count -ne 0) {
        throw 'Product project inventory does not match the complete registered coverage scope.'
    }

    if (-not (Test-Path -LiteralPath $ReportDirectory -PathType Container)) { throw 'Coverage report directory is missing.' }
    $reports = @(Get-ChildItem -LiteralPath $ReportDirectory -Filter 'coverage.json' -Recurse -File)
    if ($reports.Count -eq 0) { throw 'No collector JSON reports found.' }

    $comparer = if ($Platform -eq 'windows') { [StringComparer]::OrdinalIgnoreCase } else { [StringComparer]::Ordinal }
    $lineHits = [Collections.Generic.Dictionary[string, bool]]::new($comparer)
    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)

    foreach ($report in $reports) {
        $data = Get-Content -LiteralPath $report.FullName -Raw | ConvertFrom-Json -AsHashtable
        if ($data -isnot [Collections.IDictionary]) { throw "Invalid collector report: $($report.Name)" }
        foreach ($moduleFile in $data.Keys) {
            $module = [IO.Path]::GetFileNameWithoutExtension($moduleFile)
            if ($module -cnotin $expected) { throw "Unexpected assembly in platform report: $module" }
            $null = $seen.Add($module)
            $documents = $data[$moduleFile]
            if ($documents -isnot [Collections.IDictionary]) { throw "Invalid document map for $module" }
            foreach ($document in $documents.Keys) {
                $path = [IO.Path]::GetFullPath($document.Replace('\', '/'), [IO.Path]::GetFullPath($ProjectRoot)).Replace('\', '/')
                $types = $documents[$document]
                if ($types -isnot [Collections.IDictionary]) { throw 'Invalid type map.' }
                foreach ($type in $types.Values) {
                    if ($type -isnot [Collections.IDictionary]) { throw 'Invalid method map.' }
                    foreach ($method in $type.Values) {
                        if ($method -isnot [Collections.IDictionary] -or $method.Lines -isnot [Collections.IDictionary]) {
                            throw 'Invalid source-line map.'
                        }
                        foreach ($entry in $method.Lines.GetEnumerator()) {
                            $number = 0
                            if (-not [int]::TryParse($entry.Key, [ref]$number) -or $number -le 0) { throw 'Invalid source line number.' }
                            $hits = $entry.Value
                            if (($hits -isnot [int] -and $hits -isnot [long]) -or $hits -lt 0) { throw 'Invalid line hit count.' }
                            $key = "$module`n$path`n$number"
                            $covered = $hits -gt 0
                            if ($lineHits.ContainsKey($key)) { $covered = $covered -or $lineHits[$key] }
                            $lineHits[$key] = $covered
                        }
                    }
                }
            }
        }
    }

    foreach ($module in $expected) {
        if (-not $seen.Contains($module)) { throw "Missing product assembly: $module" }
    }
    $total = $lineHits.Count
    if ($total -eq 0) { throw 'Zero measured product lines cannot pass coverage.' }
    $covered = @($lineHits.Values | Where-Object { $_ }).Count
    $passed = [decimal]100 * $covered -ge $threshold * $total
    [ordered]@{
        Platform = $Platform
        ThresholdPercent = $threshold
        TotalLines = $total
        CoveredLines = $covered
        Percent = [math]::Round([decimal]100 * $covered / $total, 4)
        Modules = @($expected | Sort-Object)
        Reports = $reports.Count
        Passed = $passed
    } | ConvertTo-Json -Depth 4 | Write-Output
    if (-not $passed) { exit 1 }
    exit 0
}
catch {
    [Console]::Error.WriteLine("Coverage gate failed: $($_.Exception.Message)")
    exit 1
}
