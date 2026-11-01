# Data Model: Agent Multisession

**Date**: 2026-10-27 | **Basis**: [spec.md](spec.md)

**Renewed design review**: Constitution v2.0.0; concretized dispatch/recovery/
admission/paging projections according to [contracts/orchestration-details.md](contracts/orchestration-details.md).
Their validation and state transitions are first captured in TDD-01/TDD-03/TDD-04 by
functionally failing tests; persistence behavior follows only afterwards.

## Responsibility Boundary

What is stored is the management, assignment and observed inputs/outputs of sessions.
The local history is evidence, not a request to rebuild provider context oneself.
Tools, agent loops, summaries and model context budgets are not a data model of their own.

A state store belongs to exactly one trust domain and exactly one active
manager instance. A second owner of the same store is rejected. Separate
instances use separate stores. Global mutable registries are impermissible.

## Entities

### ProviderRegistration and AgentProfile

| Field | Meaning and validation |
| --- | --- |
| ProviderId | Non-empty, ordinally compared technical ID; unique per instance |
| AdapterId / ContractVersion | Explicitly registered implementation and contract version |
| Adapter instance / Capabilities | Explicitly registered execution and typed capabilities; no selection via product names |
| Endpoint (HTTP adapter option only) | Absolute approved HTTPS address; HTTP only explicitly for local tests; not a mandatory field of generic backends |
| CredentialReference | Reference to a host-side resolver; no secret in the session state |
| AgentProfileId | Unique named agent configuration |
| ModelId | Opaque model/deployment identifier chosen by the host; no invented model list |
| ExecutionKind | Explicitly AgentTask or ModelResponse; must be supported by the adapter capabilities |
| SettingsRevision | Immutable snapshot of context-relevant settings without credentials |

Provider registrations are normal objects assembled by the host, not a DLL download,
discovery or plugin marketplace of its own. Identical IDs with different meanings
are a configuration error. Secrets are only resolved at the actual call.

### Session

| Field | Meaning and validation |
| --- | --- |
| SessionId | Locally generated UUID; immutable public identity |
| ProviderId / AgentProfileId / ModelId | Assignment recorded at creation |
| SettingsSnapshot / SettingsRevision | Permissible settings required for resumption |
| ExecutionContext | Frozen optional absolute WorkspaceDirectory and canonical WorkspaceKey; validate according to adapter capabilities |
| ProviderBinding | Versioned provider-defined session reference, see below |
| BindingRevision | Confirmed continuation state increased monotonically from 0; no substitute for opaque native identifiers |
| Lifecycle | Opening, Open, Closing, Closed |
| BlockReason | None, RecoveryRequired, ProviderUnavailable, StorageFailure |
| NextSequence | Monotonic next acceptance number within the session |
| CreatedUtc / UpdatedUtc | Unambiguous UTC points in time |
| ClosedUtc | Set only for Closed |

Lifecycle and blocking are separate: an open session can be blocked by uncertain work
without counting as closed or successfully recovered.
A different provider or a different model requires a new session.

**ProviderBinding** contains a native session reference that is optionally known only at the
first handover and a separate, versioned continuation state. An immutable
session reference is not the same as a continuation token that changes after every response.
Only the adapter interprets this data. Do not store credentials, executable type names or
arbitrary deserialization instructions supplied by the model.

If the external reference is missing after a possibly executed first request, the
session is blocked. A new native session is not simply created.

### Request

| Field | Meaning and validation |
| --- | --- |
| RequestId | Locally generated UUID; assigned to exactly one session |
| SessionId / Sequence | Foreign key and unique acceptance order per session |
| Input | Unchanged text message; not empty; size limit in UTF-8 bytes |
| WorkspaceKey | Optional workspace key declared by the host |
| Timeout | Finite execution deadline; begins at the actual dispatch |
| State | Queued, Running, Succeeded, Failed, Cancelled, TimedOut or Interrupted |
| ExecutionCertainty | NotStarted, InProgress, ConfirmedEnded or Unknown |
| AcceptedUtc / StartedUtc / EndedUtc | Lifecycle points in time |
| CancellationRequestedUtc | Separate intent, no proof of external termination |
| DispatchId | Persisted UUID of the only permitted dispatch attempt |
| BindingRevision | Continuation state against which the request was started |

Only Queued counts as waiting; reservations including uncertain executions
count as occupied execution capacity until resolved. Rejected requests receive
no acceptance confirmation and no place in the durable queue.

Acceptance takes place only after a successful commit. A caller cancellation after commit does not
retroactively invalidate the acceptance. If the confirmation is lost, the request remains
queryable; there is no automatic deduplication of a new submission.

