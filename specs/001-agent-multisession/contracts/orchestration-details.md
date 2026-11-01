# Orchestration Contracts v1 — Binding Target Design

Original design 2026-10-28; implementation reconciliation 2026-10-29T21:57:58Z.
Concretizes FR-005/FR-007/FR-018–FR-021. T112–T115 are implemented following the documented
functional Red cases: ReservedDispatch with SQL fence, recovery targets/CAS,
admission certainty and all four bounded SQL keyset lists. The package state remains
pre-publication; this is not an executed migration or a new product release.

## 1. Reserved Dispatch Snapshot (U1)

TryReserveHeadAsync still returns ExecutionReservation. In the **same** transaction,
Running/InProgress, DispatchId, the occupied slots/keys and an immutable
ReservedDispatch are stored. The dispatcher reads it via the new domain operation
`ISessionQueryStore.GetReservedDispatchAsync(dispatchId, cancellationToken)`.

| ReservedDispatch field | Meaning |
| --- | --- |
| Reservation | Already stored ExecutionReservation |
| Session | SessionSnapshot frozen at reservation with ProviderId, Context and Binding |
| Submission | Stored SubmitRequest with exclusively new unchanged input and execution deadline |
| Sequence | Confirmed acceptance sequence |
| BindingRevision | Persisted revision of the confirmed continuation state at reservation |

Reservation.RequestId/SessionId, Submission.SessionId and Session.SessionId must
be consistent. Session.Binding is a valid adapter binding; its SessionReference
may be missing only in the case of a permissible lazy open. An unknown or already released
DispatchId, or a job that is no longer running or already marked for cancellation, returns
null; the dispatcher then does not send. A store error is not disguised as an empty queue.
The check applies at the time of reading; a cancel arriving afterwards is treated as cancellation of a
running dispatch, not by a feigned atomic network/DB transaction.

The operation is purely reading and starts neither provider work nor a second attempt.
The runtime worker processes each DispatchId it newly acquires at most once.
After restart, old reservations are never used again as send authorization.
A repeated read is not a retry and not a permission for a further send.
Input and binding MUST NOT be reconstructed from current host objects or from history replay.
MaxOutputBytes comes from the options fixed at runtime start;
ProviderCall is formed from this limit and the ReservedDispatch.

BindingRevision begins at 0 for the binding confirmed after Open or the explicitly lazy
binding. Every atomic publication of a new confirmed continuation state increases
it by 1. New native IDs/operation IDs from observations are stored dispatch-bound before callback confirmation;
they alone do not publish a new continuation head.
Completion checks the BindingRevision stored at dispatch against the current
session revision and exactly this still open dispatch. On deviation: RevisionConflict,
no result/history/binding success commit and no reservation release.
A rejected or failed commit MUST NOT lead to a renewed send.

## 2. Recovery Without Dummy Request and With Readable Revision (U2/U3)

`RecoveryTarget` consists of `Kind` and `Id`:

| Kind | Id | RequestId in the case snapshot |
| --- | --- | --- |
| SessionOpening | Local SessionId of the uncertain opening | null |
| RequestExecution | RequestId of the uncertain execution | Associated RequestId |

No sentinel UUID or artificial request replaces SessionOpening. RecoveryCaseSnapshot
contains Target, SessionId, optional RequestId/DispatchId, immutable CreatedUtc,
Revision, the open/resolved case status, block reason, certainty and available opaque
native references. It contains no credentials, prompts or complete inputs.

The new public read operation `IRecovery.GetRecoveryCaseAsync(target, token)` returns
the case snapshot or null. `ListRecoveryCasesAsync` returns `Page<RecoveryCaseSnapshot>`
for open cases. `InspectRecoveryAsync` likewise addresses the target, reads only the
known native reference and changes neither case, revision nor reservation. If an
inspectable reference or the capability is missing, it reports UnsupportedProviderAction/Unknown,
without native opening, send or invented end confirmation.

RecoveryDecision contains DecisionId, **Target instead of RequestId**, ExpectedRevision, Kind,
Explanation and EvidenceReference. The store uses an atomic ResolveRecoveryAsync
operation for both target kinds; the earlier request-bound ResolveReservationAsync
signature has been superseded since T113. In addition to the decision, the store receives an optional
confirmedBinding from the previously executed reopen. This does not belong to the public
operator decision: the service checks native identity/adapter/format, the store re-checks
ExpectedRevision and publishes resolution and confirmed binding together. An unknown/foreign target results in NotFound.

