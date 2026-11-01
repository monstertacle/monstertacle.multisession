# Public Library Contract v1

**Date**: 2026-10-27 | **Basis**: [spec.md](../spec.md), [data-model.md](../data-model.md)

This is the library contract with the implementation reconciliation of 2026-10-29T21:57:58Z.
It defines no HTTP routes, service installation or user interface.
Namespace root: `Monstertacle.Multisession`.

Re-checked against Constitution v2.0.0; concretization of the existing scope.
For its behavior guarantees, executable failing contract/consumption tests
are to be written first (in particular TDD-01/TDD-04/TDD-07), before the respective behavior comes into being.
The binding dispatch/recovery/admission/paging details are in
[orchestration-details.md](orchestration-details.md). T112–T115 implement the differences explicitly
named there after matching Red; these four contract slices are
now implemented and proven in the story-related TDD evidence.

SharpClaw is intended as a long-term in-process consumer. It uses the same
public contracts as other hosts, without special access to internal classes.
The libraries reference neither its assemblies nor its source code or
configuration files. A separate additional service is not an embedding prerequisite.
The target host can register existing connections via its own ISessionProvider adapters;
their concrete migration and existing states are not part of this initial contract.

## 1. Composition and Lifecycle

The host wires Common with a store, a platform implementation, explicitly
registered providers and a CredentialResolver. Logging and TimeProvider are
injectable. No hidden static registrations or vendor-related switches.

`SessionRuntime` provides the three domain facets Sessions, Requests and Recovery.
These are separate responsibilities, not an obligation for a universal
manager class with all methods. The contract version and the NuGet package follow
SemVer; version 1 starts as package version 0.1.0 before a stable 1.0 commitment.

| Operation | Result / obligation |
| --- | --- |
| StartAsync(options, cancellationToken) | Afterwards exclusively owns the store; schema, options and recovery checked; no network listeners |
| StopAsync(deadline, cancellationToken) | Accepts nothing new; finite deadline; all open requests keep a visible state |
| DisposeAsync() | Releases resources/store lease only after a controlled stop; no implicit deletion of state |

Options are validated before start and not silently changed afterwards. A second start
or an operation after a completed stop is an unambiguous lifecycle error.
The host must explicitly await stop; merely discarding an object is no
proof that external work has ended.

The stop deadline begins at receipt of the stop request and is measured monotonically.
New admission is locked from this point on. Within the deadline, open work is
completed; Queued requests remaining afterwards explicitly end as
Cancelled/NotStarted. Running work receives a confirmed final state or Unknown
with a retained reservation. A process crash before saved finalization, by contrast, is
a restart: requests still stored as Queued are not assumed to be cancelled.
If the store is unavailable, stop MUST NOT claim durable success; the error
remains visible, and the last saved state is authoritative at the next start.

## 2. Sessions — Session Catalog

| Operation | Input | Result and semantics |
| --- | --- | --- |
| CreateSessionAsync | string agentProfileId, SessionExecutionContext context, CancellationToken | Local SessionId and snapshot; context contains nullable WorkspaceDirectory/WorkspaceKey; native opening insofar as the provider supports it separately |
| GetSessionAsync | SessionId | Snapshot including provider binding, Lifecycle and BlockReason; no secrets |
| ListSessionsAsync | Bounded page size, optional cursor | Only sessions of this instance; stable sorting by CreatedUtc and SessionId |
| ReadHistoryAsync | SessionId, bounded page size, cursor | Locally observed inputs, results and operational events; no claimed complete native chat |
| CloseSessionAsync | SessionId | Lock new admission immediately; cancel waiting requests, end running ones within the deadline or save them as uncertain |
| DeleteSessionAsync | SessionId | Only Closed without an active/unresolved reservation; remove local data according to the deletion contract |

New local identity and SettingsSnapshot are stored before external opening.
If a provider only assigns a native ID with the first message, the
binding remains empty until then, without claiming an invented external session.
If an external opening possibly fails after acceptance, a blocked
local record remains findable; the error contains its SessionId.

Closing or deleting does not trigger an implicit deletion at the vendor. Modifying
operations with foreign/unknown identifiers return NotFound; individual Get read
operations return null for an unknown identity. Data of other instances is not searched.
A close with an uncertain reservation returns Closing with RecoveryRequired, not Closed.
The close intent alone is neither proof of termination nor release of the workspace.

## 3. Requests — Durable Admission and Status

