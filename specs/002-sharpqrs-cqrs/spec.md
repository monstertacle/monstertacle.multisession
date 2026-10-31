# Feature Specification: CQRS runtime access
Branch: feature/sharpqrs-cqrs
Created: 2026-10-30T01:21:40Z
Status: Implemented

## User Scenarios & Testing
### US1 (P1): Dispatch typed runtime operations
A host dispatches session, request and recovery commands and queries with the same durable results and lifecycle protections as existing ports.
Acceptance: all fourteen operations are registered; missing lookups return an explicit empty result; cancellation and errors remain observable; no duplicate admission or provider execution.

### US2 (P1): Preserve existing integrations
Existing ISessions/IRequests/IRecovery consumers continue to compile and use the same CQRS route internally.
Acceptance: existing contracts and persistence tests pass; separate runtime instances retain isolated mappings and state; callers cannot mutate runtime registrations.

### US3 (P1): Enforce coverage
Contributors and native Linux/Windows CI use Codemetr Cover and retain the existing whole-product 87% gate.
Acceptance: pinned repository-local tool, trustworthy complete coverage gate with all test projects, no baseline or weakened rules, package-only consumer resolves the CQRS dependency.

## Requirements
- FR-001: Expose separate asynchronous command and query dispatch on a started runtime.
- FR-002: Route the six mutation operations and eight observation operations through exact typed registrations.
- FR-003: Preserve lifecycle ownership, cancellation, diagnostics, pagination, revision checks and uncertainty semantics.
- FR-004: Treat cancellation/close acknowledgements as accepted local intent, never proof of remote termination.
- FR-005: Keep null lookup absence distinguishable without returning a null dispatcher result.
- FR-006: Pin the explicitly requested SharpQrs version 0.2.0-alpha.1.
- FR-007: Use Codemetr Cover in automated quality checks alongside the existing native whole-product coverage gate.
- FR-008: Keep the six existing library packages, store schema and provider protocols compatible.
- FR-009: Label the six unpublished own packages 0.2.0-alpha.1 to permit the explicitly required prerelease dependency without suppressing NuGet validation.

## Success Criteria
All fourteen typed routes and existing contracts pass; package-only consumption works; full static metrics pass; each native CI coverage gate measures and enforces at least 87%.
External live-provider, Windows 11 and independent-integrator acceptance remain tracked in feature 001 and are not claimed by this extension.