Revision is a positive persisted long number per target: the first case has revision 1,
every accepted state- or evidence-relevant case change increases it by exactly 1. Even after
resolution and renewed opening of the same target, it is not reset. Pure
reads/Inspect/renewed non-changing rejection do not increase it. ExpectedRevision must
match exactly at the atomic decision commit; only one competing commit with
the same revision can win. The others result in RevisionConflict without partial effect.
A duplicate DecisionId is rejected without renewed effect as RevisionConflict;
there is no automatic retry. GetRecoveryCaseAsync can still return the meanwhile
resolved case as long as the associated session has not been legitimately deleted.

### Uncertain Opening

Blocking and the SessionOpening case are stored together as soon as an uncertain
open becomes known. At restart, a leftover Opening session is also
conservatively recorded as such a case; it is not automatically reopened.
A failed storage commit is reported as an error, not as a saved lock.

CloseSession sets Closing and keeps the case as well as any existing reservations
open as long as there is no proven end. ConfirmEnded requires an explicit justified
investigation as with RequestExecution. With an existing close intent and without further
open/uncertain work, Closed may subsequently be reached atomically. Without a close intent,
Open/None is permissible only after a successful reopen of **the same known** binding.
If this is missing or reopen fails, the case remains open/blocked; no new
native identity is created. After a new read with the current revision, the integrator can
initiate the closing. Native resolution I/O takes place outside the transaction; before applying
the obtained evidence, ExpectedRevision is atomically checked again. RequestExecution keeps
its historical error/Interrupted result and never retroactively becomes Succeeded.

## 3. Admission Certainty and Lookup (U4)

`AdmissionStatus` distinguishes NotAccepted, Committed and Unknown **independently of**
ExecutionCertainty. A job can be committed locally but still be NotStarted externally.
PublicError receives an optional AdmissionStatus; for other operations it remains null.
SessionId and an already generated candidate RequestId may be included as safe local
lookup keys, not as confirmation of acceptance.

| Observation | Public statement |
| --- | --- |
| Local validation fails or the store confirms rollback/non-start | NotAccepted; no receipt |
| Store confirms a successful commit | Committed; return the actual RequestReceipt, even if the caller token/timer has meanwhile fired |
| Interrupted wait/store error without proof of commit or rollback | Unknown; claim neither a success receipt nor safe non-acceptance |

Before the commit operation begins, RequestAdmission generates exactly one candidate RequestId.
`RequestAcceptance(RequestId, Submission)` is used as input for CommitAcceptanceAsync;
this operation adopts the same ID and assigns Sequence/queue slot atomically.
Thus in T114 the previous store parameter changes, not the public Submit
signature. ID collisions are rejected, not merged with foreign requests.
Every new submit generates a new candidate identifier; no automatic deduplication,
no automatic retry of an unclear commit.

On caller cancel, the caller receives an `AdmissionCancellationException`, derived
from OperationCanceledException, with the sanitized PublicError including AdmissionStatus and
candidate identifier, insofar as already present. On deadline/storage errors,
SessionOperationException uses the same metadata with AdmissionTimedOut/StorageUnavailable.
ExecutionCertainty is determined separately from actually available execution knowledge;
an Unknown admission in particular MUST NOT imply a safe external NotStarted.
The host can use GetRequestAsync(candidate identifier) or ListRequestsAsync.
A record that is provisionally missing while the commit outcome is still unclear is **not** proof
of non-acceptance. There is no automatic renewed submit or later receipt push.

## 4. Bounded Lists and Cursors (A2)

For all session/request/history/recovery lists, `1 <= pageSize <= 100` applies explicitly;
it is neither silently capped nor corrected. The recommended host page size is 50,
not an implicit parameter default. Shared named constants have been in
Contracts/PageLimits.cs since T115. The cursor is null for the first page; an empty string is invalid.

| List | Ascending stable sort key |
| --- | --- |
| Sessions | CreatedUtc, SessionId |
| Requests of a session | Sequence, RequestId |
| History of a session | HistoryOrdinal, HistoryId |
| Open recovery cases | CreatedUtc, Target.Kind, Target.Id |

UUIDs are compared ordinally in the canonical D format, points in time as UTC; Target.Kind
has the fixed order SessionOpening before RequestExecution. HistoryOrdinal is a session-related
counter assigned monotonically on insertion; HistoryId a local UUID. Thus even
multiple local observations of the same request can be sorted unambiguously.

