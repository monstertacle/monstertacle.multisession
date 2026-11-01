# Native Provider Contract v1

**Date**: 2026-10-27 | **Basis**: [research.md](../research.md), [public-api.md](public-api.md)

**Renewed design review**: Constitution v1.6.0 requires, before every adapter implementation,
the matching executable failing HTTP/session contract tests from TDD-05.
The native provider boundary and the unchanged functional scope remain in place. Live accesses
are not required for the first Red evidence and do not replace it.

## 1. Shared Interface

`ISessionProvider` is a narrow .NET contract for adapters provided by the host.
Contract types reside in Common. No CLI, HTTP, SDK or vendor classes in the core.

| Operation | Semantics |
| --- | --- |
| OpenAsync | Open a native session if separately possible; otherwise return an explicit not-yet-bound binding |
| ReopenAsync | Check and reuse the persisted native reference; never silently reopen or replay history |
| SendAsync | Hand over only new input plus immutable profile settings and the last confirmed binding state |
| InspectAsync | Read a known external operation identifier; no re-execution and no agent step of its own |
| CancelAsync | Native cancellation request, where supported; distinguish confirmation and actual final state |

SendAsync reports a received external operation identifier, before further polling, to
a bounded awaitable persistence callback. Its successful completion confirms
that the identifier was stored. If it cannot be stored, success MUST NOT be
reported; the core receives Unknown and holds the reservation.

ProviderReply contains the final output, result/error category, confirmed certainty
and optionally updated versioned binding data. The core saves result and
continuation state together. Providers MUST NOT modify any database of the core directly.

Providers must observe cooperative cancellation and finite network deadlines.
A non-cooperative adapter is a contract violation, not an implicitly existing
security sandbox. After the local deadline end, the core may report Unknown and must
prevent further dispatches in the session/workspace instead of reporting a callback as ended.
Before handover, the adapter checks only locally known contract/configuration errors.
Functional limits that are only detectable at the vendor are attributed provider errors,
not a request for complete feature exploration or for rebuilding a substitute function.

If an output exceeds the configured size limit, the adapter MUST NOT report a truncated
response as a complete success. It returns OutputLimitExceeded with empty
Output and OutputComplete=false. A reception aborted as a result is no proof
that external work has ended; certainty and reservation follow the recovery contract.

## 2. Identity and Settings

- The local SessionId always remains stable.
- The native SessionReference may initially be missing, as long as nothing has ever been sent.
- A native OperationId denotes exactly one external call, not the session.
- ContinuationState may be advanced after a confirmed completion; this is
  not a provider/model change and not a replacement of the local session identity.
- Agent profile, model and context-relevant settings are frozen at creation.
  Credentials are obtained fresh from the host via a reference, not stored in the snapshot.
- A changed continuation format requires an explicit contract/storage migration;
  unreadable older/newer bindings lead to an error instead of an empty restart.
  In the first delivery, such migrations are not performed automatically:
  only the known format version is opened, others are rejected without modification.

## 3. OpenAI Reference Adapter

**Protocol**: HTTPS JSON, OpenAI REST `/v1`, base `https://api.openai.com/v1`.
Bearer credentials exclusively via the host. A configurable base URI must offer the same
Conversations/Responses contract; chat completions compatibility is not sufficient.

| Flow | Concrete contract |
| --- | --- |
| Open | `POST /conversations`, save conversation ID |
| Reopen | `GET /conversations/{conversation_id}`; a missing reference is ProviderSessionExpired |
| Send | `POST /responses` with model, conversation, exclusively new input, background=true and store=true |
| Inspect | `GET /responses/{response_id}` |
| Cancel | `POST /responses/{response_id}/cancel`, then check the final state |

The chosen conversation ID is not replaced by a response ID.
`previous_response_id` is NOT used simultaneously with conversation.
Queued/in_progress means running; completed is a successful completion only without required client-side
action. Failed, incomplete and cancelled are separate results.
A valid response without text or with a client-side function call is not
treated as an invented text response or as an occasion for a tool loop of its own.

API versioning follows the REST contract `/v1`; the concrete request/response scope
and documented fields are frozen via checked-in contract fixtures.
Models are explicit host configuration, not a hard-coded model list.

Conversation storage lifts neither model context limits nor any lifetime limits
of other provider resources. The library does not invoke a compaction flow of its own.
Automatic context functions already configured on the provider side remain on the provider side.

## 4. Gemini Reference Adapter

