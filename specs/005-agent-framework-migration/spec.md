# Feature Specification: Provider-neutral framework migration

**Feature Branch**: `feature/agent-framework-migration`
**Status**: Implemented; qualification in progress
**Input**: Use Microsoft Agent Framework in C# and migrate autonomously using SpecKit; retain provider-neutral names and contracts.

## User Scenarios & Testing

### User Story 1 - Run a host-selected agent (Priority: P1)

A host registers an existing agent without a vendor-specific integration class. New sessions, new turns and resumed sessions use that same agent contract. The host continues using typed commands and queries and the existing durable request identities.

**Independent Test**: Run two turns through the actual session runtime with a deterministic agent, recreate the adapter/runtime, then continue the same session without replaying prior inputs.

**Acceptance Scenarios**:
1. Given a registered agent, a fresh durable dispatch invokes the public agent run entry point at most once; a successful dispatch invokes that entry point exactly once (internal model/tool calls remain agent-owned) and returns the final complete output.
2. Given a confirmed session, recreating the adapter restores its confirmed state without creating a replacement session or resending history as new input.
3. Given incompatible or missing continuation state, resume fails before invoking the agent.
4. Given an acknowledged durable observation, execution may begin; a failed observation cannot dispatch external work.

### User Story 2 - Keep uncertain execution blocked (Priority: P1)

A host must never turn a timeout, lost result or process restart into permission to repeat an uncertain effect.

**Independent Test**: Fail after agent invocation, cancel during execution, exceed output limits and reopen the store; assert certainty, session blocking and reservation behavior using the existing recovery APIs.

**Acceptance Scenarios**:
1. An exception after invocation begins without terminal evidence produces an uncertain result with sanitized diagnostics and blocks continuation.
2. Unsupported remote inspection/cancellation is declared and rejected; local cancellation is never represented as proof that a remote effect ended.
3. A confirmed completed result followed by serialization failure preserves known termination but blocks continuation; it cannot silently continue from old state.
4. Truncation, content filtering, pending approvals/tool calls and unsupported nontext output cannot become successful complete text merely because a text property exists.
5. No workflow checkpoint overrides durable execution certainty or releases a workspace reservation.

### User Story 3 - Renew evidence against one revision (Priority: P1)

A project supplies versioned source, rule, verdict and artifact dependencies and project-owned validators. A bounded workflow renews the affected evidence and reports acceptance progress separately from activity.

**Independent Test**: Change a source revision, a rule revision and an output-only report separately; execute deterministic renewal steps without credentials or model calls.

**Acceptance Scenarios**:
1. A mechanical relocation invalidates dependent generated bindings, while changed content requires a new scoped verdict and changed binding rules require an explicit authorized decision.
2. A verdict for a different dependency revision cannot satisfy acceptance; independent evidence remains valid.
3. Log, status and checkpoint changes alone do not count as verified progress or reset the repeated-blocker budget.
4. Repeated unchanged blockers reach an explicit coordination result within the configured finite attempt budget; no unbounded internal retry.
5. All inputs and accepted results in a renewal round refer to the same frozen revision. Caller-owned persistence records accepted results before starting a later round; restart cannot claim an uncommitted result as accepted.

6. Cycles, duplicate IDs and missing dependencies are rejected before renewal callbacks; stale, duplicate, late or validator-failed receipts cannot advance acceptance.
7. Effectful renewal uses existing durable admission/dispatch/recovery and retains the same request identity after restart; an unresolved request is inspected or escalated, never automatically reissued.

### User Story 4 - Consume the migration as packages (Priority: P2)

A host consumes published-package-shaped artifacts with protocol-based names. Existing provider implementations have protocol-based names, and examples prefer the generic framework integration. The migration is explicit about changed package/type names and does not reset existing persisted sessions.

**Independent Test**: Pack and restore a clean external consumer using only package references; verify provider-neutral composition and document incompatible historical binding IDs without rewriting state.

### Edge Cases

Cancellation before invocation; cancellation after invocation; serializer failure after success; malformed/mismatched binding; partial/oversized output; late or duplicate evidence; dependency cycles; missing dependencies; failed validators; unchanged blockers; unknown external effect; process death between effect and durable completion.

## Requirements

- **FR-001**: Execution is composed from a host-selected agent. No vendor-name dispatch rules, hardcoded model, credential resolver or provider-specific class names in the new integration.
- **FR-002**: Durable requests, sessions, native ownership and typed command/query APIs remain authoritative; framework state cannot synthesize receipts or execution finality.
- **FR-003**: Confirmed continuation is serialized/restored through the agent contract and bound to the same adapter identity and binding format. Raw prompts, state and exceptions are not copied to diagnostics.
- **FR-004**: All execution and renewal calls propagate cancellation and have finite, explicit budgets. Uncertain effects are never automatically replayed.
- **FR-005**: Capability declarations reflect actual generic support; absent operation inspection/cancellation cannot be fabricated.
- **FR-006**: Evidence dependencies and validation rules belong to the caller project. The orchestration library supplies no thesis/style/domain rules and cannot approve its own judgments.
- **FR-007**: Evidence acceptance uses explicit dependency revisions and validators; activity alone cannot advance acceptance. Accepted count may fall when dependencies change.
- **FR-008**: Successful renewal and unchanged-blocker coordination are observable results from an actual framework workflow, not a standalone imitation.
- **FR-009**: Package/type renames have a migration table; no automatic conversion of active native sessions, credentials or provider bindings.
- **FR-010**: Requirements and architecture receive separate contrary reviews. Tests follow observed behavior-red before green; complete own production coverage remains at least 87 percent per native platform, including the new integration.

### Key Entities

- Agent registration: host-provided agent factory and explicit adapter identity.
- Confirmed continuation: agent-owned serialized session state with stable local session binding.
- Evidence definition: ID, kind, revision and dependency IDs.
- Evidence result: evidence ID, exact dependency-revision fingerprint, validator-approved artifact reference, blocker identity.
- Renewal round: frozen graph, explicit bounded attempts and caller-persisted accepted state.

## Success Criteria

- **SC-001**: Two turns and a recreated-host continuation succeed without duplicate input or replacement identity.
- **SC-002**: Every tested uncertain invocation remains blocked until explicit recovery; zero automatic resubmissions.
- **SC-003**: Source/rule changes invalidate all and only their dependent evidence; zero stale verdicts accepted.
- **SC-004**: Status-only changes produce zero verified progress; unchanged blockers terminate within the declared budget.
- **SC-005**: Package-only consumer, native-platform CI, static metrics and overall 87-percent coverage pass for the exact release source.

## Assumptions

- The agent/framework owns model calls, tools and conversation state. The library orchestrates and does not build a substitute agent loop.
- Source access and requirements establish the framework migration; live credentials and paid model calls are unnecessary for deterministic contract qualification.
- Existing native durable stores are retained. Migration of a live host is a separate qualified host update, never a session reset.
