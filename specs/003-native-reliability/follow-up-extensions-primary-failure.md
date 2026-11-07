# Follow-up: extensions scenario primary failure (T016, finding PR40-WIN-EXTENSIONS-STORAGEUNAVAILABLE-20261104)

Created: 2026-11-06. This record serves T016. It cross-references T020 without assuming a shared cause.

## Observed failure (unchanged; root decisions T016-D1 and T016-D2)

- **Run:** pull request #40 push run 41898475710, job `quality (windows-latest)`, step "Publish and validate native host
  outside the source tree".
- **Head:** `5c31809`. Its delta to `main` `fc3891f` is four documentation and evidence files, so the failing code is
  identical to `main`.
- **Result:** scenario `extensions` ended FAIL with `StorageUnavailable` (code 7), certainty Unknown, no session, request or
  details, an empty `CleanupFailures` list, after 15.23 s. Passing runs of the same scenario take 0.33–0.95 s.
- **Not a fix:** a later pass of the same head.
- **Root cause:** unknown.
- **Linux T020 (separate):** the `main` push run 41876640953 failed on Linux in `RuntimeCleanupTests:29`. That is T020
  evidence and is separate from this follow-up. Raw stage values, corrected by root decision T016-D4:
  - dedicated oversleep maximum 0.7 ms;
  - last wake age 4.3 ms;
  - pool dispatch maximum 669.4 ms;
  - 66 pool items skipped.

  No zero-oversleep conclusion and no shared cause are drawn.

## Source analysis before implementation (source `fc3891faeac95fb5bff18d0a32ffa040fa9f2f5c`)

- **`ExtensionsProbeFlow.ExecuteAsync`** (`examples/Monstertacle.Multisession.ValidationHost/ExtensionsProbeFlow.cs`):
  - It holds its host with `await using ProbeHost host`.
  - The body waits up to 5 s for overlap (`WaitAsync(TimeSpan.FromSeconds(5))`) and calls
    `host.Runtime.StopAsync(TimeSpan.FromSeconds(10))`.
  - If the body throws and `ProbeHost.DisposeAsync` (that is, `SessionRuntime.DisposeAsync`) also throws, C# replaces the
    body's exception with the disposal exception. The primary failure is lost and the disposal failure is reported as the
    primary.
  - A failure of disposal alone, after a successful body, is also reported as the primary failure, not as a cleanup failure.
- **`ScenarioRunner.ExecuteOwnedAsync`:** keeps a primary plus the state-lease cleanup failure, but cannot see host disposal
  hidden inside the flow.
- **Existing owner pattern:**
  - `WaveProbeFlow.ExecuteOwnedAsync` already keeps the primary failure and adds the host disposal failure
    (`ValidationProbeException.FromException(primary).WithCleanup(cleanup)`).
  - It reports disposal alone as `ScenarioCleanupFailed`. `ProbeHost.StartPreparedAsync` does the same for startup.
  - `ScenarioOwnershipTests.PreservesWaveFailureAcrossBothCleanupBoundaries` tests the Wave owner.
  - The test class states the design: actual per-scenario owners, not a generic resource runner.
- **Runtime source of the code** (`RuntimeLifecycle`): stop and dispose emit `StorageUnavailable` with certainty Unknown at
  five sites:
  - stop-gate entry (`StopOnceAsync`);
  - stop acceptance or queued cancel, worker completion, and cleanup (all three in `CompleteStopAsync`, one `failure`
    variable);
  - worker fault (`WorkAsync`, observed through completion).

  The inner exceptions are discarded. `FailureDetails(FailureStage)` is public, and its closed `FailureStage` enum has no
  stop or dispose stage.
- **Typed failures:**
  - `ValidationFailure.FromException` keeps only the public error and details.
  - `ScenarioFailureAttributionTests.TypedFailureKeepsItsPublicErrorOnly` pins that a typed failure adds no throw site.
  - Untyped failures name their exception type and throw site.
- **Other flows:** ten other flows also hold their host with `await using`: capacity, cancellation, isolation, limits,
  native-context, recovery, retention, shutdown, soak and workspace. They have the same masking shape. They are not part of
  this observed failure.

### Readings of the observed report that the current source cannot separate

