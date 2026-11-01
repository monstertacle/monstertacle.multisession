# Operations and safe resumption

The library runs in the host's process. The host owns configuration,
provider objects, credential resolvers and, where applicable, diagnostics. Each runtime
requires its own private local state folder and a matching native
lease. Two runtime instances must not share a store. No static
provider registries or global credentials are required.

## State and execution certainty

A request has a local state (`Queued`, `Running`, `Succeeded`, `Failed`,
`Cancelled`, `TimedOut`, `Interrupted`) and, separately, the certainty `NotStarted`,
`InProgress`, `ConfirmedEnded` or `Unknown`. A local Cancel, Timeout or
Stop result does not prove an end in the backend. In particular, Unknown remains occupied.
A provider success that arrives later does not overwrite a local final result
that has already been secured.

`SubmitAsync` returns a receipt only after a confirmed admission commit. The
request ID and sequence are durable thereafter. If the receipt is lost
and admission is unclear, do not blindly resend: `ListRequestsAsync` and the
session history allow local investigation. A failure with `NotAccepted`,
by contrast, proves that this attempt was not accepted. Failures contain
no raw provider responses; `SessionOperationException.Failure` carries the
public code and the separate admission/execution certainty.

## Deadlines and capacity

| Option | Default | Start/meaning |
| --- | --- | --- |
| AdmissionTimeout | 5 seconds | Entry into Submit, including local admission work |
| RequestTimeout | 600 seconds | Execution from actual dispatch, no queue TTL |
| ShutdownTimeout | 10 seconds | Default for controlled shutdown |
| DbBusyTimeout | 1 second | SQLite busy limit; currently exactly this value is supported |
| MaxConcurrentRequests | 4 | Occupied slots including uncertain reservations |
| MaxQueuedRequests | 100 | Durably waiting requests |
| DbWorkQueueCapacity | 256 | Bounded local infrastructure work |
| MaxInputBytes | 1 MiB | Inclusive limit for strictly valid UTF-8 |
| MaxOutputBytes | 4 MiB | Inclusive limit for final text output |

A `SubmitRequest.Timeout` sets the finite execution deadline of this request.
No automatic queue expiry, provider retry or fallback selection
is implied. Invalid UTF-16 inputs are rejected before admission. An output that is too
large is a failure with empty output, not a truncated success.

At most one unreleased execution is possible per session. Writing
agent tasks require the WorkspaceKey explicitly declared by the host.
Identical keys serialize within the runtime. The host must map actually
identical workspaces to the same canonical key; the
library does not detect file system aliases of workspace names. Independent
session heads can use free slots without changing the FIFO order within a
session. Unknown slots are not additional free parallelism.

## Cancel, Close, Stop and Dispose

`CancelRequestAsync` first persists the cancellation request. Queued work can
end as Cancelled/NotStarted. For Running, the cooperative provider path is
cancelled and the end confirmation is awaited for a bounded time. Without such confirmation,
session/workspace and slot remain reserved. The core performs no forced
backend termination, process search or killing of unknown processes.

`CloseSessionAsync` blocks new admission, ends waiting work and handles
running work in a controlled manner. `Closing` and `RecoveryRequired` remain visible
if the external end is unresolved. Closed is set only once the local
completion condition is fulfilled. Close deletes neither local history nor provider data.

`StopAsync(deadline, token)` blocks admission immediately; its deadline starts at
entry, not only after an internal lock. Waiting work is securely
Cancelled/NotStarted; unconfirmed running work remains Unknown. Open/recovery operations
that have already started are tracked as runtime work. If
local work runs beyond the wait deadline, store and lease remain protected until the end
of cleanup. An expired wait deadline is not proof of disposal.
`Completion` and the observed Stop/Dispose result belong to host supervision.
Dispose during a running start waits for its actual completion
and prevents a subsequent worker release. Concurrent Dispose calls
share the running cleanup; before store preparation has been taken over,
dependencies remain host-owned.
Dispose of providers and diagnostics only after their actual last runtime
use.

## Restart and recovery

Open the expected store explicitly with `OpenExistingAsync`. A missing
store is not replaced. The same private folder, the same `InstanceTrustScopeId`
and compatible provider registrations are needed. The host keeps the
TrustScope ID separately as a non-secret, reliable mapping; do not generate a new UUID
for an existing store. Native bindings and profile revisions are
persisted identities, not freely interchangeable configuration values.

After an unplanned restart, Queued is resumed. Started work that has not been safely
ended becomes Interrupted/Unknown and receives a recovery case. It is
not automatically re-executed. Missing local adapter configuration blocks
affected sessions; it authorizes neither provider changes nor new native IDs.
The current implementation checks local registration/capabilities in this process. A
complete re-check of native credentials/model availability is still
part of the open acceptance T080; local configuration reconciliation does not prove it.