| Operation | Input | Result and semantics |
| --- | --- | --- |
| SubmitAsync | SubmitRequest(SessionId, Input, Timeout, WorkspaceKey, WriteIntent), CancellationToken | RequestReceipt with RequestId and Sequence only after commit; WorkspaceKey is nullable, WriteIntent is an explicit bool |
| GetRequestAsync | RequestId | Snapshot of state, result, certainty and sanitized error |
| ListRequestsAsync | SessionId, bounded page size, cursor | Findability even after a lost acceptance confirmation |
| CancelRequestAsync | RequestId | Visible cancellation request; the return does not prove external termination |

Submit means acceptance, not already completed provider work. The caller can
read the state repeatedly; a persistent UI/SSE push protocol is not presupposed.
The CancellationToken passed to Submit initially concerns the local admission operation.
After commit, CancelRequest is used to end the stored job;
a lost client wait MUST NOT silently delete a confirmed request.
AdmissionTimeout begins on entry into SubmitAsync and limits only the local
wait for acceptance, not the later queue waiting time. Deadline/token cancellation before a safely
completed commit means no confirmed acceptance. If the commit outcome is unknown at
cancellation, the error MUST NOT claim safe non-acceptance; the session's list
of requests remains the lookup path. A new submit is not automatically deduplicated.

The acceptance order is the Sequence assigned under the SQLite transaction, not the
wall-clock order of parallel callers. At most one running dispatch per session.
Separate sessions can run in parallel as long as global capacity and workspace permit.

When the queue limit is full, QueueFull occurs without acceptance. Infrastructure queues are
likewise bounded; admission may wait in a controlled way or be rejected with LocalCapacityExceeded,
but never buffer tasks or messages without bound.

An accepted Queued request has no automatic waiting deadline; RequestTimeout
only begins at dispatch and is measured monotonically. It can be explicitly
cancelled beforehand. Among admissible session heads, the oldest acceptance counts,
with equal AcceptedUtc the ordinally compared RequestId. Blocked heads are
skipped without changing the order within the same session. Fixed waiting times
or global starvation freedom are not guaranteed. Uncertain reservations continue to occupy
slots; when the limit is fully occupied, there is no guarantee of further progress.

After timeout/cancellation, the library starts no further follow-up requests of a session
as long as the actual termination of the previous dispatch has not been resolved.
This applies even if a Cancelled/TimedOut result is already visible locally.

## 4. Recovery — Explicit Instead of Blind Replay

| Operation | Input | Result and semantics |
| --- | --- | --- |
| ListRecoveryCasesAsync | Bounded page size, cursor | Open RecoveryCaseSnapshots for SessionOpening and RequestExecution including current revision |
| GetRecoveryCaseAsync | RecoveryTarget | Single case snapshot including revision, also after resolution as long as the session is retained; unknown → null |
| InspectRecoveryAsync | RecoveryTarget | Reads the known native state without send/retry, revision change or reservation release |
| ResolveRecoveryAsync | RecoveryDecision with Target/ExpectedRevision/justification | Persists resolution atomically; stale or duplicate decision → RevisionConflict |

RecoveryTarget addresses either an uncertain opening via SessionId or an
execution via RequestId. No dummy requests are created. Creation, revision,
closing without a known native reference and CAS rules: orchestration-details.md §2.

ConfirmEnded presupposes that external completion has actually been determined or has been explicitly
confirmed by the responsible operator after investigation. A missing
local PID, an expired timer or an HTTP connection end is not sufficient.

A continuation additionally requires a valid native reference or a
continuation state obtained by the adapter from confirmed evidence. Without this proof,
the session remains blocked or is closed. No transcript replay and no
silent reopening under the previous identifier. The original Interrupted/error
result is not retroactively rewritten into an unproven success.

A retry happens exclusively through a new submit with a new identifier after an
explicit decision. There is no exactly-once promise for external vendors.

## 5. Options and Host Obligations

| Setting | Initial value / validation |
| --- | --- |
| MaxConcurrentRequests | 4; positive; Unknown reservations continue to count |
| MaxQueuedRequests | 100; positive; counts durable Queued jobs instance-wide |
| MaxInputBytes / MaxOutputBytes | 1 MiB / 4 MiB; positive; UTF-8 bytes, no hidden character assumptions |
| AdmissionTimeout | 5 seconds from entry into SubmitAsync; positive and finite; concerns only local admission |
| RequestTimeout | 600 seconds; positive and finite; begins at dispatch |
| ShutdownTimeout | 10 seconds; positive and finite |
| DbWorkQueueCapacity | 256; positive; limits local database work |
| DbBusyTimeout | 1 second; positive and less than ShutdownTimeout; no unbounded DB retries |
| Retention | Explicit period or RetainUntilExplicitDeletion; no silent content deletion |
| StateDirectory | Absolute, private, local file system; explicitly initialized |

