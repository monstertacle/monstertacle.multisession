# Windows default-budget admission: source analysis and private controls (2026-11-04)

Part of `follow-up-windows-admission.md`. This record covers the source architecture, the existing CI evidence, private offline
controls and a measurement proposal for root decision. It changes no product, test, bound or pipeline code, and it claims no
cause.

## 1. Source state

`main` `18cbcbba85bd06e2db0688a6c0ca156cd24af123` (tree `46676dac`). The analyzed files, with the first 16 hex digits of their
SHA-256 at that head:

| File | SHA-256 (16) | Symbols |
|---|---|---|
| `src/Monstertacle.Multisession.Common/Requests/RequestAdmission.cs` | `03b3a8e00fb9efbc` | `SubmitAsync` |
| `src/Monstertacle.Multisession.Common/Contracts/RuntimeDefaults.cs` | `7aa512c19ea34eb9` | `AdmissionTimeout` (5 s), `DbBusyTimeout` (1 s), `MaxQueuedRequests` (100) |
| `src/Monstertacle.Multisession.Storage.Sqlite/SqliteWorkQueue.cs` | `c6634513632aeab4` | `ExecuteAsync`, the worker loop |
| `src/Monstertacle.Multisession.Storage.Sqlite/SqliteWork.cs` | `a9d6bb5e8d86c4f5` | `Run`, `CancelWaiting`, `Reject` |
| `src/Monstertacle.Multisession.Storage.Sqlite/SqliteTransactions.cs` | `6d5d26a1ce9b9cdd` | `Run` (begin, action, commit) |
| `src/Monstertacle.Multisession.Storage.Sqlite/AcceptanceTransactions.cs` | `c1227aeeeada3661` | `CommitAsync`, `Commit`, `Insert` |
| `src/Monstertacle.Multisession.Storage.Sqlite/SqliteConnectionPolicy.cs` | `18a9b61513932c76` | `Open`, `Configure` |
| `src/Monstertacle.Multisession.Storage.Sqlite/StorageFailurePolicy.cs` | `fba5abc7eea37461` | `Error`, `Normalize` |
| `src/Monstertacle.Multisession.Storage.Sqlite/QueryTransactions.cs` | `2551d62fa16c76c9` | `Session` |
| `tests/Monstertacle.Multisession.IntegrationTests/RuntimeLifecycleTests.cs` | `4aaf497dfb6f27ea` | `StopsAcceptanceAndFinalizesAllQueuedPages` |
| `tests/.../Fixtures/AdmissionStoreFixture.cs` | `5538031b5facc80c` | `CreateAsync` |
| `tests/.../Fixtures/PrivateStoreDirectory.cs` | `548d3bacc797c9e7` | the test lease |

Native SQLite comes from `SQLitePCLRaw.bundle_e_sqlite3` 2.1.13 through `Microsoft.Data.Sqlite` 10.0.14.

## 2. Architecture of one default-budget submit

**Budget clock.** `SubmitAsync` starts one `CancellationTokenSource(AdmissionTimeout, TimeProvider)`. That is 5 s by default,
covering the whole call, and it is linked with the caller's token (`RequestAdmission.cs:26-27`). The budget is per call; the
102-submit loop as a whole has none.

**Stage 7, `AdmissionLookup`.**
- `GetSessionAsync(...).AsTask().WaitAsync(token)` is awaited (line 35).
- The store runs it as one work item on the queue.

**Stage 8, `AdmissionCommit`.**
- `committing = true` is set, then `CommitAcceptanceAsync` runs (lines 37-42).
- In the store this is one admission work item (`AcceptanceTransactions.cs:10`) holding one SQLite transaction. The transaction
  validates the session again, checks the duplicate and the queue capacity, then inserts the request and updates the session
  (`AcceptanceTransactions.cs:12-50`).
- If the store task completed synchronously it is returned at once (line 40). Otherwise the caller awaits it through
  `WaitAsync(token)`.

**Store owner.**
- `SqliteWorkQueue` has one bounded `BlockingCollection` (capacity `DbWorkQueueCapacity`, 256) and one dedicated thread
  (`TaskCreationOptions.LongRunning`) that owns the connection (`SqliteWorkQueue.cs:15-36`).
- Every store operation of this runtime goes through it, in FIFO order.
- Before each item, the worker reads `lease.IsHeld`. In this test that is a plain field of the test lease
  (`PrivateStoreDirectory.cs:14`), so the native lease code is not on this path.

