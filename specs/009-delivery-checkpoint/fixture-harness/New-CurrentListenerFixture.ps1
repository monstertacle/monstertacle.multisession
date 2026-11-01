[CmdletBinding()]
param(
    [string]$Release = (Join-Path ([Environment]::GetFolderPath('UserProfile')) '.local/share/sharpclaw-event-listener/releases/2c4a8af153f866d09bfb564fa88d4ac2856b20ec'),
    [string]$ExpectedCommit = '2c4a8af153f866d09bfb564fa88d4ac2856b20ec',
    [string]$Output = (Join-Path $PSScriptRoot '../../../tests/Monstertacle.Multisession.Delivery.Tests/Fixtures/CurrentListener'),
    [string]$TempRoot = (Join-Path ([Environment]::GetFolderPath('UserProfile')) 'dev/.agenttemp')
)
# Fixture-only: runs the hash-verified current Listener release against private copies,
# private configurations and fixture feed/RPC processes. It never reads or writes live state.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $IsLinux) { throw 'The current Listener release is Linux-only; generate the fixture on Linux.' }

$manifest = Get-Content -LiteralPath (Join-Path $Release 'bundle-release.json') -Raw | ConvertFrom-Json -AsHashtable
if ($manifest.commit -cne $ExpectedCommit) { throw "Release commit $($manifest.commit) is not $ExpectedCommit." }
foreach ($name in @('SharpClaw.Listener.dll', 'SharpClaw.Protocol.dll', 'SharpClaw.dll', 'Microsoft.Data.Sqlite.dll', 'SharpClaw.Listener.deps.json', 'SharpClaw.Listener.runtimeconfig.json')) {
    $actual = (Get-FileHash -LiteralPath (Join-Path $Release $name) -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -cne $manifest.files[$name]) { throw "Release file hash mismatch: $name" }
}
$listener = Join-Path $Release 'SharpClaw.Listener.dll'
$dotnet = (Get-Command dotnet).Source
$dotnet = [IO.Path]::GetFullPath((Get-Item -LiteralPath $dotnet).ResolvedTarget ?? $dotnet)

$work = Join-Path ([IO.Path]::GetFullPath($TempRoot)) ('delivery-fixture-' + [Guid]::NewGuid().ToString('N').Substring(0, 12))
$null = [IO.Directory]::CreateDirectory($work, [IO.UnixFileMode]'UserRead, UserWrite, UserExecute')
# Fixture-only programs are templates (like eng/*.cs.txt): built in the private work directory, never part of the
# product solution or its coverage. Codemetr still measures their sources explicitly.
$source = Join-Path $work 'harness-source'
$null = New-Item -Path $source -ItemType Directory
foreach ($template in Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.txt' -File) {
    Copy-Item -LiteralPath $template.FullName -Destination (Join-Path $source $template.BaseName)
}
$sources = @(Get-ChildItem -LiteralPath $source -Filter '*.cs' -File | ForEach-Object FullName)
Push-Location (Join-Path $PSScriptRoot '../../..')
try { $sources | & $dotnet tool run codemetr-csharp -- check --files-from - --format json > (Join-Path $work 'codemetr.json') }
finally { Pop-Location }
if ($LASTEXITCODE -ne 0) { throw 'Fixture harness Codemetr check failed.' }
$harness = Join-Path $work 'harness'
& $dotnet restore (Join-Path $source 'FixtureHarness.csproj') --locked-mode -v quiet | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Fixture harness locked restore failed.' }
& $dotnet build (Join-Path $source 'FixtureHarness.csproj') -c Release -o $harness --no-restore --nologo -v quiet | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Fixture harness build failed.' }
$harnessDll = Join-Path $harness 'FixtureHarness.dll'

