[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw 'The volume formatting diagnostic requires a native Windows host.' }

# This diagnostic never fills a volume and never selects an existing physical disk.
$probeId = [Guid]::NewGuid().ToString('N')
$probeRoot = Join-Path ([Environment]::GetFolderPath('UserProfile')) "dev/.agenttemp/volume-format-$probeId"
$reportRoot = Join-Path $PSScriptRoot "../artifacts/quality/windows/volume-format-$probeId"
if ([IO.Directory]::Exists($probeRoot)) { throw 'The private probe directory already exists.' }
[IO.Directory]::CreateDirectory($probeRoot) | Out-Null
[IO.Directory]::CreateDirectory($reportRoot) | Out-Null
$script:lastDiskpartExit = $null

function Invoke-PrivateDiskpart([string] $Name, [string] $Commands) {
    if ($Commands.ToCharArray().Where({ [int] $_ -gt 127 }).Count -ne 0) { throw 'Diskpart probe paths must be ASCII.' }
    if ($null -ne $script:lastDiskpartExit) {
        $remaining = 15 - [Diagnostics.Stopwatch]::GetElapsedTime($script:lastDiskpartExit).TotalSeconds
        if ($remaining -gt 0) { Start-Sleep -Milliseconds ([int][Math]::Ceiling($remaining * 1000)) }
    }
    $scriptPath = Join-Path $probeRoot "$Name.txt"
    [IO.File]::WriteAllText($scriptPath, $Commands.Replace("`n", "`r`n"), [Text.Encoding]::ASCII)
    $start = [Diagnostics.ProcessStartInfo]::new('diskpart.exe')
    $start.UseShellExecute = $false
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.ArgumentList.Add('/s')
    $start.ArgumentList.Add($scriptPath)
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $start
    try {
        if (-not $process.Start()) { throw 'Diskpart did not start.' }
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(60000)) { throw 'Diskpart exceeded its 60-second diagnostic deadline.' }
        $result = [pscustomobject]@{
            MeasuredUtc = [DateTime]::UtcNow.ToString('O')
            ExitCode = $process.ExitCode
            Output = $stdout.GetAwaiter().GetResult()
            Error = $stderr.GetAwaiter().GetResult()
        }
        $result | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $reportRoot "$Name.json") -Encoding utf8
        Write-Host "$Name exit=$($result.ExitCode)"
        Write-Host $result.Output
        Write-Host $result.Error
        return $result
    }
    finally {
        try {
            if ($null -ne $process.SafeHandle -and -not $process.HasExited) {
                $process.Kill($true)
                if (-not $process.WaitForExit(5000)) { throw 'Diskpart did not exit after termination.' }
            }
        }
        finally {
            $process.Dispose()
            $script:lastDiskpartExit = [Diagnostics.Stopwatch]::GetTimestamp()
        }
    }
}

try {
    $cases = @(
        @{ Size = 64; Mode = 'inherited' }, @{ Size = 128; Mode = 'inherited' },
        @{ Size = 256; Mode = 'inherited' }, @{ Size = 512; Mode = 'inherited' },
        @{ Size = 64; Mode = 'private' }, @{ Size = 64; Mode = 'system' }
    )
    foreach ($case in $cases) {
        $size = $case.Size
        $mode = $case.Mode
        $name = "$size-$mode"
        $imageRoot = Join-Path $probeRoot $name
        if ($mode -eq 'inherited') { [IO.Directory]::CreateDirectory($imageRoot) | Out-Null }
        else {
            $owner = [Security.Principal.WindowsIdentity]::GetCurrent().User
            $security = [Security.AccessControl.DirectorySecurity]::new()
            $security.SetOwner($owner)
            $security.SetAccessRuleProtection($true, $false)
            $inherit = [Security.AccessControl.InheritanceFlags]::ContainerInherit -bor [Security.AccessControl.InheritanceFlags]::ObjectInherit
            $security.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($owner, 'FullControl', $inherit, 'None', 'Allow'))
            if ($mode -eq 'system') {
                $systemSid = [Security.Principal.SecurityIdentifier]::new([Security.Principal.WellKnownSidType]::LocalSystemSid, $null)
                $security.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($systemSid, 'FullControl', $inherit, 'None', 'Allow'))
            }
            [IO.FileSystemAclExtensions]::Create([IO.DirectoryInfo]::new($imageRoot), $security)
        }
        $image = Join-Path $imageRoot 'fresh.vhd'
        if ([IO.File]::Exists($image)) { throw 'The diagnostic image already exists.' }
        try {
            $commands = "create vdisk file=`"$image`" maximum=$size type=expandable`nattach vdisk`ncreate partition primary`nfilesystems`nformat fs=ntfs quick label=msprobe`nexit`n"
            $result = Invoke-PrivateDiskpart "format-$name" $commands
            Write-Host "Diagnostic size=$size MiB mode=$mode formatted=$($result.ExitCode -eq 0); this does not replace the quality gate."
        }
        finally {
            if ([IO.File]::Exists($image)) {
                $null = Invoke-PrivateDiskpart "detach-$name" "select vdisk file=`"$image`"`ndetach vdisk`nexit`n"
                [IO.File]::Delete($image) # A still attached image must fail cleanup visibly.
            }
        }
    }
}
finally {
    # Leave a failed attachment visible instead of claiming successful resource cleanup.
    if (@([IO.Directory]::EnumerateFiles($probeRoot, '*.vhd', [IO.SearchOption]::AllDirectories)).Count -eq 0) { [IO.Directory]::Delete($probeRoot, $true) }
}
