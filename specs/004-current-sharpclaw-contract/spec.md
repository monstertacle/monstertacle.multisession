# Feature Specification: Current SharpClaw consumption contract
Created: 2026-10-30T07:41:16Z
Owner: Alexander Bleissem
Status: in_progress

## User Scenarios & Testing
### US1 (P1): Current host alignment
A host integrator receives source-bound evidence for the current SharpClaw backend/session contract rather than relying on the old 7854a12 analysis.
Acceptance: main 8ca48622050721686a41c06a63d2735182ff3544 pinned, decisive files hashed and compared, native ownership/observation/cancellation boundaries documented.

### US2 (P1): Safe composed execution
A package-only consumer uses the current actual SharpClaw CLI execution transport in isolated workers while multiple library sessions run concurrently.
Acceptance: library/session/request/native identities stay bound; an unrelated parent child remains alive; first/follow-up requests and store reopen preserve identity; unsupported late-ID or ambiguous completion paths fail closed.

### Edge cases
Attempted session with missing ID; changed workspace/model; terminal event without confirmed process completion; unsupported inspect/cancel; callback acknowledgement; cancelled/failed worker; one session's process cleanup must not kill another session.

## Requirements
- FR-001: Library assemblies do not reference or copy SharpClaw.
- FR-002: Host session, reservation, fleet, prompt and tool policy stays host-owned.
- FR-003: ISessionProvider remains the boundary unless an executable failing contract demonstrates a missing generic capability.
- FR-004: Actual SharpClaw code participates in the consumer proof; a new arbitrary provider fake alone is insufficient.
- FR-005: Synchronous Observation callbacks are not presented as durable awaitable library acknowledgements.
- FR-006: Native process cleanup is confined to a dedicated worker, not the shared parent process.
- FR-007: Proof is offline and owned; no real provider call, deployed instance, state migration or credential access.

## Success Criteria
SC-001: Source/assembly/package digests identify the exact tested combination.
SC-002: Concurrent sessions and their followers preserve native identity without cross-kills or replay.
SC-003: Negative contract cases reject safely and identify boundaries rather than claim unsupported equivalence.
SC-004: Documentation distinguishes ABI/process-contract proof from live provider/product integration.

## Assumptions
The user authorized current-source refresh and extension here; no live-provider approvals or full SharpClaw migration are inferred.