**Protocol**: HTTPS JSON, stable Interactions API `/v1`, base
`https://generativelanguage.googleapis.com/v1`; the host supplies `x-goog-api-key`.
No mixing with old `/v1beta`/API revision examples.

| Flow | Concrete contract |
| --- | --- |
| Open | Only prepare the local binding; the first message creates the native interaction |
| First send | `POST /interactions` with model, new input, background=true and store=true |
| Further send | Same path, additionally previous_interaction_id of the last confirmed completion |
| Reopen / Inspect | `GET /interactions/{interaction_id}`; check the stored head |
| Cancel | `POST /interactions/{interaction_id}/cancel`; await later status confirmation |

The first confirmed native interaction and the respective chain head are stored separately.
An interaction in the in_progress state MUST NOT be used as a confirmed continuation head.
After a crash with an uncertain new head, no silent branching from the old head.

The native chain contains history but does not automatically adopt system_instruction,
tools or generation_config from the last call. Only already permissible immutable
profile settings are handed over again; the library does not itself create tools or
feature configurations. Unknown settings requested by the host are checked
per adapter, not with a central cross-vendor feature matrix.

completed without required client action is success. in_progress remains running;
requires_action is UnsupportedProviderAction. Failed, incomplete and cancelled remain
distinguishable. A cancel POST is not a documentation-wide idempotency guarantee.
Retention may vary per tier/setting; expired interactions are not rebuilt locally.

## 5. Uncertain Outcome

Before every external send, the core persists DispatchId and reservation. A timeout
between handover and received OperationId remains Unknown. The adapter MUST NOT
automatically send again because of a network error.

If the OperationId is known, an explicitly requested Inspect can determine the native final state.
This does not yet silently change the local request or reservation;
the recovery interface decides on safe release and the valid continuation state.
No existing diagnostic log entry replaces a saved result or binding.

Reading status queries may be repeated on temporary errors within the finite deadline.
Authentication errors, context limits and unsupported
actions are not concealed by repetition or automatic model/provider switches.

## 6. Extension and Compatibility Probe

Further compatible endpoints are integrated exclusively through configuration.
A different native contract receives a further implementation of ISessionProvider.
The host registers it explicitly. There is no deriving of behavior from
provider names, no arbitrary assembly file in the request and no open plugin loader.

Every adapter must pass the same set of tests: opening/lazy binding, follow-up request,
unchanged assignment, updated confirmed head, wrong/missing reference,
error/timeout/cancel states, retention errors and absence of own feature rebuilding.
A third controlled test implementation demonstrates extensibility without
rewriting the core or a consuming application in a vendor-specific way.

Live acceptance uses at least two real vendors with one opening, first
message and follow-up request each on all guaranteed target systems. A fake probe proves
the management contract, not the actual vendor operation.

## 7. Not Included

Own token limits, context compaction, tool execution, agent loops, multimodal
conversion, model catalog synchronization, dynamic agent delegation, unrestricted
CLI remote control and automatic retry of send operations.
Remote access for end users or operating system services are likewise not a provider function.

## 8. Generic Agent Backend Boundary — Binding Concretization

`ISessionProvider` denotes a registered execution adapter, not necessarily
a vendor HTTP API. CLI, SDK and model adapters use the same session boundary.
No selection via product names in the core and no source/assembly dependency on the target host.

- `ProviderCapabilities` explicitly names support for agent jobs, model responses,
  native inspection and native cancellation as well as a required workspace.
  An `AgentProfile` defines `ExecutionKind` (`AgentTask` or `ModelResponse`).
  Unsupported requirements are rejected before handover; a model response
  MUST NOT fulfill a requested agent job. No tool loop of its own as a substitute.
- `SessionExecutionContext` carries the optional absolute `WorkspaceDirectory` and
  the canonical WorkspaceKey supplied by the host. Snapshot and assignment are stored
  with the session. A required workspace must be validated before start.
  The key remains separately authoritative for write serialization.
- `ProviderCall` contains local session/request/dispatch identifiers, ModelId,
  ExecutionKind, execution context, new input, limits and immutable permissible
  profile settings. `ProviderBinding` carries AdapterId, format version, optional
  stable `SessionReference` and a separate confirmed `ContinuationState`.
- Open/Reopen use a `ProviderSessionContext`, without a dummy request or dummy prompt.
  Lazy open without a native ID is permissible only before the first possible handover.
- Send receives an awaitable observation callback. `ProviderObservation` distinguishes
  `SessionEstablished`, `OperationAccepted` and content-free `Progress`.
  The core must durably save a new native ID/operation ID and confirm the callback
  before the adapter continues. A callback error does not mean a confirmed
  successful execution. An already bound native identity MUST NOT change.
