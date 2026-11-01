# Current SharpClaw embedding boundary

The offline contract proof uses SharpClaw main
`8ca48622050721686a41c06a63d2735182ff3544` (plugin ABI 3) and locally packed,
unpublished Multisession `0.2.0-alpha.5` packages. The SDK has no SharpClaw
assembly reference. See [the source analysis](../specs/004-current-sharpclaw-contract/analysis.md)
and [measurement manifest](../specs/004-current-sharpclaw-contract/evidence/measurement.json).

The consumer references the actual SharpClaw project and the SDK through NuGet.
Its controlled offline `ICliProtocol` runs `/bin/echo` through the actual
`CliBackend`, `CliExecution` and native cleanup. It proves this transport and
session contract on Linux; it does not prove a live agent backend, all plugins,
Windows SharpClaw execution, or migration of existing host sessions.

## Ownership and observations

Run SharpClaw execution in a dedicated worker process. Its native cleanup scans
all children of its process; running it directly in a shared host can terminate
unrelated work. The isolated negative calibration terminated an owned sentinel;
the concurrent worker reference preserved the parent's sentinel.

Keep backend selection, model, workspace, session custody, reservations, fleet
and tool policy in the consuming host. Use the existing `ISessionProvider` boundary.
Persist the opaque native ID and continuation state, including the attempted flag.
Reject incompatible reopen bindings and never replace an existing native identity.

Await the SDK's durable operation observation before starting dependent external
work. SharpClaw's synchronous `Action<Observation>` is not an awaitable durable
acknowledgement. The reference therefore covers only a protocol with a native ID
allocated before execution. A late-ID transport needs a separately verified bridge.

Exit code zero alone is insufficient for success. Check the terminal signal and
the actual completion receipt; ambiguous termination remains `Unknown`. Killing a
worker does not by itself establish that every external operation has ended.
The reference advertises no inspect or native cancellation capability.

## Executed cases and reproduction

The external driver is retained at
`/home/user1/dev/e2e-harness-bundles/multisession-current-sharpclaw/driver`.
It is measurement infrastructure and does not run in product CI. Its source,
lock file, tested binaries, packages and result logs are hashed in the manifest.

Run with a fresh owned state directory and the manifest's exact source/packages:

```sh
bash /home/user1/dev/scripts/run-capped.sh -- dotnet run --project /home/user1/dev/e2e-harness-bundles/multisession-current-sharpclaw/driver/Probe.csproj -- --consumer /home/user1/dev/.agenttemp/sharpclaw-consumer-new
```

The consumer passed two concurrent first requests and two follow-ups after store
reopen, preserving distinct IDs and workspace/model/attempted bindings. It also
rejected a successful process without a terminal success event and preserved the
original ID with `Unknown` certainty after an identity mismatch. Peak concurrent
workers was two and the parent sentinel survived.

The `--custody` mode exercised actual `RunSessions.Prepare`: attempted sessions
without an ID and model/workspace changes were rejected without changing the
original custody file. The `--unsafe` calibration deliberately fails with exit 1
when the owned unrelated child is killed; this is the expected negative result.
Use that mode only inside its disposable measurement process.

Cancellation, arbitrary late-ID backends, live provider behavior and full host
composition remain open under the [concrete integration follow-up](../specs/004-current-sharpclaw-contract/follow-up.md).
