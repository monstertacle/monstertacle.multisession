# Source analysis and decisions

## Inspected baseline

Own source: `84d0c0878fd2254300d06230f447ecc8c78c65f6`. Framework source: `b7884ad88eb85c27b2a7bf40c0e15948e7ccc28d`. Durable extension: `5289ab6dae5bc3e0ad1bc16889b338854be33525`. Production dependency candidate: published `Microsoft.Agents.AI.Workflows` and `Microsoft.Agents.AI` 1.27.0, MIT; lock files and vulnerability audit required before release. SDK remains 10.0.403.

- `Common/SessionRuntime.cs`: owns lifecycle, typed CQRS and facade composition; preserve.
- `Common/Sessions/SessionCatalog.cs::CreateSessionAsync`: persists opening before adapter work and blocks uncertain opening; preserve.
- `Common/Requests/RequestDispatcher.cs::SendAsync`: dispatch comes from a frozen reservation; observations are awaited; completion uncertainty becomes recovery rather than resend. Preserve all of it.
- `Common/Requests/DispatchReplyPolicy.cs`: verifies immutable binding identity, certainty and output. Continue using this normalizer.
- `Common/Contracts/ISessionProvider.cs`, `ProviderBinding.cs`, `ProviderCapabilities.cs`: adapter seam, distinct stable identity/continuation, explicit capability declarations. Extend at this seam; no core framework coupling.
- `Storage.Sqlite/RecoveryTransactions.cs` and `UnknownTransactions.cs`: restart and uncertain effects remain owned by durable storage. A workflow checkpoint is not a replacement transaction.
- Existing HTTP protocol modules own transport, credential resolution, native identities, polling and structured errors. Their existing vendor names are replaced by protocol names; wire adapter IDs remain unchanged to avoid changing persisted state. Protocol IDs are technical interoperability metadata, not class names or runtime branch selectors.
- Framework `AIAgent.cs`: session creation, serialization and deserialization are separate async methods; agent owns model/tool execution. `AgentResponse.cs` exposes finish reasons and typed content; `.Text` alone does not prove completeness.
- Framework `WorkflowBuilder.cs`, `FunctionExecutor.cs`, `RequestPort.cs`: generic typed executors can run without models. `InProcessRunner.cs` executes effects before checkpoint; old checkpoint may repeat effects. `CheckpointManager.Default` is in-memory.
- Durable extension `DurableExecutorDispatcher.cs`: RequestPort wait has no built-in deadline. Do not introduce this second durable scheduler for the local migration.

## Architecture candidate

New `Monstertacle.Multisession.AgentFramework` package references Common and framework 1.27.0. `FrameworkSessionProvider` implements the existing seam with a host-supplied context-to-agent factory and explicit options. Agent instances and their resource lifetime belong to the host; every dispatched turn restores an independent session from confirmed serialized state. Factory must return a stable agent ID for that context, implement serialization/restoration without dispatch and own any external credentials/workspace/tool configuration.

The binding uses the existing local session ID as its stable framework session reference; a versioned continuation envelope also binds agent ID and session ID to serialized state. It is explicitly a framework-managed session, not a promise to recover an arbitrary vendor-native session. Incompatible state fails before RunAsync. No reconstructed history is submitted as new input. Bounded state size and time limits apply. Only an explicit terminal response with complete supported text is successful; no inference from partial stream content. Serialization failure after known terminal response blocks continuation while preserving known termination. Generic inspection and remote cancellation are unsupported; local cancellation remains unknown after invocation.

Evidence renewal is a separate typed framework workflow. Immutable graph nodes carry ID, kind, revision and dependency IDs. A topological validation pass rejects cycles/missing IDs before callbacks. Each node fingerprint includes its transitive dependency fingerprints. Caller-supplied validators approve artifact receipts for exactly that fingerprint; Source/Rule input revisions are not generated judgments. A bounded executor loop requests one renewal at a time, waits for acknowledged durable request results, and emits a final report with accepted receipts, progress and coordination reason. No internal persistence/checkpoint replay or second execution ledger. Callers persist reports and link requests; unresolved effects block the round. Retrying a blocked round requires caller-retained receipts/request references and does not authorize resubmission.

## Requirements review disposition

Separate contrary requirements review accepted: distinguish adapter entry-point count from internal tools; explicitly reject incomplete response types; separate serialization failure from uncertain execution; reject invalid graphs and stale/duplicate receipts; require effectful renewal to use durable requests. Own-position weaknesses and source-backed review summaries are retained in review evidence. Architecture review is a separate gate before code.

## Test plan and risks