**Work item states** (`SqliteWork.cs`): waiting, running, finished.
- A cancellation while *waiting* completes the item with `AdmissionCancellationException` (`NotAccepted`, stage `StoreQueue`).
- A cancellation while *running* has no effect until the synchronous SQLite call returns. Line 57 checks the token only before
  the action. `SqliteTransactions.Run` checks it before begin and before commit, never during a statement or the commit itself.
- Completion uses `RunContinuationsAsynchronously`. The caller therefore resumes on a thread-pool thread, never on the store
  worker.

**Durability** (`SqliteConnectionPolicy.cs:27`):
- `journal_mode=DELETE`, `synchronous=EXTRA` and `busy_timeout=1000` are required. Every acceptance therefore creates, flushes
  and deletes a rollback journal file next to the database. On Windows the test's database is under `%USERPROFILE%`
  (`PrivateStoreDirectory.cs:12`).
- Pooling is off. One connection per store.

**Error mapping** (`RequestAdmission.cs:44-64`):

| What the caller sees | Code | Certainty / status |
|---|---|---|
| The deadline token fired when an `OperationCanceledException` arrived | `AdmissionTimedOut` | stage `AdmissionLookup` + `NotAccepted` before `committing`; `AdmissionCommit` + `Unknown` after it |
| The pending commit had completed successfully by the time of the catch | none: the receipt is returned (line 46) | accepted |
| The store raised a `SessionOperationException` (busy, I/O, rules) | its own code, for example `StorageUnavailable` with native code 5 | from the store |

`NotAccepted` after `committing` is kept only if the exception is the store's own `AdmissionCancellationException` (line 47).

**Lookup by the caller.**
- An `Unknown` candidate is resolved with `GetRequestAsync(requestId)`, never by resubmitting.
- `NotAccepted` means no write.

**Linux and Windows on this path.**
- The managed code is identical.
- They differ in the native SQLite VFS (unix versus win32 file locking, flush and delete), in the file system and its filter
  drivers (for example on-access scanning), and in the thread-pool and timer implementation under host load.
- The production native leases (`WindowsStateDirectoryLease.IsHeld` with its ownership check, the Linux lease) are used by the
  runtime and the platform tests, not by this integration test.

## 3. Observations from existing CI evidence (no run repeated)

Failed push run 41769537053 (`9a76a2f`, Windows), against the passing pull_request run 41769544991 on the same head. Both TRX
files come from the retained `quality-windows` artifacts.

**The failure itself**
- The failed test took 8.73 s; on the passing run the same test took 4.08 s, including a 116 ms stop.
- The stack ends at `RequestAdmission.cs:54`, the deadline branch. That line serves both stages.
- The test prints only the exception message. The exception itself carried the stage (`FailureDetails`) and the admission status
  and request id (`PublicError`), but they were not printed. The loop index existed only in the test's loop variable and was not
  captured at all. It cannot be recovered.
- From 8.73 s minus the 5 s budget, the earlier submits and cleanup took at most about 3.7 s.

**What ran around it**
- The assembly disables test parallelization (`AssemblyTestPolicy.cs`), so no other test of the assembly overlapped.
- The neighbouring tests ran close to normal speed: the run-to-run duration ratio is 0.96-1.10 for the four tests just before
  (03:53:23) and 0.92-1.54 for the seven tests just after (03:53:33-34).
- The failed run had two more slow windows: 03:53:01-03:53:08 (five tests at 3.7-17x) and 03:55:48-03:56:11 (twelve tests at
  3-6x). The passing run had no test at 3x or more of the failed run.
- Across 401 tests over 50 ms, the duration ratio is p50 1.02 and p90 1.77.

**Earlier records** (unchanged, see the other evidence files):
- stage 8 `Unknown` (41054347449);
- stage 7 `NotAccepted` (41683919153);
- the restart worker's slow block (41533898656);
- the Windows envelope: median 3 to 20 times Linux, with heavy tails.

## 4. Private offline controls (Linux, outside the repository)

**Setup**
- The harness is `e2e-harness-bundles/multisession-windows-default-admission-20261104`. Its README and SHA256SUMS bind every
  input and log.