- **A.** The 5 s overlap wait timed out (`TimeoutException`), then disposal failed with `StorageUnavailable` and replaced it.
- **B.** A typed `StorageUnavailable` came from the body, for example the explicit 10 s stop.
- **C.** The body passed, and disposal alone failed.

All three produce the same report today.

## Decision (smallest consistent change; no product, public API, bound or pipeline change)

- `ExtensionsProbeFlow` uses the failure branch of the existing owner rule of `WaveProbeFlow`:
  - `ExecuteAsync` starts the host and calls `ExecuteOwnedAsync`;
  - `ExecuteOwnedAsync` runs the workload; on a workload failure it disposes the host, keeps the primary failure and adds a
    disposal failure as cleanup.
- **No Wave-style `ScenarioCleanupFailed` relabelling on success.** The workload ends with its own explicit
  `StopAsync`, which already runs runtime cleanup. Disposal then finds a stopped runtime and returns, so reading C cannot
  occur for this flow, and a relabelling branch would be dead, untestable code.
- **The observed report** is therefore reading A or B, not C. After the change, A reports `ScenarioExecutionFailed`, with
  `TimeoutException`, a throw site, and a `StorageUnavailable` cleanup failure. B reports its typed primary failure, plus a
  cleanup failure if disposal also fails.
- Bounds stay: 5 s overlap, 10 s stop. There is no retry and no swallowed exception.

### Not changed, with reasons (open for root decision)

- **The other ten `await using` flows:** same shape, no observed failure bound to them. Changing them widens the diff
  without evidence; root decides.
- **A throw site for typed failures:** it would name the runtime method (`RuntimeLifecycle.StopOnceAsync` or
  `CompleteStopAsync`). It contradicts the pinned contract `TypedFailureKeepsItsPublicErrorOnly`, so it needs a contract
  decision.
- **Runtime stop stages:** separating acceptance, cancel, completion and cleanup inside `CompleteStopAsync`, or keeping the
  inner exception, needs a public `FailureStage` extension or a new diagnostic channel. That is an API decision. It overlaps
  T020 (stop latency).

## Test plan (TDD; observed domain Red before Green)

1. A behavior-preserving extraction: `ExtensionsProbeFlow.ExecuteOwnedAsync(host, …)` with the current `await using`
   semantics.
2. **Red:** a unit test with an in-memory store whose disposal fails and a body failure. The report must keep the body's
   primary failure and list `StorageUnavailable` as cleanup. Under the extraction it observes the disposal error as the
   primary and no cleanup.
3. **Green:** the owner rule. Control: a body failure with successful disposal keeps only the primary (no invented
   cleanup).
4. **Negative control:** the real `extensions` scenario passes with no failure and no cleanup (existing
   `ValidationScenarioTests` and `ValidationHostContractTests` cases).
5. The full codemetr guard from the root.

## Qualification (private clone, 2026-11-06; serial, capped 2G/1G, private temp)

| Step | Result |
|---|---|
| Red, on the behavior-preserving extraction (commit `250eb92`) | `KeepsWorkloadFailureWhenHostDisposalAlsoFails` failed: expected NotFound, actual StorageUnavailable, with no cleanup. That is the observed report shape. The control passed |
| Green | both tests pass |
| Unit tests | 463/463 pass |
| Real `extensions` scenario (negative control, net10.0) | `ValidationScenarioTests` and `NativeScenarioOwnershipTests` pass |
| `net10.0-windows` integration target on Linux | fails with `UnsupportedPlatform` before the flow, identically on the unchanged base `fc3891f`. That is the environment, not this change |
| Full codemetr guard | 621 files, 0 violations |

The raw Linux T020 line (job 126737700342), kept verbatim: `dedicated-n=495 dedicated-oversleep-max-ms=0.7
dedicated-last-wake-age-ms=4.3 pool-n=429 pool-dispatch-max-ms=669.4 pool-unresolved-for-window=false pool-skipped=66`. It is
T020 evidence only.

## Limitations

- **The runtime cause stays unknown.** A recurrence with this report separates A from B, but not:
  - which body call threw a typed failure under B;
  - which `CompleteStopAsync` sub-stage failed;
  - whether a deadline or a fault occurred.
- **No stage timing is recorded.**
- **The ten other `await using` flows keep the masking shape.**

All of these are open root decisions (see "Not changed").
