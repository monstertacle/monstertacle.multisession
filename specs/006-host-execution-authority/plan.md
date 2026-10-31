# Plan: Host-directed execution authority

Status: in_progress
Owner: primary integration agent

## Technical context

C#14, SDK10.0.403, .NET10, Ubuntu24.04 x64 and Windows x64. Existing Common/SQLite/Linux/Windows/AgentFramework packages; Microsoft Agent Framework1.27.0. No new dependency or tool policy. Version0.2.0-alpha.3, store format2, no active-store migration.

## Constitution gate

Current sources and both callers/owners inspected; joint contrary requirements and architecture review dispositions in research.md. Canonical caller IDs, persisted controlled dispatch, typed outcomes and separate observations resolve concrete host contract holes. TDD semantic Red before behavior, async/cancellation, native87-percent coverage and complete static guard required. No Python, threshold weakening, baseline or compatibility shims.

## Boundaries

Common owns admission/dispatch/certainty; SQLite atomic identity/reservation and persisted mode; framework package owns qualified CLR result and invocation observer mapping; native libraries own ownership; host owns work admission/acceptance. No state authority is duplicated in the host.

## Milestones

1. Behaviorless signatures and semantic Red contract tests.
2. Canonical ID and explicit targeted lifecycle implementation, format2 and observation evidence.
3. Typed framework outcome/observer implementation and meaningful regression coverage.
4. Full local gates/native CI, actual alpha.2 reader refusal, alpha.3 local feed and SharpClaw consumption.

## Findings and coordination

All new P1 contract gaps block host runtime migration until tests pass. Historical format1 remains intact and refuses; no silently recreated store. The concurrent owner session operates live thesis workers; this work changes only isolated source/fixtures/local feeds.

External dependency boundary: Microsoft Agent Framework and its Durable extension are upstream repositories used read-only. All changes are in Monstertacle/SharpClaw through supported public extension points. The owner does not need to own or fork the external framework.

## Progress

- 2026-10-31T15:39:46Z: identity, immutable dispatch mode, controlled startup/shutdown and atomic target-head behavior are implemented on the isolated branch. Observed semantic Reds are retained in evidence/. Canonical session/request IDs pass through the existing facades and CQRS routes; generated-ID compatibility methods were removed and own callers updated. Duplicate session IDs reject inside the SQLite transaction before external open. Full local qualification is running. This is not completion of typed outcomes, native observations, producer release or SharpClaw adoption.

- 2026-10-31T15:49:58Z: branch feature/host-directed-execution owns continuation. Increment targets alpha.3 and format2. Full Linux quality passed with 1140 tests, no skips, product line coverage97.8717percent; Codemetr measured542types/522files with zero violations and skipped types. Framework reference clones remain clean. evidence/qualification.json binds the exact source hash. Producer release remains blocked on the named pending contracts and native/package qualification; SharpClaw feature004 owns actual host consumption.