The cursor, at most 2048 UTF-8 bytes long, is a Base64url-encoded JSON structure with
Version=1, StoreId, list kind, SessionId for session-related lists and the last complete
sort key. Parsing is strict and culture-independent. Unknown version, invalid
encoding/key/size, a different list kind, a foreign store or a wrong session result in
InvalidInput without a partial result. A cursor is not proof of authorization and contains no
secrets/contents. The store/host access controls remain independently required.

Keyset continuation reads strictly after the last key, at most pageSize elements;
one additional internally read element decides on NextCursor. At the end, this is
null. Deleting the last read element does not invalidate the continuation. Multiple
pages are a **live view**, not a snapshot transaction: newly inserted elements after
the cursor can appear, those before it only on a new scan. States can change between
reads; the returned DTOs themselves are immutable snapshots. Thus neither
snapshot isolation nor complete findability through just one parallel scan is claimed.

## 5. Evidence-Bound Continuation Head (T116)

For `RequiresOperationAwareRecovery`, RecoveryService uses the optional port
`IExecutionRecoveryProvider` instead of the ordinary reopen. A provider-defined new
head must be verified from the known uncertain operation. Without the necessary
reference or a matching port, case and reservation remain locked. The existing
identity/adapter/version and CAS rules apply unchanged.

A successfully confirmed `SessionEstablished` may concretize the previously empty SessionReference
in RecoveryCase.Binding. This publishes neither a new confirmed
head nor the binding in the SessionSnapshot. The case preserves the known native identity
for explicit resolution; the service receives this known case binding, and the store checks
it at the joint decision/binding commit. No observed reference is replaced by
reopening. Productive persistence of this projection belongs to T060–T062.

## 6. Atomic Lock on Unusable Continuation (T117)

With `ContinuationAvailable=false` or a known ProviderSessionExpired category,
CommitCompletionAsync stores the block reason ProviderUnavailable together with Result,
History, confirmed binding and permissible reservation release. Null/true alone change
no existing policy. RecoveryRequired is not downgraded. An uncertain response
continues to go through the separate uncertainty commit and remains reserved. With a known
continuation blocker, the session accepts no further requests; already Queued ones remain
visible for explicit close/stop/resolution. No new native reference and no automatic
retry. Productive transactions must fulfill the same atomic boundary.

### Authoritative Snapshot on Confirmed Opening

CommitSessionBindingAsync returns the SessionSnapshot confirmed in the same transaction.
CreateSessionAsync returns it. SettingsRevision and CreatedUtc
remain unchanged; UpdatedUtc denotes the last stored session mutation.
ClosedUtc is null before actual Closed, even with Closing with unresolved work.
The chronological local UTC points in time do not replace monotonic execution deadlines.

### Local Configuration Lock Before Worker Start

CommitConfigurationAvailabilityAsync atomically applies exclusively a local availability
decision. SessionSnapshot.ConfigurationUnavailable marks its origin.
A configuration that is valid again may only lift a ProviderUnavailable lock marked in this way;
RecoveryRequired and unmarked native continuation restrictions remain in place.
Registry/binding/execution kind/workspace checks take place after bootstrap, before workers.
Opaque model/settings data remain unchanged; native auth/model availability is
not feigned by an invented local feature catalog.

## 5. Atomic Local Cancel/Close Facets

ISessionStore inherits the narrow facets IRequestCancellationStore and ISessionClosureStore.
For Running, RequestCancellationAsync stores only the first CancellationRequestedUtc;
Queued atomically becomes Cancelled/NotStarted including Result/History. Final results remain
unchanged. CancelQueuedSessionAsync processes the set of still waiting entries of a
local session in one commit with bounded Sequence keyset pages; an empty result
set is a successful empty bulk cancellation. This store port is not a public
close or a substitute for IRequests.CancelRequestAsync with its NotFound contract.

BeginSessionCloseAsync locks admission, finalizes Queued and stores the Running intent
together. RefreshSessionCloseAsync checks active/uncertain requests, unreleased
reservations and open cases. Closed requires the absence of all these obstacles;
ClosedUtc is set once. Opening gets its own case. A late binding
commit MUST NOT reopen Closing/Closed. The SessionClosure core observes the
progress in a bounded way; completion or recovery update Closing even without a
waiting caller. None of these transactions calls a provider or deletes its data.