Permissible combinations: Queued/NotStarted; Running/InProgress; Succeeded/ConfirmedEnded;
Failed, Cancelled or TimedOut with NotStarted, ConfirmedEnded or Unknown according to
the actual handover status; Interrupted initially with Unknown. InProgress means a
running local dispatch attempt, not an already proven acceptance at the vendor.
After a local final state with Unknown, the reservation persists. A later
recovery may change the certainty to ConfirmedEnded, but MUST NOT turn the historical final state
into a claimed success. Session lifecycle and BlockReason are not request states.

### RequestResult and HistoryEntry

| Field | Meaning and validation |
| --- | --- |
| RequestId | One public final local result per request |
| Outcome | Succeeded, Failed, Cancelled, TimedOut, Interrupted |
| FailureCode | Stable category; optionally a provider-related, sanitized code |
| ExecutionCertainty | Result of the actual termination observation |
| Output | Final text output reported by the provider, not a self-generated substitute response |
| OutputComplete | True only for output captured completely within the permissible size limit |
| ProviderReference | Non-secret external evidence identifier, if available |
| CompletedUtc | UTC point in time |
| HistoryOrdinal / HistoryId (HistoryEntry) | Monotonic session-related insertion order and local UUID per history entry |

HistoryEntry assigns input, output and operational events via SessionId, RequestId
and Sequence. An output above MaxOutputBytes is recorded with Failed/OutputLimitExceeded
without truncated success content; existing history remains unchanged. For this
error, Output remains empty and OutputComplete=false. For regularly complete output,
OutputComplete=true applies. If the external completion cannot be confirmed, Unknown
with a retained reservation is additionally required. A provider context limit is likewise
an error, not an authorization to shorten or compact existing entries.

The local history comprises the contents observed via this library. Contents
already existing at the vendor or created by its internal tools
are not claimed to be completely present locally.

### ExecutionReservation

| Field | Meaning and validation |
| --- | --- |
| DispatchId / RequestId | Unambiguous assignment to the actually reserved attempt |
| SessionId | At most one unreleased reservation per session |
| WorkspaceKey | At most one unreleased writing reservation per key |
| ReservedUtc | Saved before external start |
| ReleaseReason / ReleasedUtc | Only after confirmed end or explicit resolution |

For a writing job, WorkspaceKey is mandatory; the library does not itself determine
from model texts which files an agent could access. The host
supplies consistent canonical keys for shared workspaces. An unresolved
job holds the associated reservation even after a restart.

### ReservedDispatch and RequestAcceptance

ReservedDispatch is the execution snapshot stored transactionally with the reservation,
consisting of Reservation, Session, Submission, Sequence and BindingRevision. GetReservedDispatchAsync
reads it without provider I/O; a release/missing job does not authorize a send. Validation,
binding update and completion CAS are in orchestration-details.md §1.

RequestAcceptance carries the candidate RequestId and the unchanged SubmitRequest. The ID is
generated before commit; Sequence and queue slot are still assigned atomically by the store. AdmissionStatus
is a separate admission certainty, not a request state and not an ExecutionCertainty. Errors,
caller cancel, confirmed commit and lookup follow orchestration-details.md §3.

### RecoveryCaseSnapshot and RecoveryDecision

RecoveryTarget identifies SessionOpening/SessionId or RequestExecution/RequestId.
RecoveryCaseSnapshot carries this target, SessionId, optional RequestId/DispatchId,
CreatedUtc, persisted revision from 1, case status and sanitized resolution information.
Native references are optional; an uncertain opening requires no dummy request.
Creation/CAS/inspect rules and retention of resolved cases are in
orchestration-details.md §2; the list order in §4.

| Field | Meaning and validation |
| --- | --- |
| DecisionId / Target | Unique decision for an uncertain opening or execution |
| ExpectedRevision | Protection against stale or competing decisions |
| Kind | ConfirmEnded or CloseSession |
| Explanation / EvidenceReference | Mandatory justification without secrets |
| DecidedUtc | UTC point in time |

ExpectedRevision comes from the publicly read RecoveryCaseSnapshot. A decision
changes case, certainty and permitted release together or not at all; a stale revision
and an already used DecisionId result in RevisionConflict. BindingRevision and case revision
are different counters with different responsibilities.

ConfirmEnded confirms that no external work is running anymore. The historical final state
Interrupted is preserved; certainty and reservation release are added traceably.
This is not retroactive evidence of success. Before follow-up requests, the adapter must
be able to resume the same provider reference; otherwise the session remains blocked.

CloseSession prevents further execution in the session. Closing alone does not release
an uncertain workspace/capacity reservation: for that, too, saved
termination evidence or the explicit operator confirmation is required.
A retry is only a new, explicitly submitted request with a new identifier.
A recovery decision CloseSession therefore means Closing as long as such a
reservation is open. Closed is only reached after its proven resolution and the end of all
further open requests. Renaming a state MUST NOT bypass a lock.

### StoreMetadata

SchemaVersion, StoreId, InstanceTrustScopeId, CreatedUtc and migration version identify
the store. An unknown newer schema is rejected before write access.
A deliberately new store is initialized explicitly. A missing expected
store MUST NOT be replaced by automatic create-if-missing use.