1. Read `ListRecoveryCasesAsync` or `GetRecoveryCaseAsync` and save the revision.
2. Investigate native evidence with `InspectRecoveryAsync`. Inspect alone changes
   neither result nor reservation. Unsupported provider paths remain failures.
3. Submit an accountable decision with a unique DecisionId, Target, ExpectedRevision,
   `ConfirmEnded` or `CloseSession`, a non-secret justification and an evidence reference
   via `ResolveRecoveryAsync`. Outdated revisions are rejected.

ConfirmEnded does not invent a success. CloseSession is not a confirmation of an
external end. Without sufficient evidence, the reservations persist.
The library does not provide automatic format migration: an incompatible
schema/binding is rejected before unchecked changes. This also applies to later formats:
older stores are rejected, never converted (only the current version).

## Data retention and permissions

Retention must be configured explicitly: `new RetentionPolicy(true, null)`
keeps data until explicit local deletion. Alternatively, the policy sets
a finite retention age from ClosedUtc. `DeleteSessionAsync` requires Closed,
a fulfilled age and conflict-free data; open cases or uncertain reservations
are not deleted along with it. There is no automatic deletion service. Local
deletion means neither physically secure media erasure nor provider or
backup deletion. The host decides separately on provider retention and backups.

SQLite uses local DELETE journaling, EXTRA synchronization and foreign keys.
Transactions secure admission, dispatch/fence, observation and result/binding.
No network drives or shared stores. Linux requires own 0700 directories,
0600 files, the current owner and symlink-free canonical paths. Windows
requires local supported volumes, a private user DACL and no reparse/
path aliases. Existing incorrect permissions are rejected and not repaired.
Lease files are not deleted on Dispose. Never process product state with the
intentionally marked crash/diskfull test workers.

## Diagnostics and data protection

Optional host-owned `SessionDiagnostics` use a logger, ActivitySource and
the BCL meter `Monstertacle.Multisession`. Currently, confirmed admission and
completion are reported with IDs, state, certainty, duration and failure category.
The event scope comprises confirmed admissions and observed dispatch completions.
The result counter describes these observations; the stored history remains
the source for all local results, including directly cancelled queue entries.
Gauge snapshots show queue, occupied and uncertain reservations; Unknown is
a subset of the occupied slots. Observers perform no database I/O.
If statistics are unavailable, their validity is withdrawn, rather than zero being invented
as supposed idleness. Request/session IDs are not metric labels.

Do not include prompts, outputs, headers, credential values, bindings or raw
exception texts in library diagnostics. The host bears the same obligation
for its own logs, exports and HTTP handlers. Faulty diagnostics callbacks
are isolated and made visible via FailureCount; they do not change business results.

Current evidence and explicitly open limits are under
[implementation-progress](../specs/001-agent-multisession/evidence/implementation-progress.md).

## Availability on restart

After store bootstrap, the runtime checks preserved Open sessions before worker start.
Registration, contract, binding version and declared capabilities must still match.
An adapter with `ISessionAvailabilityProvider` additionally checks the frozen
model access in read-only fashion; both native HTTP adapters use the model metadata GET for this,
with a freshly resolved credential. Afterwards, `ReopenAsync` checks the same stored
native reference. The overall check is bounded by `RuntimeOptions.RequestTimeout`;
caller cancellation aborts the start. The HTTP adapter additionally has its own finite
header/read deadlines. There is no generation request for the availability check.

A rejection blocks exclusively this session as `ProviderUnavailable`.
`SessionSnapshot.AvailabilityError` names the sanitized cause, for example
`AuthenticationFailed`, `InvalidConfiguration`, `ProviderSessionExpired` or `TimedOut`.
Requests already accepted keep their identifiers and remain Queued; there is neither
a substitute model nor a repeated Open. After access has been corrected, a new host start can
lift its own availability lock after a positive check. On a regular Stop,
queue entries are still cancelled; an old stopped request is not
revived. Foreign native continuation blocks as well as RecoveryRequired/StorageFailure
are not released by this availability check.

Metadata can describe vendor aliases; the library does not store a new
model selection from them. A successful read is evidence of access/existence at that
point in time, not a promise of future quota or generation. The direct lazy reopen of
Gemini remains without HTTP; the additional availability facet is explicitly separate.

`SubmitRequest.Timeout` is the authoritative deadline of each explicitly submitted
task. A host can take its default from `RuntimeOptions.RequestTimeout`;
this option value additionally bounds the startup reopen/availability and recovery reads.
A task that has already been accepted does not consume its execution deadline while Queued.