Input/output limits are inclusive: exactly the limit is permissible, one byte above it
is not. An empty input or an overrun is rejected before acceptance as InvalidInput or
InputLimitExceeded. With an oversized provider output, the request ends with
Failed/OutputLimitExceeded, empty Output and OutputComplete=false, not with a truncated
success. Earlier history entries remain unchanged. If the external end cannot be confirmed,
Unknown with reservation remains; the native context management is not changed.

A vendor call additionally has a finite HTTP header timeout of 30 seconds.
Polling begins with a one-second interval; server-side Retry-After indications are respected for
harmless status queries without exceeding the overall request deadline.
There is no automatic retry of opening/send POSTs.

The host is responsible for authentication, user authorizations, provider accesses, permitted
endpoints, registration of the adapters and consistent workspace keys.
A host registration is not a sandbox approval for untrusted code. Memory shared by different
users or deliberately shared storage paths are not offered as
secure internal tenant separation.

The host must operate storage-medium/provider retention and any necessary backups.
Provider state can expire despite existing local history. This library
guarantees no unlimited provider memory or recovery after loss of the storage medium.

## 6. Store and Platform Contracts

`ISessionStore` exposes domain-level atomic operations such as CommitAcceptance,
TryReserveHead, CommitCompletion, MarkUncertain and ResolveReservation instead of a
generic CRUD repository with which the core could inadvertently bypass transaction rules.
The store implements no provider calls. All DB work is bounded and serialized.

The pure `ReservationPolicy` can evaluate a complete consistent `QueuedRequest` projection
within this atomic store operation. It contains only already
accepted/validated Queued records with Sequence/AcceptedUtc; WriteWorkspaceKey is
set only for write jobs. Selection and subsequent reservation must take place under
the same transaction. A policy call alone reserves nothing.
`RequestScheduler.TryReserveAsync` delegates such an operation to the store;
RequestDispatcher.TryDispatchAsync processes a fresh reservation with its
ReservedDispatch and fixed registrations. False means no permissible dispatch;
true confirms a stored local final result, including a confirmed error result.
Store errors are reported as errors, not as an empty queue. Callback confirmation,
binding identity and output limit are checked before completion; unconfirmed work
keeps its reservation. SessionRuntime/worker composition remains T026.

The ReservedDispatch stored in the same reservation transaction is readable via
GetReservedDispatchAsync and carries input, timeout, frozen session context
and BindingRevision. Public RequestSnapshots do not replace this record.
The C# query interface has had this operation since T112; contract see
orchestration-details.md §1. Since T114, admission uses RequestAcceptance with a pre-generated
candidate identifier; only Sequence/queue capacity remain store authority (§3).

All lists follow the same PageLimits/cursor rules from orchestration-details.md §4:
page size 1–100, versioned range-bound keyset cursors, no cross-page
snapshot guarantee. These rules are implemented locally test-driven in T115; productive SQLite queries and
persistence acceptances remain T063.

`IStateDirectoryLease` represents checked private storage and exclusive ownership.
Windows/Linux implementations check permissions, local paths and the lifetime lock.
The lease remains open for the entire store usage. A foreign instance receives StoreAlreadyOwned.
Faulty permissions, an unsupported platform or network paths are reported before acceptance
of a request. A Windows package on Linux is not a silent no-op fallback.

The shared core is checked with test implementations of these contracts. This requires
no platform-specific libraries or real vendor accesses.

## 7. Error Contract

Public errors consist of a stable category, SessionId/RequestId where available,
execution certainty and a sanitized optional provider code. Raw data, secrets,
authorization headers and complete prompts do not belong in the error description.
For admission errors, the separate optional AdmissionStatus is added; external certainty
does not replace a statement about the local commit. Caller cancel remains recognizable via an
AdmissionCancellationException derived from OperationCanceledException.
The concrete error/receipt/Unknown semantics are defined in orchestration-details.md §3.