Observed-red tests before implementation: agent session restoration/new-turn input, malformed state, acknowledgement-before-invocation, unsupported actions, cancellation and invocation exceptions, incomplete content, output/state limits and serialization failure. Integration uses the actual SQLite runtime and deterministic AIAgent. Renewal tests cover transitive invalidation, independent evidence, cycle/missing/duplicate IDs, validator rejection, revision mismatch, duplicate receipts, status-only blockers and finite coordination. Package-only consumer, native CI, full Codemetr and overall 87-percent coverage include the new package. Published package versions cannot be silently replaced; new artifacts use 0.2.0-alpha.2 with an explicit migration guide. No live host or active session conversion is performed by this library change.

## Architecture review disposition (pre-code)

Separate contrary architecture review raised two blockers; both are resolved by contract narrowing, without replacing the core.

1. Renewal accepts only already durably associated `EvidenceWorkItem` request/session IDs and fingerprints and reads `IRequests.GetRequestAsync`. It never calls SubmitAsync, CancelRequestAsync or a raw agent. Missing association is coordination, including after lost admission receipt. A new round cannot manufacture a replacement request. Hosts persist association before exposing work to renewal; ambiguous admission remains a host recovery case. This eliminates the unsafe callback submission window rather than inventing a second idempotency ledger.
2. Known terminal response followed by serialization failure returns the original identity-compatible binding with `ContinuationAvailable=false`, not null binding. `DispatchTermination` can retain known finality during concurrent cancellation while `SessionBindingPolicy` blocks stale continuation. Test through actual durable runtime.

Response classification: require Stop finish reason, no background ContinuationToken, final message role Assistant with nonempty TextContent only. Earlier completed tool/intermediate messages are not concatenated into final output. Length/filter/tool-call/null/unknown finish reason, final nontext/approval, empty/missing final message or background token produce Unknown and blocked continuation. Once the narrow terminal shape is established, serialize the updated session; serializer/state-limit failure produces ConfirmedEnded failure with original binding and ContinuationAvailable=false. UTF8 output-limit failure preserves the newly serialized binding and confirmed end but never returns truncated success text.

Architecture approval is limited to this narrowed design and its tests. Agent lifetime, configuration equivalence and serializer compatibility are host responsibilities; stable IDs alone are not proof. Unsupported remote recovery can permanently block a session and is explicitly preferred to unsafe replay. Framework package compatibility, native CI and real-host qualification are not inferred from source review.

Review accounting: requirements advocate 12 read-only shell calls, measured inspection 13:36:05Z-13:36:35Z; requirements status-quo 12 calls, full duration unmeasured. Architecture advocate 58 seconds/17 underlying calls; status-quo55 seconds/15 calls. Both reviewed the same pre-disposition spec/research/plan. Root verified RequestAdmission ID allocation, DispatchTermination reply behavior and framework response fields before deciding. No review claimed production qualification.

## Implementation review and correction

The frozen implementation candidate is archived externally under digest `8fa99d3573aefa71b2d830addc1e2da75d54c3f2bccd9d7a1d13da07cae824fb`. Separate contrary implementation reviews confirmed the prior request-link and terminal-serialization dispositions, then found two liveness defects: prior validation occurred before the advertised deadline, and pre-invocation cancellation disabled an unchanged continuation. Root also identified immediate pending-read exhaustion.

All three received executable semantic Red: three review regressions failed (two validator locations and cancelled continuation), and the pending-to-completed test failed with empty acceptance. Corrections: deadline established before preparation; asynchronous token-aware project validators execute under bounded waiting, including synchronous callback prefixes, with late faults observed and late acceptance rejected. Pure validators must still cooperate and never perform effects; timeout cannot physically kill host code. Cancellation before invocation preserves continuation policy; malformed state and Unknown still block. Pending observations now use a finite configured interval inside the round deadline. Actual-SQLite regression cancels during restore, then dispatches a later turn successfully on the same session. Targeted36-unit/four-runtime tests qualify these corrections; final native CI supersedes intermediate counts.

Implementation review accounting: advocate40 seconds/7 underlying calls; status-quo64 seconds/11 calls. No roles were combined; both inspected the same candidate and deterministic evidence. Follow-up review is restricted to these corrections and evidence.

## Correction re-review

Both contrary roles reviewed the narrow source diff against frozen candidates. Candidate-v2 digest: `7812e88e46ff999d2260a082af51f02fde3f4531b1341233ea13c2b2490e1974`. Advocate:11 seconds/6 underlying calls. Status-quo:13 seconds/7 calls. Both found no remaining blocker in changed areas and explicitly did not claim native/release qualification. Root verified the deadline-before-preparation ordering, bounded validation path, cancellation continuation flags and cancellable observation delay against the source and regression evidence. Resource consumption by uncooperative host callbacks remains a documented host boundary, not authority to accept late results.
