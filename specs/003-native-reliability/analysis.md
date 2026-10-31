# Current architecture evidence before implementation
Created: 2026-10-30T06:20:36Z

Historical preliminary candidate: the PublicError/codec and generic runner
proposals below were rejected after the opposed reviews. The current design is
authoritatively recorded in architecture-decision.md and data-model.md; do not
implement the superseded proposals from this analysis.
Owner: Alexander Bleissem
Base: e6a9c6a1c5a866c377c04c1014387efec50a39c8
SourceHash: 1c90c394e617eb4e9f23858735e2fb25bf8266dbb9056cd868939aa8d53c3213

## Observed failure
CI 40987926123, Windows: routing failed WaveAdmission_AdmissionTimedOut; deterministic failed StorageUnavailable. Later same-source runs passed. The logs have no underlying SQLite code or execution phase, so they do not establish antivirus, permissions, hardware latency or a CQRS defect.

## Deep dive
SqliteWorkQueue owns one connection, but executes synchronous SQLite operations inside Task.Run and an async Channel reader. Its scheduling shares the host ThreadPool. SqliteWork<T> owns waiting/running/finished CAS and cancellation registrations; completed work uses asynchronous continuations. Queue capacity and lease state are explicit. The product has no dedicated executor.
SqliteTransactions.Run preserves no-write after verified rollback and Unknown once committing, but StorageFailurePolicy.Normalize removes raw error details entirely. SqliteStoreFactory also collapses native open/preparation failures. PublicError is a safe positional record without a structured diagnostic body.
RequestAdmission starts its monotonic deadline before lookup, creates the candidate ID only before commit and rebuilds PublicError in catch clauses. Even if the store supplied safe details, this reconstruction would discard them.
ScenarioRunner and WaveProbeFlow flatten errors further. await-using cleanup can replace a primary probe error. Existing AcceptanceFailureTests use real triggers/deferred constraints and assert certainty and sanitization, providing reusable regressions.
Relevant callers: SessionRuntime CQRS handlers -> original guarded RuntimeRequests -> RequestAdmission -> SQLite queue/transactions; ProbeHost/ValidationScenarioTests -> scenario executable. No provider policy or schema change is needed.

## Hypotheses and decision gate
A shared managed worker occupied by synchronous host work may delay the idle SQLite executor. First reproduce this in an outside-repository isolated process on the unmodified library; no implementation until observed. This proves a schedulability defect if reproduced, but does not by itself prove the historical CI cause.
If reproduced, replace the shared-pool Channel executor with a bounded BlockingCollection and one owned background Thread; no sync-over-async, no queue-limit or deadline change. Caller continuations remain asynchronous. Dispose closes admission, drains/rejects queued items and disposes the connection on its owner thread before completion.
Measured scheduling defect: the outside-repository scheduler-probe returned IndependentStoreRead=false in its 500ms window on the unchanged base. It constrained only its isolated process to one worker, occupied that worker, and issued GetSessionAsync against an already prepared idle store from a separate driver thread. The store read remained pending; unblocking restored cleanup. The product diff was empty. This is the implementation decision's deterministic evidence; it is not a measured historical Windows root cause.
Implementation refinement: Task.Factory.StartNew with LongRunning and TaskScheduler.Default owns the synchronous BlockingCollection loop and its completion, rather than manually duplicating task completion/fault handling around Thread. There is no async delegate or Task wait in the worker. Disposal owns collection completion and prevents TryAdd racing CompleteAdding via a small gate.
Add optional safe FailureDetails to PublicError (record body, existing constructor preserved). SQLite adapts known exceptions to numeric codes. Admission retains metadata and adds its own phase only when no lower-level phase exists.
Persistence inspection found StoreSerialization uses UnmappedMemberHandling.Disallow. Writing a new Details member to existing persisted error JSON would therefore break older readers despite unchanged SQL tables. Diagnostics are operational, not durable state: remove only PublicError.Details from the store-specific JSON type contract, while public/default JSON and scenario reports retain it. Test MarkUncertainAsync with details, exact persisted JSON absence and restart compatibility before implementing this codec rule. Do not change schema/version or weaken unknown-member validation.
For each behavior write a real failing test before the minimal fix, archive source and measured UTC/digests. Existing transaction, cancellation, capacity and lifecycle tests form the regression baseline. Static/Cover pins and thresholds remain unchanged.

## SharpClaw source read
Clean main updated by fast-forward to 8ca48622050721686a41c06a63d2735182ff3544. Compared with 7854a12: IAgentBackend, ICliProtocol, Session, RunContext, Observation unchanged. RunSessions.Prepare/SessionSelection now separate selection and durable identity custody; RunReservation rechecks admission before committing. AdmissionDecision/AdmissionFaults expose closed phase, cause and scope without exception text.
A new generic backend seam is unnecessary; existing ISessionProvider remains appropriate. Current-host package/adapter proof is a separate feature after the reliability work. No SharpClaw source or running instance is modified.