Categories: InvalidConfiguration, NotFound, SessionClosed, SessionBlocked, QueueFull,
LocalCapacityExceeded, StoreAlreadyOwned, StorageUnavailable, UnsupportedStoreVersion,
UnsupportedPlatform, AuthenticationFailed, ProviderRateLimited, ProviderUnavailable,
ProviderSessionExpired, ProviderContextLimit, UnsupportedProviderAction, ExecutionFailed,
Cancelled, TimedOut, Interrupted, RevisionConflict, InvalidInput, InputLimitExceeded,
OutputLimitExceeded and AdmissionTimedOut.

Locally invalid inputs are rejected before acceptance. An already accepted
job durably receives its error as a result. Context limits are passed on
without compression, model switch, hidden retry or truncation of existing history.
Queued/Running are not final; Succeeded, Failed, Cancelled, TimedOut and Interrupted
are local final states. ExecutionCertainty is separate from this and can, in particular
after Cancelled/TimedOut/Interrupted, still be Unknown. Permissible combinations
are bindingly defined in the data model; Lifecycle and BlockReason belong to the session.

## 8. Observability and Acceptance

Structured events contain instance, session, request and dispatch identifiers,
lifecycle transition, certainty, duration and error category, but no contents/secrets.
BCL metrics: waiting requests, active or uncertain reservations,
admission duration, execution duration and result counts. No high-cardinality RequestIds
as metric labels; activity correlation can use them in targeted traces.
Log errors MUST NOT cause an already committed job to be lost.

| Requirements | Contract/acceptance reference |
| --- | --- |
| FR-001, FR-014, FR-017, FR-023 | NuGet consumption, Common without platform, real Windows/Linux host test |
| FR-027 | Embedded consuming host via the same public contracts; SC-011 |
| FR-002–FR-007, FR-022, FR-024–FR-025 | Provider contract, routing, native binding and unchanged contents |
| FR-008–FR-013, FR-016 | Admission, FIFO, capacity, workspace, cancel and stop |
| FR-015, FR-021, FR-026 | Sanitized diagnostics, permissions, retention and instance separation |
| FR-018–FR-020 | Commit, restart, Unknown and explicit resolution |

All time/load values are targets to be validated, not already achieved measurement results.

## 9. Agent/Model Boundary and Target Host Mapping

The generic adapter contract from `provider-contract.md` §8 is binding.
CreateSession additionally receives the explicit `SessionExecutionContext`; workspace,
ExecutionKind, model and adapter assignment are frozen for continuations.
`ProviderRegistration` registers an adapter instance including technical identity and
contract version. Endpoint and authentication options belong in the concrete
adapter, not as mandatory HTTP fields in a generic registration.
HTTP adapters must still check permitted absolute HTTPS endpoints; this is not
a removal of the security requirements. Credentials are not copied into session snapshots.

The local UUID and the opaque external session identifier remain separate; neither
a provider prefix nor a UUID format may be centrally presupposed for external identifiers.
Integration with the target host requires matching contract cases with an observed
native ID, confirmed terminal end, workspace and cancellation. A model REST
fixture alone does not fulfill this proof. Existing sessions are not migrated.

## Implementation Reconciliation: Resumption and Availability

The current public call for creation is
`CreateSessionAsync(string agentProfileId, SessionExecutionContext context, CancellationToken cancellationToken)`;
`SessionExecutionContext` carries optional WorkspaceDirectory and WorkspaceKey. The
tables above are semantic short forms. Before worker start, stored Open
sessions are checked locally and via their same native reopen reference. The optional
facet `ISessionAvailabilityProvider.ValidateAvailabilityAsync(ProviderSessionContext,
CancellationToken)` enables an explicitly read-only model/credential check;
both native adapters implement it, the generic runtime knows no vendor names.

SessionSnapshot adds `ConfigurationUnavailable` as the origin of a re-checkable
availability lock and nullable `AvailabilityError` as a sanitized ErrorCode category.
A successful re-check removes exclusively its own locks/cause; other
native restrictions and recovery precedence remain. No bound model, native ID,
settings or binding revision are replaced from metadata. Queued remains unchanged on start
rejection. The check uses the runtime execution deadline; an available
other provider can continue to work after preparation. Details and limits in
`docs/operations.md`, concrete Red/Green evidence in `evidence/tdd-us2.md`.

The existing availability store write receives an ErrorCode category as an optional last
parameter; old calls remain source-compatible. Current snapshots persist
the field in the existing JSON. Older snapshots without the property remain readable; older
binary versions can safely reject unknown new fields. This implies no guarantee of
backward-compatible pre-format migration or a new schema version.