$socket = Join-Path $work 'rpc.sock'; $modeFile = Join-Path $work 'mode'; $sendLog = Join-Path $work 'sends.log'
Set-Content -LiteralPath $modeFile -Value 'unready' -NoNewline
$null = New-Item -Path $sendLog -ItemType File
$server = Start-Process -FilePath $dotnet -ArgumentList @($harnessDll, 'rpc', $socket, $modeFile, $sendLog) -PassThru -RedirectStandardError (Join-Path $work 'rpc.err')
$transcript = [Collections.Generic.List[object]]::new()
try {
    $deadline = [DateTime]::UtcNow.AddSeconds(20)
    while (-not (Test-Path -LiteralPath $socket) -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 100 }
    if (-not (Test-Path -LiteralPath $socket)) { throw 'RPC fixture did not start.' }

    $artifact = { param($cursor, $id) [ordered]@{ version = 1; cursor = $cursor; eventId = $id; type = 'artifact.published'; observedUtc = '2026-11-01T00:00:00+00:00'
            artifact = [ordered]@{ projectId = 'fixture-project'; artifactId = "fixture-artifact-$cursor"; sha256 = ('ab' * 32); bytes = 42; artifactCreatedUtc = '2026-11-01T00:00:00+00:00' } } }
    $events = @((& $artifact 1 '11111111-1111-4111-8111-111111111111'), (& $artifact 2 '22222222-2222-4222-8222-222222222222'))
    $eventsPath = Join-Path $work 'events.json'
    ConvertTo-Json -InputObject $events -Depth 8 | Set-Content -LiteralPath $eventsPath

    function New-Config([string]$Name, [string]$Target, [string]$State) {
        $config = [ordered]@{ schema = 1; projectId = 'fixture-project'; instanceId = 'fixture-instance'
            source = [ordered]@{ executable = $dotnet; arguments = @($harnessDll, 'feed', $eventsPath) }
            delivery = [ordered]@{ type = 'codex'; targetId = $Target; socketPath = $socket }
            statePath = (Join-Path $work "$State/checkpoint.db"); pollSeconds = 5; initialCursor = 0 }
        $path = Join-Path $work "$Name.json"
        $config | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $path
        return $path
    }
    function Invoke-Listener([string]$Step, [int]$Expected, [string[]]$Arguments, [string]$Reply) {
        Set-Content -LiteralPath $modeFile -Value $Reply -NoNewline
        $out = Join-Path $work "$Step.out"; $err = Join-Path $work "$Step.err"
        & $dotnet $listener @Arguments > $out 2> $err
        $code = $LASTEXITCODE
        $entry = [ordered]@{ step = $Step; mode = $Reply; arguments = @($Arguments | ForEach-Object { [IO.Path]::GetFileName($_) }); exitCode = $code
            stdout = (Get-Content -LiteralPath $out -Raw) ?? ''; stderr = (Get-Content -LiteralPath $err -Raw) ?? '' }
        $transcript.Add($entry)
        if ($code -ne $Expected) { throw "Step $Step exited $code, expected $Expected`: $($entry.stderr)" }
        return $entry.stdout.TrimEnd("`n")
    }
    function Write-Request([string]$Name, [string]$Id, [string]$KeyJson, [switch]$Replay) {
        $request = [ordered]@{ schema = 1; requestId = $Id; expected = ($KeyJson | ConvertFrom-Json -AsHashtable) }
        if ($Replay) { $request.acceptPossibleDuplicate = $true }
        $path = Join-Path $work "$Name.json"
        $request | ConvertTo-Json -Depth 8 -Compress | Set-Content -LiteralPath $path
        return $path
    }

    # Custody fixture: exhausted preparation, granted rearm, lost acknowledgement, single replay, then a route move.
    $before = New-Config 'custody-before' 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa' 'custody'
    $after = New-Config 'custody-after' 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb' 'custody'
    foreach ($attempt in 1..3) { $null = Invoke-Listener "prepare-$attempt" 1 @('once', $before) 'unready' }
    $null = Invoke-Listener 'exhausted' 3 @('once', $before) 'unready'
    $rearmKey = Invoke-Listener 'preparation-key' 0 @('preparation-key', $before) 'idle'
    $rearm = Write-Request 'rearm-request' 'c0000000-0000-4000-8000-000000000001' $rearmKey
    $rearmReceipt = Invoke-Listener 'preparation-rearm' 0 @('preparation-rearm', $before, $rearm) 'idle'
    $rearmAgain = Invoke-Listener 'preparation-rearm-repeat' 0 @('preparation-rearm', $before, $rearm) 'idle'
    if ($rearmAgain -cne $rearmReceipt) { throw 'Repeated rearm did not return the stored receipt.' }
    $null = Invoke-Listener 'lost-acknowledgement' 3 @('once', $before) 'lose'
    $replayKey = Invoke-Listener 'delivery-key' 0 @('delivery-key', $before) 'accept'
    $replay = Write-Request 'replay-request' 'c0000000-0000-4000-8000-000000000002' $replayKey -Replay
    $replayReceipt = Invoke-Listener 'delivery-replay' 0 @('delivery-replay', $before, $replay) 'accept'
    $null = Invoke-Listener 'stage-second' 1 @('once', $before) 'unready'
    $rebindKey = Invoke-Listener 'delivery-rebind-key' 0 @('delivery-rebind-key', $before, $after) 'idle'
    $rebind = Write-Request 'rebind-request' 'c0000000-0000-4000-8000-000000000003' $rebindKey
    $rebindReceipt = Invoke-Listener 'delivery-rebind' 0 @('delivery-rebind', $before, $after, $rebind) 'idle'
    $rebindAgain = Invoke-Listener 'delivery-rebind-repeat' 0 @('delivery-rebind', $before, $after, $rebind) 'idle'
    if ($rebindAgain -cne $rebindReceipt) { throw 'Repeated rebind did not return the stored receipt.' }
    $null = Invoke-Listener 'old-route-refused' 1 @('preparation-key', $before) 'idle'
    $movedKey = Invoke-Listener 'moved-preparation-key' 0 @('preparation-key', $after) 'idle'

    # Uncertain fixture: the single replay itself lost its acknowledgement; a fresh request stays refused.
    $lost = New-Config 'uncertain' 'cccccccc-cccc-4ccc-8ccc-cccccccccccc' 'uncertain'
    $null = Invoke-Listener 'uncertain-first-loss' 3 @('once', $lost) 'lose'
    $lostKey = Invoke-Listener 'uncertain-delivery-key' 0 @('delivery-key', $lost) 'lose'
    $lostRequest = Write-Request 'uncertain-replay-request' 'c0000000-0000-4000-8000-000000000004' $lostKey -Replay
    $null = Invoke-Listener 'uncertain-replay-loss' 1 @('delivery-replay', $lost, $lostRequest) 'lose'
    $lostReceipt = Invoke-Listener 'uncertain-replay-repeat' 0 @('delivery-replay', $lost, $lostRequest) 'accept'
    $secondKey = Invoke-Listener 'uncertain-second-key' 0 @('delivery-key', $lost) 'accept'
    $second = Write-Request 'uncertain-second-request' 'c0000000-0000-4000-8000-000000000005' $secondKey -Replay
    $null = Invoke-Listener 'uncertain-second-refused' 1 @('delivery-replay', $lost, $second) 'accept'
} finally {
    if (-not $server.HasExited) { $server.Kill($true); $server.WaitForExit() }
}

