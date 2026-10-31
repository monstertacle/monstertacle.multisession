# Implementation Plan: Agent Framework migration

**Branch**: `feature/agent-framework-migration`
**Status**: in_progress: implementation complete; native/package qualification pending
**Owner**: primary integration agent
**Spec**: [spec.md](spec.md)

## Summary

Use Microsoft Agent Framework for host-supplied agent execution and evidence renewal workflows. Keep existing durable CQRS/SQLite/native ownership and Unknown recovery. Rename existing vendor-named implementation types/packages after protocol responsibilities, preserving technical wire IDs and documenting package/type breaks.

## Technical Context

C#14 / SDK10.0.403 / .NET10; Linux Ubuntu24.04 x64 and Windows x64. NuGet library package, Microsoft.Agents.AI and Workflows1.27.0 (MIT); no model service dependency. Existing SQLite10.0.14 and SharpQrs0.2alpha1 remain. Storage schema unchanged. xUnit2/Coverlet/Codemetr existing pins retained. Finite caller-visible execution/renewal deadlines, bounded serialized state and callback attempts. Native platform libraries continue owning process/volume checks.

## Constitution Check

Pre-design: source analysis completed in research.md; framework owns agent/tool loop; no own substitute loop. Project-owned graph validators retain domain authority. TDD, full static gate, complete native-platform87-percent coverage, locked restore/format/analyzers/audit and package-only consumers required. No Python. No tool/gate weakening. New public names are provider-neutral. Post-design contrary review completed and both blockers resolved before implementation; dispositions in research.md.

## Project Structure

- `src/Monstertacle.Multisession.AgentFramework/`: provider, versioned continuation validation, execution/response policy, evidence graph/workflow contracts.
- Existing protocol directories rename to `Providers.Conversations` and `Providers.Interactions`; update references/tests/package smoke and complete coverage inventory.
- `tests/Monstertacle.Multisession.UnitTests/`: deterministic agent and workflow tests.
- `tests/Monstertacle.Multisession.IntegrationTests/`: actual durable-runtime continuation and uncertain-effect tests.
- `specs/005-agent-framework-migration/`: requirements, source analysis, contracts, reviews, measured TDD/gate evidence and release disposition.

## Milestones

1. Complete contrary requirements and architecture reviews and dependency approval evidence.
2. Compile behaviorless signatures and deterministic tests; observe semantic Red; implement adapter and renewal Green.
3. Rename protocol implementations, update example and package consumer to generic agent path, include new module in coverage denominator.
4. Full local/native CI and package consumer; resolve findings; PR and merge only exact-source green; never reset live sessions.

## Risk and findings ownership

Primary integration agent owns all new gaps. Unknown-effect/custody, stale acceptance and missing full coverage are release blockers. No automatic fallback to old adapter or checkpoint replay. Historical session IDs/wire bindings remain intact; no public namespace compatibility shim. The existing native-capacity follow-up remains owned by `../003-native-reliability/follow-up-capacity-probe.md` and is not claimed fixed here.

## Findings disposition

- Admission/link crash window: resolved by read-only renewal of preassociated request references; missing associations coordinate without new admission. Tests cover restart/Unknown/missing association.
- Terminal serialization/cancellation certainty: resolved by unchanged binding plus disabled continuation; actual SQLite/reopen regression passes.
- Pre-invocation cancellation liveness: resolved by preserving continuation policy; cancelled restore followed by successful later turn passes.
- Validator deadline and late acceptance: resolved by async token-aware bounded validation before/after preparation; previous/fresh blocked-callback regressions pass.
- Immediate pending observation exhaustion: resolved by finite cancellable interval; pending-to-completed regression passes.
- New package source mapping: Google.Protobuf added explicitly; isolated locked package consumer passes.
- Coverage diagnostic warning: concrete owned follow-up in [follow-up-coverage-diagnostic.md](follow-up-coverage-diagnostic.md). Complete gate and denominator remain required.
- Existing native-capacity follow-up: retained in its original plan, not claimed resolved by framework migration.

## Local qualification

Full native Linux:402 unit,252 contract,412 integration and68 platform tests; zero skipped. Product coverage97.8364 percent; complete Codemetr Cover including ValidationHost95.8 percent with zero violations. Static guard measures540 types/520 files with zero violations/skips. Isolated package consumers restore locked dependencies and execute both original public runtime flow and two generic framework turns. An archival-only correction keeps generated consumer sources as text and isolates its metrics context; final CI must qualify that release source on both native platforms. Public package publication and live-host activation are not inferred from these library checks.