- It runs on an export of `18cbcbba` plus a private 21-line stage-mark patch. The patch is never retained.
- It replays the failing setup exactly: two sessions, then 102 sequential submits with the default 5 s budget, the default
  durability settings and `MaxQueuedRequests` 130.
- No provider, network or Python is used, and each run gets fresh private state.
- One finite condition is injected at submit 50. The candidate is then looked up read-only.
- Three consistent runs of all five controls, plus one run with a 50 ms thread-pool canary:

| Control | Injected condition | Caller saw | Later lookup | Canary max lag |
|---|---|---|---|---|
| C1 | lookup store work slow (5.5 s on the worker) | `AdmissionTimedOut`, `AdmissionLookup`, `NotAccepted`, 5.0 s | absent | 3 ms |
| C2 | commit waits in the queue behind a 5.5 s store operation | `AdmissionTimedOut`, `AdmissionCommit`, `Unknown`, 5.0 s | **absent** | 6 ms |
| C3 | durable commit done (SQLite commit 2.4 ms), then the worker is slow | `AdmissionTimedOut`, `AdmissionCommit`, `Unknown`, 5.0 s | **Queued** | 7 ms |
| C4 | a second connection holds `BEGIN EXCLUSIVE` for 6 s | `StorageUnavailable`, `StoreExecution`, native 5 (busy), `NotAccepted`, 1.0 s | absent | 3 ms |
| C5 | every thread-pool worker blocked for 6 s; the store worker stays free | `AdmissionTimedOut`, `AdmissionLookup`, `NotAccepted`, 6.0 s; the lookup work ended after 1.6 ms | absent | **5983 ms** |

**Envelope without injection:** 20 iterations, 2040 submits, 0 failures. Limits:
- These timings were taken with the private marks in place. They are not a production or Windows cost.
- The "queue wait" rows start at the `ExecuteAsync` offer mark, taken before `TryAdd`. They measure offer to run start, not proof
  of a successful enqueue.

| Interval | p50 | p99 | max |
|---|---|---|---|
| Submit | 2.53 ms | 6.12 ms | 40.85 ms (the first submit) |
| Lookup queue wait | 0.01 ms | 0.04 ms | 2.62 ms |
| Lookup run | 0.07 ms | 0.20 ms | 1.77 ms |
| Lookup resume | 0.01 ms | 0.07 ms | 3.98 ms |
| Commit queue wait | 0.00 ms | 0.02 ms | 1.13 ms |
| Commit run | 2.39 ms | 5.51 ms | 34.07 ms |
| SQLite commit | 2.18 ms | 5.14 ms | 10.44 ms |
| Commit resume | 0.03 ms | 0.10 ms | 1.59 ms |

**What the controls establish**
1. **Stage 7 versus stage 8:** one bit only. A store-side stall (C1) and a process-wide scheduling stall (C5) produce the same
   public error. Without a scheduling measurement they cannot be told apart.
2. **Stage 8 `Unknown` covers two opposite states:** the request was never written (C2) or durably accepted (C3). Only the later
   lookup separates them. This agrees with the existing no-replay rule.
3. **A store lock wait** in this Linux control fails with `StorageUnavailable` and native code 5 after about 1 s (C4), not with
   `AdmissionTimedOut`. In this control, a single busy wait on a held lock did not produce the observed error.
   - Limit: Linux only. The win32 VFS retries some sharing violations internally, and that is not covered here.
4. **A fast store can still time out:** a store that finishes in under 2 ms is reported as `AdmissionTimedOut` when the thread
   pool cannot resume the caller (C5). So the error does not show that the store was slow.
5. **In these controls, the canary separated scheduling from store stalls:** a 50 ms thread-pool tick showed 3-7 ms of lag in C1
   to C4 and 5983 ms in C5.
   - Limit: the private canary only sampled. It read its maximum before it was stopped, had no readiness or sample count, and
     could miss the final interval.
   - A low, zero or missing lag is therefore not proof that no scheduling stall happened. Only a high lag is informative, and
     it shows delayed scheduling, not which of the thread pool, the GC, the host or native code caused it.

**Finding ADM-CERTAINTY-QUEUE-20261104 (source, not a cause)**
- In C2 the commit work never ran, as the later absent lookup and the controlled order show. By source,
  `SqliteWork.CancelWaiting` completes only a still-waiting item, with `NotAccepted` and `StoreQueue`. That transition was not
  logged directly. The caller nevertheless receives `Unknown` and stage `AdmissionCommit`.