$null = New-Item -Path $Output -ItemType Directory -Force
$sends = @(Get-Content -LiteralPath $sendLog)
$oracle = [ordered]@{ listenerCommit = $ExpectedCommit; generatedUtc = [DateTimeOffset]::UtcNow.ToString('O'); fixtures = [ordered]@{} }
foreach ($state in @('custody', 'uncertain')) {
    $target = Join-Path $work "snapshot-$state.db"
    Copy-Item -LiteralPath (Join-Path $work "$state/checkpoint.db") -Destination $target -Force
    foreach ($suffix in @('-wal', '-shm')) {
        $side = Join-Path $work "$state/checkpoint.db$suffix"
        if (Test-Path -LiteralPath $side) { Copy-Item -LiteralPath $side -Destination "$target$suffix" -Force }
    }
    $dump = Join-Path $Output "$state.rows.json"
    & $dotnet $harnessDll snapshot $target $dump
    if ($LASTEXITCODE -ne 0) { throw "Snapshot failed: $state" }
    foreach ($suffix in @('-wal', '-shm')) { if (Test-Path -LiteralPath "$target$suffix") { throw "Write-ahead log was not merged: $state" } }
    $databaseHash = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant()
    # SQLite files are never source fixtures (.gitignore); the merged file stays in the private work directory.
    $oracle.fixtures[$state] = [ordered]@{ database = "$state.db"; sha256 = $databaseHash; rows = "$state.rows.json" }
}
$oracle.outputs = [ordered]@{ preparationKey = $rearmKey; preparationReceipt = $rearmReceipt; deliveryKey = $replayKey; deliveryReceipt = $replayReceipt
    rebindKey = $rebindKey; rebindReceipt = $rebindReceipt; movedPreparationKey = $movedKey; uncertainKey = $lostKey; uncertainReceipt = $lostReceipt; uncertainSecondKey = $secondKey }
$oracle.sends = $sends
$oracle.transcript = $transcript
$oracle.harness = @(Get-ChildItem -LiteralPath $PSScriptRoot -File | Sort-Object Name | ForEach-Object { [ordered]@{ file = $_.Name; sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() } })
$oracle.release = [ordered]@{ commit = $manifest.commit; listenerSha256 = $manifest.files['SharpClaw.Listener.dll']; protocolSha256 = $manifest.files['SharpClaw.Protocol.dll'] }
$text = ($oracle | ConvertTo-Json -Depth 10).Replace($work, '<work>')
Set-Content -LiteralPath (Join-Path $Output 'oracle.json') -Value $text
Write-Output "Fixture written to $Output from private work directory $work"