- Only a `ProviderReply` with an explicit final Outcome and ExecutionCertainty confirms
  the local completion. A progress/tool intermediate step, an operation ID or
  a process exit code alone is not success. ConfirmedEnded and Unknown remain
  different; so do a cancel request and a proven completion.
- Inspect/Cancel without the capability return a typed Unsupported result,
  not a silent no-op. The decision on reservation release remains in the core.

**Target host mapping**: host-side RunContext → session/call context; observed
native session ID → confirmed SessionEstablished callback; TerminalSuccess plus
exit code → final reply; CancellationToken → cooperative adapter cancellation.
Workspace/model/backend remain bound to the same session snapshot. The host
remains responsible for its tools, sign-ins and process execution. This mapping
is a verifiable design, not yet a passed consumption or migration proof.

**Binding reference**: OpenCode **1.19.0**, explicitly retained by the project owner.
No update and no adoption of newer CLI/V2 API contracts.
OpenCode remains an optional external backend, not a mandatory runtime (FR-022).
A reference fixture may model its session/completion contract but MUST NOT feign
real OpenCode execution or real provider acceptance.

**Additional contract cases before Green (T010/T035/T089)**: hand over workspace/ExecutionKind and
an unchanged model identifier to a host-owned backend; continue an opaque `ses_…` across
two jobs; save the ID before further external progress; reject an identity change;
do not count exit 0 without terminal success or a tool intermediate step as success;
callback errors and cancel without end evidence keep Unknown; reject unsupported capabilities
before handover. These cases supplement, but do not replace, the two native
vendor contracts, persistence/process probes or the actual package consumption.

## 9. Operation-Bound Resumption after Unknown

An adapter whose uncertain execution can change a native continuation head
sets `ProviderCapabilities.RequiresOperationAwareRecovery=true` and implements
`IExecutionRecoveryProvider.ReopenAfterExecutionAsync`. The port receives the stored
known binding reference and the optional persisted operation identifier. It reads native
evidence and returns exclusively a confirmed compatible continuation state.
It opens and sends no work. A still readable old head alone is not sufficient.
If a reference needed for this check is missing, UnsupportedProviderAction/Unknown remains;
a missing port is no reason to fall back to ordinary reopen.

With stable native conversation IDs, ordinary reopen remains permissible. The selection
is capability/interface-bound, without vendor names in the core. Confirmed end and
continuation state are only published with the explicit revisioned recovery decision;
Inspect alone changes nothing. The historical request does not retroactively become
Succeeded. Close/Closing requires no reopening for continuation.

## 10. End and Continuation Availability

`ProviderReply.ContinuationAvailable` is an optional statement by the adapter. `false`
names a known continuation blocker, `null` defers to the unchanged legacy/error policy,
`true` is no proof of an external end. Common does not interpret vendor-specific
ContinuationState strings. After a safe completion, result/history/binding,
ProviderUnavailable lock and safe slot release are stored together. Native expiry
also blocks legacy producers without this optional field.

With Unknown, RecoveryRequired including the held reservation takes precedence. A negative
continuation signal MUST NOT replace missing end certainty. Conversely, a native
completed head remains usable even with a local OutputLimitExceeded; a local text error alone
does not justify falling back to an outdated head. The first safely failed
interaction call without a completed head can preserve its known root, but MUST NOT silently permit a
new root or further admission.

## Optional Read-Only Access Check Before Resumption

`ISessionAvailabilityProvider.ValidateAvailabilityAsync` is an additional generic
facet; it does not change any existing reopen/lazy contract. Native adapters read the
chosen model metadata entry via their already configured endpoint and current
host credential resolver. The basis is [OpenAI Model retrieve](https://developers.openai.com/api/reference/resources/models/methods/retrieve)
and the stable GET `v1/models/{modelsId}` in [Gemini's official v1 discovery](https://generativelanguage.googleapis.com/$discovery/rest?version=v1).
No prompt is sent and no complete catalog is built. A missing
model resource is ProviderUnavailable; auth remains AuthenticationFailed instead of appearing as an
expired native session identity. ModelId and binding remain frozen.

Third-party adapters can implement this facet explicitly or already fulfill the same access check of their own
in Reopen. The core derives capabilities from the contract,
not from vendor/model names. RecoveryRequired and other native locks are
not lifted by a successful metadata read. Alias metadata do not prove
model substitution by the library. The real account/backend acceptance remains T105.