- Inferred mechanism: the `WaitAsync(token)` cancellation reaches the catch first, and line 47 trusts only the store's own
  exception. The order of the token callbacks was not measured.
- This is conservative and safe, because `Unknown` requires a lookup and no replay. It does lose certainty and the queue stage.
- Root decided to keep this behaviour (see `follow-up-windows-admission.md`). Nothing is changed.

## 5. Inference, kept separate

The 41769537053 failure is compatible with three explanations that the existing evidence cannot tell apart:
- a store-side stall (C1/C3-like), for example slow journal create, flush or delete on the runner's disk or under a file filter;
- a commit behind such a stall (C2-like);
- a process-wide scheduling stall (C5-like).

**Leaning, not established.** The run's other slow windows and the earlier "every step slowed together" record lean towards
host or process-wide episodes. That fits the existing inference and is still not a measurement.

**Not supported for this path:**
- a self-inflicted queue backlog: the test is sequential and nothing else uses the store during setup;
- a parallel test: parallelization is disabled;
- a plain lock wait, as in the Linux control C4: that gave `StorageUnavailable` with native code 5. Windows VFS sharing retries
  are not covered by it.

## 6. Open questions

1. Which stage (7 or 8) and which loop index failed in 41769537053, and was the candidate written? This is not recoverable: the
   store was deleted and the log has no details.
2. During a failing submit, was the store worker inside SQLite, waiting in the queue, or finished while the caller was not
   resumed?
3. Did the thread pool or the whole host stall at that moment?
4. Does the win32 VFS sharing-violation retry or on-access scanning of the journal file add multi-second delays on hosted
   runners?
5. Does the default 5 s budget need a documented operating-envelope statement for Windows hosts under load? This stays open in
   this follow-up and in feature001 T099.

## 7. Measurement proposal for root decision (nothing implemented)

The options are ordered by intrusiveness. Each keeps every bound and assertion, and none adds a CI retry or a failure hunt.

**M1, test-only failure attribution** (answers question 1 for future failures)
- Default-budget setup loops in IntegrationTests write a separate test diagnostic on failure, then rethrow the original exception
  unchanged. The diagnostic carries the loop index, stage, admission status and the last 10 submit durations.
  - First user: `RuntimeLifecycleTests.cs:22-27`.
  - Related: `SubmitAsync` is called in 16 IntegrationTests files. Which of them are default-budget setup loops is listed as
    part of M1, not assumed here.
- After a stage-8 `Unknown`, the rethrow also carries the read-only lookup outcome of the candidate.
- No product change. The observer cost was not measured.

**M2, test-only scheduling canary** (answers question 3)
- The same test-only scope as M1, plus a 50 ms thread-pool tick that records its maximum lateness.
- The value is reported only on failure, together with `ThreadPool.ThreadCount`, `ThreadPool.PendingWorkItemCount` and
  `GC.GetTotalPauseDuration()`. The GC value is cumulative over the process lifetime, so it is reported as a delta from the
  setup baseline.
- In C1 and C5, the canary separated the two stalls the public error cannot. A low lag proves nothing (item 5 above). It needs
  no runtime access.

**M3, internal store stage stamps** (answers question 2; needs an architecture decision because it changes product source)
- Timestamps for enqueue, run start, SQLite commit start and end, run end, and deadline fired, kept in the existing
  `FailureDetails` or an internal diagnostic that tests can read (`InternalsVisibleTo`).
- No public API change.
- The private 21-line prototype shows the marks and how the stall is placed. Its cost was not measured: the timings were taken
  with the marks in place.

**M4, journal-file I/O attribution on Windows** (question 4)
- Only after M1 to M3 point at store I/O.
- Native counters such as `GetFileInformationByHandleEx` timings or ETW are out of this grant and need their own decision.

**Recommendation:** M1 and M2 together, as one bounded test-only change. A naturally failing native run would then show the stage,
the index, the candidate outcome and the sampled scheduling lag. A normal canary alone does not authorize M3; root assesses the
actual failure evidence first. None of this is started without root's decision.

Root decision 2026-11-04: M1 and M2 are granted as a test-only change in the first `RuntimeLifecycleTests` setup loop only, under
nine named conditions. M3 and M4 are not granted. See `follow-up-windows-admission.md`.
