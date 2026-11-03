# Disposal after a timed-out stop can rethrow the earlier stop failure

Owner: Alexander Bleissem
Status: ready; observed once on Windows, the cause is located in source, no deterministic reproduction yet
Origin: push run 41699098083 at `4669501` (pull request #22). `quality (windows-latest)` failed one case,
`ContractTests.RuntimeCleanupTests.BoundsCleanupAndRetainsLeaseUntilStoreCloses`. The pull request run 41699106368 on the same
head passed the same test, as did the runs on `a24e548` and `8054481`. The runtime source is unchanged from `main` (`4b533c9`).

## Observed

The test's `finally` block calls `runtime.DisposeAsync()` after an intentionally timed-out `StopAsync`. It threw
`SessionOperationException: StorageUnavailable`:
- `RuntimeLifecycle.ThrowStopFailure` (`RuntimeLifecycle.cs:222`);
- called from `StopAsync` (`:111`, the locked check `_state == RuntimeState.Stopped`);
- called from `DisposeCoreAsync` (`:183`).

## Located (from source, not reproduced)

`DisposeCoreAsync` reads `_state` without the lock (`:181`) and calls `StopAsync` when the state is not yet `Stopped`. If the
cleanup of the earlier, timed-out stop sets `Stopped` (`CleanupCoreAsync`, under the lock) between those two points, `StopAsync`
takes the `Stopped` branch and rethrows the recorded stop failure. If cleanup finishes before `:181`, disposal returns normally.
The same history can therefore end with or without an exception from `DisposeAsync`, depending on timing.

A temporary local probe (Linux; disposal started while cleanup was held, and after it finished; neither committed) did not hit
the window. Both orderings returned normally.

## Next steps

1. Decide the intended contract: should disposal after an already reported stop failure rethrow it, or complete quietly once
   cleanup has finished? The current behaviour is timing-dependent either way.
2. Add a deterministic red-first test with a controllable point between the state read and the stop call, and make the smallest
   change (for example, deciding under the lock in `DisposeCoreAsync`). Keep the stop deadline and lease-retention guarantees
   unchanged.
3. Run the full native gates. Do not skip or loosen `RuntimeCleanupTests`.

## Acceptance

The decided disposal outcome is deterministic and pinned by a red-first test. `RuntimeCleanupTests` passes unchanged in meaning
on Linux and Windows.
