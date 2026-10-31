[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$HostAssembly,
    [string]$StateRoot,
    [string]$ReportDirectory = 'artifacts/validation-scenarios'
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$assembly = (Resolve-Path -LiteralPath $HostAssembly).Path
if ([string]::IsNullOrWhiteSpace($StateRoot)) {
    $base = Join-Path ([Environment]::GetFolderPath('UserProfile')) 'dev/.agenttemp'
    $null = [IO.Directory]::CreateDirectory($base)
    $StateRoot = Join-Path $base ('multisession-published-' + [Guid]::NewGuid().ToString('N'))
}
if (-not [IO.Path]::IsPathFullyQualified($StateRoot) -or (Test-Path -LiteralPath $StateRoot) -or
    -not [IO.Directory]::Exists((Split-Path $StateRoot -Parent))) { throw 'Expected a new absolute probe directory beneath an existing parent.' }
if ($IsWindows) {
    $owner = [Security.Principal.WindowsIdentity]::GetCurrent().User
    $security = [Security.AccessControl.DirectorySecurity]::new()
    $security.SetOwner($owner)
    $security.SetAccessRuleProtection($true, $false)
    $security.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($owner, 'FullControl', 'ContainerInherit, ObjectInherit', 'None', 'Allow'))
    [IO.FileSystemAclExtensions]::Create([IO.DirectoryInfo]::new($StateRoot), $security)
} else {
    $null = [IO.Directory]::CreateDirectory($StateRoot, [IO.UnixFileMode]::UserRead -bor [IO.UnixFileMode]::UserWrite -bor [IO.UnixFileMode]::UserExecute)
}
$reports = Join-Path ([IO.Path]::GetFullPath($ReportDirectory)) ([Guid]::NewGuid().ToString('N'))
$null = New-Item -Path $reports -ItemType Directory -Force
$assemblyHash = (Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash.ToLowerInvariant()
$payload = @(Get-ChildItem -LiteralPath (Split-Path $assembly -Parent) -Recurse -File | Sort-Object FullName | ForEach-Object {
    [ordered]@{ File = [IO.Path]::GetRelativePath((Split-Path $assembly -Parent), $_.FullName).Replace('\', '/'); Sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
})
$results = [Collections.Generic.List[object]]::new()
$scenarios = @('deterministic', 'routing', 'failures', 'cancellation', 'shutdown', 'capacity', 'workspace', 'extensions', 'recovery', 'native-context', 'isolation', 'embedded-consumer', 'retention', 'limits')
foreach ($scenario in $scenarios) {
    $start = [Diagnostics.ProcessStartInfo]::new('dotnet')
    $start.UseShellExecute = $false
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.WorkingDirectory = $StateRoot
    foreach ($argument in @($assembly, '--scenario', $scenario, '--state-root', (Join-Path $StateRoot $scenario))) { $start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $start
    $elapsed = [Diagnostics.Stopwatch]::StartNew()
    if (-not $process.Start()) { $process.Dispose(); throw "Could not start scenario: $scenario" }
    $output = $process.StandardOutput.ReadToEndAsync()
    $errorOutput = $process.StandardError.ReadToEndAsync()
    try {
        if (-not $process.WaitForExit(300000)) { throw "Scenario exceeded five-minute process budget: $scenario" }
        $text = $output.GetAwaiter().GetResult()
        $stderr = $errorOutput.GetAwaiter().GetResult()
        [IO.File]::WriteAllText((Join-Path $reports "$scenario.json"), $text)
        [IO.File]::WriteAllText((Join-Path $reports "$scenario.stderr.log"), $stderr)
        $report = $text | ConvertFrom-Json
        if ($process.ExitCode -ne 0 -or $report.Status -ne 'PASS' -or $report.Scenario -ne $scenario) { throw "Selected scenario failed: $scenario" }
        if ([string]::IsNullOrWhiteSpace($report.RuntimeVersion) -or [string]::IsNullOrWhiteSpace($report.SqliteVersion)) { throw "Missing native version evidence: $scenario" }
        $results.Add([ordered]@{ Scenario = $scenario; Status = $report.Status; ElapsedSeconds = $elapsed.Elapsed.TotalSeconds; RunId = $report.RunId; RuntimeVersion = $report.RuntimeVersion; SqliteVersion = $report.SqliteVersion; OperatingSystem = $report.OperatingSystem })
        Write-Host "PASS published scenario: $scenario"
    } finally {
        if (-not $process.HasExited) { $process.Kill($true) }
        if (-not $process.WaitForExit(30000)) { throw "Owned scenario process did not exit: $scenario" }
        $process.Dispose()
    }
}
[ordered]@{ CompletedUtc = [DateTimeOffset]::UtcNow.ToString('O'); HostAssemblySha256 = $assemblyHash; Payload = $payload; StateRoot = $StateRoot; Scenarios = $results } |
    ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $reports 'manifest.json')
Write-Host "PASS published scenarios report: $reports"