Version 1 exclusively initializes an explicitly new format and reopens the same
known format. There is not yet any in-place migration or automatic
format conversion. Differing schemas/binding versions lead to an error
before existing payload data or metadata are modified. Even a failed
start MUST NOT publish a substitute store. Later format changes require
separate planning with backup, crash, rollback and acceptance rules.

## State Transitions

| Initial state | Event | Result / atomic effect |
| --- | --- | --- |
| No request | Admission with open session and free queue limit | Commit Queued + input + sequence, then confirm |
| Queued | Cancellation before dispatch | Cancelled / NotStarted; no provider call |
| Queued | Admission with free capacity and free workspace | Running/InProgress + DispatchId + reservation in one transaction |
| Running | Confirmed successful completion | Succeeded + output + new binding state + reservation release in one transaction |
| Running | Confirmed error / cancellation / timeout | Corresponding final state + ConfirmedEnded + release |
| Running | Connection failure, local cancellation without termination evidence | Failed/Cancelled/TimedOut + Unknown; reservation and session locked |
| Running at restart | No safely stored result | Interrupted + Unknown; no automatic replay |
| Final state / Unknown | ConfirmEnded with matching revision | Change certainty and reservation; do not reinterpret the result as success |
| Open | CloseSession | Closing; lock new admission, cancel waiting ones, stop running ones in a controlled way |
| Closing | No open or uncertain executions | Closed; retain history according to retention rule |

An explicitly cancelled job that has not yet been sent may safely end as NotStarted.
After the persisted dispatch marker, Unknown is conservatively assumed if it is
not safely proven that the provider has not accepted a job. As a result,
a crash immediately before the real send can require a manual resolution; this
over-caution is the deliberate alternative to possible duplicate side effects.

## Storage and Transaction Boundaries

Separate tables for Metadata, Sessions, Requests, Results, History, Reservations,
RecoveryCases and RecoveryDecisions; ReservedDispatch is stored bound to the reserved request/
dispatch. Uniqueness for `(SessionId, Sequence)`, RequestId and active
session/workspace reservations is ensured by database constraints.
This document defines the target schema; concrete SQL implementation and integration remain
open tasks. Already existing C# scaffolds do not yet contain all of the fields named above.

- Admission, capacity check and sequence assignment take place together transactionally.
- Only the first waiting job of an executable session may be admitted.
- Database locks are never held across a provider network call.
- After a response, result and continuation state must commit together; a missing
  commit MUST NOT be interpreted at restart as a successful continuation.
- Provider call and local commit are not a distributed transaction. The gap is
  handled by persisted dispatch and Unknown, not by exactly-once promises.
- A channel existing only in memory is a wake-up signal, never the request source.

## Restart and Retention

Start: acquire exclusive store ownership, check schema, check consistency, mark old Running
jobs as uncertain, retain reservations, check provider bindings,
then readmit exclusively unlocked Queued jobs.

Controlled stop is separate from this: it locks new admission, ends after a
finite deadline and stores remaining Queued requests as Cancelled/NotStarted.
A crash before this storing leaves them Queued and thus resumable.
An already stored final state is not started again after restart.

Queued requests have no automatic expiry time. Their request deadline only begins
with dispatch; the host can explicitly cancel them beforehand. To the local admission,
the separate finite AdmissionTimeout deadline applies instead. With free execution capacity,
the oldest admissible session head is preferred; with identical AcceptedUtc,
RequestId decides ordinally. Blocked heads are not treated as executable.
No fixed global waiting time or starvation freedom is guaranteed.

Retention requires an explicit host-side period or the explicit setting
RetainUntilExplicitDeletion. No silent default for content deletion. Only closed
sessions without waiting, active or unresolved work are deletable. Deletion removes
local contents from the library queries, not automatically external provider data,
backups or physical SQLite pages. Secure storage-medium erasure is not a guarantee.

## Limits for Version 1

- Text messages and final text responses; no multimodal processing of its own.
- One active owner per local store; no network file systems or active clusters.
- Configurable finite queue, request, output and time limits.
- No built-in tools, prompt compaction, transcript replay or feature negotiation.
- Public errors contain no tokens, prompts or raw vendor HTTP bodies.

## Implemented Additive Availability Metadata

`SessionSnapshot.ConfigurationUnavailable` marks an availability lock that can be re-checked
before worker release. Nullable `AvailabilityError` contains exclusively a
sanitized ErrorCode category of this check, no texts or credentials. Both
reside in the existing session JSON; no additional SQL column/schema migration.
A missing field from an earlier snapshot is read as null. Undefined categories
or a cause without an origin flag are rejected before recovery mutation. Positive
revalidation clears exclusively its own block/cause; existing recovery/other
native restrictions and the original IDs/ModelId/Settings/binding revision remain.