## 6. Store and Runtime Options Before Startup

SessionRuntime awaits ValidateRuntimeOptionsAsync on IStoreBootstrap before StartAsync,
recovery and worker creation. The SQLite store compares the effective RuntimeOptions
with its immutable factory record validated at CreateNew/OpenExisting. All values
must match; StateDirectory is reconciled via the held lease reference,
canonically equivalent spellings are not a different store. Different retention,
limits or deadlines result in InvalidConfiguration before any change to stored user states.

An explicit OpenExisting with a new valid configuration is permitted; afterwards
its runtime uses the same configuration. Options are not replaced in the schema or silently
overwritten. A direct store consumer already uses its explicitly validated
factory configuration. If the runtime fails before successful store preparation,
store and lease remain owned by the host. Second ownership remains StoreAlreadyOwned.

## 7. Explicit Local Deletion and Retention

ISessionRetentionStore.DeleteClosedSessionAsync checks and deletes the local subgraph
atomically. SessionCatalog.DeleteSessionAsync delegates via the bounded SessionDeletion
core; no provider operation is involved. Closed and ClosedUtc alone are
not sufficient: the same current check for active/uncertain requests, reservations and open
cases is executed again in the deletion transaction. Ineligible SessionBlocked, missing
session NotFound, no partially confirmed deletion after a storage error.

RetainUntilExplicitDeletion permits the explicit delete without a time period. With Duration,
the full period from ClosedUtc inclusive must be waited out; meanwhile, contents remain
queryable. There is no automatic deletion run at start, stop or expiry of the period. The
host triggers delete. Factory and runtime policy are identical according to section 6; an
explicit reopen may choose a new valid host configuration. Deletion also removes
local results, history, released reservations as well as resolved cases/decisions
of this session only. Other sessions and store metadata are preserved. No guarantee of
physical SQLite page cleanup, external vendor deletion or backup removal.

## 8. Optional Local Diagnostics

A host-owned `SessionDiagnostics` per runtime is passed as an optional sixth
constructor parameter and disposed of by the host only after the runtime has ended.
`SourceName` identifies ActivitySource and Meter. `InstanceId` correlates logs/traces;
for metrics, `Meter.Scope` distinguishes the local instrument instance. Measurement labels
contain exclusively `request.state`, `execution.certainty` and `error.code`,
no local or native IDs. `FailureCount` counts caught sink/sampling errors.

Admission/execution duration histograms use seconds. `multisession.requests.completed`
counts observed, confirmed stored completions of this instrumentation; historical
results and lost commit confirmations are not additionally claimed events.
Queue/occupied/Unknown gauges are based on `ISessionStatisticsStore.GetRuntimeStatisticsAsync`
as one consistent local read projection. Occupied includes Unknown. The runtime
monitor updates initially and after each one-second pause. Observable callbacks
read only the atomic cache and do not block any DB queue. Until the first sample, on error
and after stop, values are withdrawn, not interpreted as zero. Cache values are
not a capacity decision. The complete lifecycle diagnostics acceptance is still open.

Provider responses may return exclusively the categories explicitly listed in `ProviderFailurePolicy`.
Local queue/ownership/store/admission/CAS categories
and undefined values are treated as ExecutionFailed. Error objects receive the
certainty of the reply and no local IDs/admission status supplied by the provider.
Typed provider exceptions receive a permitted category and remain Unknown without separate
end evidence. Observation/storage errors actually detected locally keep their
local category; they are not an unconfirmed claim by the adapter.

## 9. Current Implementation Slice and Remaining Releases

All contracts of this document have a Common/SQLite implementation and automatic
contract/persistence regression. Separate endpoint/account/target system acceptances are
not derived from these tests. After T118, factory/runtime configuration is checked for equality before
recovery. Native bindings and optional model/credential availability
are checked before worker release; own configuration-related locks carry
a nullable sanitized AvailabilityError without lifting recovery/foreign native locks.
Worker cancellation waits for asynchronous cancellation callbacks before resource release, but
keeps to the caller-bound stop deadline. Linux explicitly disposes of its flock lock.

Still open overall gates are in particular T034's unambiguous native Gemini context error
mapping, the missing platform/hour/live/integrator evidence and the historical
TDD audit. Task status and concrete follow-up mapping are in tasks.md as well as the
acceptance matrices under evidence. No schema/binding migration, NuGet publication,
host port/service installation or SharpClaw existing-state migration is part of the implementation.
