# Test-only admission setup diagnostics (M1 and M2): architecture before implementation (2026-11-04)

Part of `follow-up-windows-admission.md`. Root granted it on 2026-11-04 under nine named conditions. Test-only: no production,
API, schema, pipeline, threshold or default-budget change.

## Source state

- Branch `test/003-admission-setup-diagnostics`, from the corrected documentation head `5e765f7`. That head is `main` `18cbcbba`
  plus documentation.
- Call site: `tests/Monstertacle.Multisession.IntegrationTests/RuntimeLifecycleTests.cs`, method
  `StopsAcceptanceAndFinalizesAllQueuedPages`, lines 22-27.
  - The loop makes 102 sequential `RequestAdmission.SubmitAsync` calls under the default 5 s budget, with `maxQueued: 130`.
  - The test's own timer (`began`) starts only after the loop, the store disposal and the runtime reopen.

## Existing code examined

- **Call site:** `RuntimeLifecycleTests`.
  - It already receives `ITestOutputHelper output`.
  - The loop variable is the only holder of the loop index.
  - The request id was `Guid.NewGuid()`, passed straight into `SubmitAsync` and not kept.
- **Store ownership:** `AdmissionStoreFixture`.
  - It owns `Store` and disposes it in `DisposeAsync` (`await using setup`).
  - During the loop the store is open, so a read-only `Store.GetRequestAsync(Guid, CancellationToken)` (`ISessionQueryStore`) is
    available.
  - The test disposes the store itself after the loop. An early failure leaves disposal to `await using setup`.
- **Exception content:** `SessionOperationException`.
  - `Failure` (`PublicError`) holds the code, certainty, request id and admission status.
  - `Details` (`FailureDetails`) holds the stage and native codes.
  - The loop index is not part of it.
- **Precedent:** `UnitTests/Fixtures/StageObservation.cs`, a test-only fixture linked into IntegrationTests.
  - It writes one line after a measured window.
  - A failing write is thrown together with the primary failure, primary first (`AggregateException`).
  - It is reused as a pattern, not as code. It reports stage stamps of a passing or failing window, whereas this case needs a
    failure-only report with a lookup and a sampler.
- **Concurrency rule:** `AssemblyTestPolicy` disables test parallelization, so a process-wide counter is not shared with another
  test of the assembly. The counters are still process-wide.

## Decision

Two new test fixtures in `IntegrationTests/Fixtures`, used only by this loop, with no change to any other caller.

**`AdmissionSetupObservation`**
- `Start` takes:
  - a read-only lookup delegate (the fixture's `Store.GetRequestAsync`);
  - an output delegate (`output.WriteLine`);
  - the GC pause baseline, read at start.
- It starts the scheduling sampler.
- `Record(submitted)` stores the duration of a passing submit in a ring of the last 10 numbers. Nothing is formatted on the
  passing path.
- `Capture(index, candidate, submitted, failure)` is synchronous and called first in the catch, before any await. It records:
  - the submit end and duration;
  - the thread-pool thread count and pending work items;
  - the GC pause delta since start;
  - the sampler snapshot.
- `ReportAsync(snapshot, failure)` then:
  - stops the sampler, with a bound;
  - runs one lookup of the original candidate id, with its own 2 s token and a 3 s outer bound;
  - writes one line.
  - A failing write is thrown as `AggregateException(primary, output)`. Otherwise it returns, and the call site rethrows the
    original exception with `throw;`, so the same instance, type, details, message and stack are kept.
- Lookup results are reported as observed, with the lookup elapsed time:
  - the request state and certainty;
  - `absent-at-observation`;
  - `timeout`;
  - `error:<type>`.
- A late lookup task is observed by a continuation, never joined.
- Absence is not reported as `NotAccepted`.

**`SchedulingSampler`**
- A loop of 50 ms `Task.Delay` ticks on the thread pool.
- It records the readiness stamp (first tick started), the completed sample count, the last-sample stamp and the maximum lag.
- The snapshot reports:
  - "not ready" or "no samples" explicitly, never as 0 lag;
  - the gap from the last sample, or from readiness, to the failure point.
- Stopping is a cancellation plus a bounded wait. A loop that does not stop, or that faults, is reported, never ignored.
- No thread-pool minimum or maximum is changed.

**Call-site shape**
- The observation's `await using` scope ends after the loop, before `await setup.Store.DisposeAsync()`. The sampler is therefore
  stopped before the test's stop timer starts.
- On an early failure, the observation's disposal (bounded) runs before `setup`'s, as nested scopes do.
- Every assertion, timer origin, budget, token, queue capacity and durability setting stays unchanged.

**Reading the result**
- A high lag shows delayed scheduling, not whether the thread pool, the GC, the host or native code caused it.
- A low or missing lag proves nothing.
- The GC value is cumulative for the process and is reported as a delta from the setup start.

## Test plan

**Unit tests of the fixtures** (deterministic, with injected delegates):
- One line with index, candidate, code, stage, status and request id, then the same exception instance.
- A write failure gives the primary first, then the output failure.
- A lookup that never completes is reported as `timeout`, within the bound.
- A lookup error and an absent result are reported as observed, with no `NotAccepted` claim.
- A sampler that never started, or has no samples, is reported as such, not as 0 lag.
- A delayed sample is reported as lag.
- The ring keeps only the last 10.
- The passing path writes nothing.

**Red:** the tests run against behaviourless signatures first.

**Call-site controls** (private shadow copies, never retained):
- the unchanged loop under an injected store stall, which writes no diagnostic (Red), then the new loop (Green);
- a store stall against a scheduling stall;
- a lookup stall;
- an output failure;
- an unchanged passing run, including cleanup.

**Mutations:** swallowing or wrapping the exception, an unbounded lookup, and reporting missing samples as 0.

**Gates:** local Codemetr, format and quality, then the exact-head native CI. No retries.
