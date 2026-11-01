# Implementation Plan: Cross-Vendor Agent Multisession

**Branch**: `001-agent-multisession` (logical feature ID); initial repository branch `main`.
| **Date**: 2026-10-27 | **Spec**: [spec.md](spec.md)

**Input**: `specs/001-agent-multisession/spec.md`

**Current execution reconciliation 2026-10-30T00:02:08Z:** Runtime, both native
provider adapters, SQLite/recovery, both platform leases, diagnostics, ValidationHost
and package consumption are implemented; individual earlier status paragraphs below
describe the planning state at that time. T112–T115 have since been executed,
no NotImplemented scaffolds in the product code. Authoritative for current tasks
is tasks.md, for technical evidence evidence/acceptance-audit.md/build-native.json,
for missing acceptance evidence/release.md/remaining-acceptance.md. No product release:
missing Win11/live/integrator/historical Q06 evidence remains blocked.
T125/T126 are completed with native Red/Green evidence and test isolation;
current technical candidate fb1a167, full source hash and verification state
in evidence/delivery-candidate.md. The hours actually running are only assessed as
passed after they have ended.

**Status**: Phase 0 and Phase 1 documented; incremental implementation in progress,
no product release. Current partial evidence and limits: tasks.md as well as
evidence/tdd-foundation.md, tdd-us1.md, tdd-us2.md and coverage.md.
Re-checked after FR-027 and Constitution v2.0.0: generic backend boundary, mandatory
TDD, at least 87 % line coverage and official Codemetr guard. Approved are
tool version and default thresholds, no debt baseline. Q-01 has been checked natively on the frozen
candidate; missing further Q gates remain release conditions.

**Target host reconciliation**: R-13/R-14 and provider-contract.md §8 make concrete the actually
examined agent backend/session contract: explicit capabilities/ExecutionKind,
workspace snapshot, awaitable observations for opaque native session/operation ID,
terminal reply and separate certainty. T010/T035/T089 must fulfill these contract cases
before Green. The design reconciliation is complete; actual target host/
package consumption remains open. No change to the target host code or migration of existing state.
OpenCode **1.19.0** remains the fixed reference by explicit user decision;
no upgrade, no newer V2 API and no mandatory upstream OpenCode runtime.
Update 2026-10-28: R-15 checks SharpClaw commit `7854a12` against the original
analysis commit. The relevant backend/session boundary is unchanged; new context cache,
output and fleet services remain in the host. Deep dive and test plan before request admission:
`evidence/architecture/2026-10-28-sharpclaw-refresh-request-admission.md`.

## Summary

Reusable C#/NuGet libraries manage parallel provider sessions with
separate identity, durably accepted FIFO, finite limits and conservative
recovery. Context compaction, tools and agent loops remain entirely
with the provider. An instance belongs to one trust domain; the host controls access.

A Common core contains the domain logic. Separate Windows/Linux packages take over
storage permissions and exclusive ownership. SQLite persistence and two native HTTP providers
are separate packages. A later console/service/daemon host provides lifecycle,
configuration and request intake; this plan does not implement a service.
SharpClaw is explicitly intended as a long-term consuming host. The
dependency direction remains `SharpClaw -> NuGet libraries`, never the reverse.
Its actual product migration or state adoption is a separate later delivery.

Reference connections are OpenAI Conversations/Responses and Gemini Interactions `/v1`.
Native session references are continued; no chat histories are re-fed to simulate a
session. Technical decisions and sources: [research.md](research.md).

## Technical Context

**Language/Version**: C# 14, .NET 10 LTS, SDK 10.0.403 exactly pinned; `net10.0`,
Windows platform package `net10.0-windows`; no prereleases or implicit SDK upgrades.

**Scripting/tooling boundary**: In accordance with Constitution v2.0.0, Python is prohibited for product code,
tests, build/CI scripts, code generation and agent-assisted project work,
including temporary helper scripts and one-off agent commands. Use C#/.NET and, if
needed, PowerShell or shell scripts; do not introduce a Python runtime as a
project prerequisite. Explicitly approved by the user on 2026-10-28.

**Primary Dependencies**: Microsoft.Data.Sqlite 10.0.14;
Microsoft.Extensions.Logging.Abstractions 10.0.14; BCL HttpClient, System.Text.Json,
Channels, TimeProvider and Diagnostics. No agent framework, no mandatory
hosting stack and no automatic POST retries. Metadata/licenses see R-08.

**Storage**: One local SQLite database per trust domain; exclusive owner;
DELETE journal with synchronous=EXTRA and foreign keys. Single bounded
DB work channel; no SQL transaction across external calls. Credentials only via host resolver.

**Testing**: xUnit 2.9.3, xunit.runner.visualstudio 3.1.5, Microsoft.NET.Test.Sdk 18.11.0;
deterministic provider fakes, HTTP contract fixtures, real process kill/restart tests,
platform permission/lock tests, separate explicit live provider acceptance.
For each new/changed behavior requirement, first observe an executable functionally red test,
then a minimal implementation, followed by refactoring with green tests.
At least 87 % line coverage of all own production code as a CI gate on
Windows and Linux. Measure the shared libraries and the respective native platform package
completely, including assemblies not loaded by tests. The other
platform library is to be explicitly assigned to the other native run in the scope inventory;
a product project MUST NOT be missing from both runs. Merge reports of multiple test suites per
platform line by line, do not average percentages. Test code does not count toward the
coverage denominator; exclude generated code/third-party code only with justification.
Collector, version, runsettings, aggregation and gate implementation are defined and pinned in T005/T015;
missing/empty reports or less than 87 % block the release.
The project owner explicitly approved the change from 90 to 87 % on 2026-10-28;
change evidence `evidence/architecture/2026-10-28-coverage-87.md`.

**Quality Guard**: `Codemetr.CSharp.Tool` 0.1.0 pinned locally, approved default rules
in `codemetr.json`, no debt baseline. Full check from the root including
tests/ValidationHost: `dotnet tool run codemetr-csharp -- check --format json`.
Project-local OpenCode skill `codemetr-guard` and `.github/workflows/codemetr-guard.yml`
are set up; real CI runs still require a connected Git repository.

**Target Platform**: Windows 11 25H2 x64, Windows Server 2025 x64 and Ubuntu 24.04 LTS x64.
Framework-dependent hosts with .NET 10 runtime. Other architectures/distributions,
Windows service installation and Linux service units are not part of the first delivery.

**Project Type**: Six NuGet libraries and one non-productive consumption/acceptance host.
No web endpoints, user interface, CLI product or operational installation.

**Performance Goals**: SC-001–SC-010 apply unchanged: ten sessions, correct
assignment for 100 requests, demonstrably overlapping independent work, local
cancellation observation within two seconds with cooperative providers, ten seconds
shutdown period in the corresponding test and one hour of continuous acceptance per target system.
No guarantee of vendor-side response latency or unlimited context length.
SC-011 adds an embedded consumption proof without changes to the library source code
or an additional multisession process as a prerequisite for the long-term target host.

**Constraints**: No agent/tool loop of its own, no context replay, no compaction,
no silent substitute provider; persisted uncertain work is not automatically repeated.
No network file system, cluster, untrusted plugin sandbox or internal tenant model.
The package structure is in place; the overview below also describes still planned parts.
Existing scaffolds/partial implementations are not completed product functions.

**Scale/Scope**: One active logical host per store. Initial values: MaxConcurrentRequests=4,
MaxQueuedRequests=100, MaxInputBytes=1 MiB, MaxOutputBytes=4 MiB, RequestTimeout=600 s,
ShutdownTimeout=10 s and DbWorkQueueCapacity=256. Positive finite values required;
SQLite busy timeout=1 s instead of the long library default.
AdmissionTimeout=5 s limits local admission; requests already stored as Queued
have no automatic waiting deadline. Request and stop deadlines are measured monotonically.
Larger values are not considered load-tested solely by configuration.
The host must explicitly configure retention or choose RetainUntilExplicitDeletion.

## Constitution Check

*GATE: check before Phase 0 and re-evaluate after Phase 1. Design conformance is
not the same statement as fulfilled code/product release.*

Renewed review run: Constitution v2.0.0 and FR-027 are the current basis.
Authoritative is the mandatory generic
agent/provider boundary. The contract gap identified in R-13 was closed in the design by
provider-contract.md §8; executable evidence remains open.
The existing technical decisions were re-evaluated against TDD; no new
technology selection or extension to an agent runtime of its own is required.

| Principle / requirement | Before Phase 0 | After Phase 1 | Evidence in the design |
| --- | --- | --- | --- |
| I: C#/.NET, no Python, analyzers, nullable, async/await | PASS: stack fits, concrete selection in research | PASS (design); no claimed implementation evidence | SDK/package pins; scripting/tooling boundary; cancellation and finite deadlines in the contract |
| II: SOLID, DRY, KISS, YAGNI, generic backends | Core without vendor dependency planned | PASS (design): agent/target host contract explicitly modeled, execution open | Common without back-dependency; provider-contract.md §8; T010/T035/T089 |
| III: Mandatory check with Codemetr | PASS for research without code; release reserved | Tool/rules approved; native technical check on the frozen candidate PASS, remaining release gates open | R-09, evidence/codemetr-approval.md, evidence/codemetr-results.md and CI guard |
| IV: TDD, ≥87 % line coverage, Windows/Linux, CI | PASS (design): Red before behavior and coverage gate planned | PASS (design); actual partial evidence see evidence/coverage.md, no product release | TDD strategy, T005/T015/T104, quickstart.md and Q-06 |
| V: Security and diagnostics | PASS: one trust domain, host auth | PASS (design) | Private store permissions, no secrets in state/log, audit gate |
| Culture, time, paths, supported platforms | PASS: no conflicting requirement | PASS (design) | UTC, ordinal IDs, explicit matrix, platform adapters |
| Documentation and change control | PASS | PASS (design) | Research decisions with alternatives, versioned contracts |

The unknown ratification date remains unchanged as a governance TODO in the constitution.
No date is invented. The Codemetr tool and default thresholds were explicitly approved by the
project owner; evidence in `evidence/codemetr-approval.md`.
This is not an already passed check of the product code still to be implemented.

The project owner is responsible for the governance decisions:
he confirms the original ratification date, as far as it can be determined, before the final
adoption of the constitution documentation. Until then, the existing TODO remains visible.
He has confirmed CLI 0.1.0 with default rules without a debt baseline. On 2026-10-28,
SDK 10.0.403 was explicitly approved, since 10.0.402 is no longer installed locally.
Constitution v1.6.0 reflects this decision; no exception and no date are invented.
Known documentation TODOs are separate from the product release.

Q-01 is demonstrated for the frozen technical candidate on both native CI systems:
442 files/459 types, no rule violations; semantic measurement limits
in evidence/codemetr-results.md. This does not replace the remaining product release.
Every further extension requires its current analysis of existing code and new checks.

## Project Structure

### Documentation (this feature)

```text
specs/001-agent-multisession/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── public-api.md
│   ├── provider-contract.md
│   └── orchestration-details.md
└── checklists/
    ├── requirements.md
    └── multisession.md
```

`tasks.md` exists and steers the incremental implementation including the
additions T111–T115 from the analysis cleanup of 2026-10-28.

### Source Code (repository root)

The following structure is implemented. Individual still missing acceptances and external
prerequisites remain open in tasks.md; the overview does not replace their evidence:

```text
Monstertacle.Multisession.slnx
global.json
Directory.Build.props
Directory.Packages.props
src/
├── Monstertacle.Multisession.Common/
│   ├── Sessions/
│   ├── Requests/
│   ├── Scheduling/
│   ├── Recovery/
│   └── Contracts/
├── Monstertacle.Multisession.Storage.Sqlite/
├── Monstertacle.Multisession.Windows/
├── Monstertacle.Multisession.Linux/
├── Monstertacle.Multisession.Providers.OpenAI/
└── Monstertacle.Multisession.Providers.Gemini/
tests/
├── Monstertacle.Multisession.UnitTests/
├── Monstertacle.Multisession.ContractTests/
├── Monstertacle.Multisession.IntegrationTests/
└── Monstertacle.Multisession.PlatformTests/
examples/
└── Monstertacle.Multisession.ValidationHost/
```

**Structure Decision**: Platform libraries, storage and providers reference Common;
Common references no concrete implementation. The consuming host explicitly wires
the required platform package, storage and the selected providers.
No empty Linux/Windows packages and no DI/reflection framework are created.
The shared library is testable with fakes without SQLite or platform code.
Test projects and ValidationHost get IsPackable=false; only the six libraries
are packed as product NuGet packages.

**Shared flow**:

1. Host supplies options, provider registrations, CredentialResolver, storage and the
   state directory/ownership implementation matching the platform.
2. Start takes exclusive store ownership, checks schema and recovery and starts bounded
   work channels. No HTTP listener is opened implicitly.
3. Admission commits input, session sequence and queue limit check atomically, then confirms.
4. Scheduler selects FIFO heads of unlocked sessions; same session and same writing
    workspace are serialized. Oldest admissible head is preferred.
    With equal AcceptedUtc, RequestId decides ordinally. Blocked heads do not prevent
    independent admissible work; a fixed global waiting time is not guaranteed.
5. Running/reservation is persisted before external handover. Provider receives only new
    input plus its last confirmed native continuation state.
6. Result and new binding state commit together; safe reservation is released.
7. In case of uncertain outcome, session/workspace and execution slot remain reserved.
   Independent sessions may use free remaining slots; occupied capacity is not invented.

Closing is a local lifecycle decision, not an implicit deletion of provider data.
On stop, no new jobs are accepted. Within the deadline, open work is
completed; remaining waiting requests are explicitly ended with Cancelled/NotStarted.
Running requests are stopped in a controlled way or saved as uncertain.
After an unplanned restart, by contrast, the requests that remained Queued are continued.
No accepted job disappears without a state. Details: [data-model.md](data-model.md).
If all slots are reserved by uncertain work, further work remains waiting;
error isolation is no permission to bypass the execution limit or workspace locks.

No automatic migration is planned for the first storage/binding format.
An incompatible format is rejected before changes; a missing expected store
is not re-initialized. Later format changes require their own
migration/rollback plan. No insufficiently specified updater is implemented here.

## Concretized Orchestration Contracts and Implementation Prerequisites

Binding details: [contracts/orchestration-details.md](contracts/orchestration-details.md).
ReservedDispatch including query and dispatcher as well as recovery/admission/paging changes
T112–T115 are implemented following the chronological Red/Green cycles.
Evidence in evidence/tdd-us1.md, tdd-us3.md and tdd-us5.md. T009/T010 still denote
only their original signature scope; the later implementation acceptance
is based on the actual supplementary tests and current native overall checks.

| Finding | Architecture decision | Prerequisite before Green |
| --- | --- | --- |
| U1 | ReservedDispatch is stored atomically with the reservation; GetReservedDispatchAsync returns input/timeout/session/BindingRevision without reconstruction; completion checks the revision | T112 before T025 and T051/T059/T060 |
| U2/U3 | RecoveryTarget for SessionOpening or RequestExecution; publicly readable RecoveryCaseSnapshot with persisted CAS revision; no dummy requests | T113 before T052/T062 and the relevant part of T037/T079 |
| U4 | Pre-generated candidate identifier in RequestAcceptance; AdmissionStatus separate from external certainty, confirmed commit returns receipt | T114 before T058/T076 |
| A2 | Shared page limits and range-bound keyset cursors; HistoryOrdinal/HistoryId and separate recovery case snapshots | T115 before paging parts T026/T037/T063 |

The dependencies named in the table were observed in the respective cycles.
Runtime vertical, paging and revisioned recovery are now
fully within the technical verification scope. Individual green partial slices do not close any
missing live/target system/integrator acceptance; this remains concretely assigned in tasks.md and
evidence/remaining-acceptance.md. Native SQLite/provider/recovery evidence
is linked in the current evidence/build-native.json and the TDD records.

T015 comprises local workflow/runner configuration and synthetic gate behavior tests.
Real Windows/Linux CI and required branch checks are T111 and T104: mandatory
**release gates**, but no prerequisite for local test-driven further development.
The approved repository connection exists; native quality, guard, audit and six
required branch checks are actually demonstrated. The pending live/
target system/integrator approvals remain independent acceptance prerequisites.

## TDD Implementation Strategy

**Prerequisite before every extension**: First examine the current existing code via
code deep dive and architecture analysis according to Constitution §II, then define the concrete
extension design and its Red–Green–Refactor cycles. Examine affected modules,
relevant callers/dependencies, responsibilities, contracts, data/control flows,
states/persistence, existing tests, reuse and regression risks.
Keep the evidence with source revision/source hash, file/symbol references, findings, open questions,
architectural consequences, chosen extension point and test plan under
`specs/001-agent-multisession/evidence/architecture/<change>.md`; replace `<change>`
with a unique change identifier. Without evidence or with unresolved
implementation-relevant architecture questions, do not begin any implementation. Old
analyses must be re-checked against the current code and scope; small extensions
are not exempt. Explicitly document missing existing code, but still examine existing
scaffolds/contracts. This requirement is not yet an executed deep dive.

Each row describes a test-driven vertical implementation unit, not a request
to first fully implement all libraries and add tests afterwards.
Each unit is further broken down into small Red–Green–Refactor cycles. Before starting,
only minimal test/project scaffolds and behaviorless signatures are permissible.
Missing SDK, unrestored packages, compiler errors or zero discovered tests
never count as expected functional Red evidence.

| Unit / reference | Executable tests first | Expected functional Red reason | Minimal Green, then refactor |
| --- | --- | --- | --- |
| TDD-01 Sessions and assignment; FR-003–FR-007, SC-002 | SessionRoutingTests: identifiers, fixed assignment, separate contexts, follow-up request, invalid/closed session | New session cannot be found, wrong assignment or impermissible send is not prevented | Only session catalog and routing against controlled store/provider fakes; then split responsibilities |
| TDD-02 FIFO, parallelism and workspaces; FR-002, FR-008, FR-016, SC-001/SC-005 | SchedulingTests: paused fakes, same session serialized, independent ones overlap, limits 2/3, shared workspace | Second impermissible send starts, permissible independent work remains blocked or queue exceeds limit | Implement only bounded admission and the durable ordering rule; no message broker |
| TDD-03 Durable acceptance and recovery; FR-018–FR-021, SC-009 | PersistenceRecoveryTests: real temporary store, process kill before/after commit, lost wake-up signal, uncertain dispatch, missing store | Confirmed request missing after reopening, uncertain request is sent again or wrong reservation released | Add transactions and persisted dispatch boundaries one at a time; no network call under DB lock |
| TDD-04 Errors, lifecycle and diagnostics; FR-009–FR-013, FR-015, FR-026, SC-003/SC-004 | LifecycleTests: foreign provider error, waiting cancel, local timeout with Unknown, stop deadline, instance separation, sanitized diagnostics | Independent request ends wrongly, cancel triggers send, unconfirmed end counts as safe or data gets mixed | Add only state transitions and safe observation; do not presuppose production accesses |
| TDD-05 native provider contracts; FR-003/FR-022/FR-024/FR-025, SC-008/SC-010 | ProviderContractTests: controlled HTTP responses, native IDs/chain head, only new message, requires_action, context limit, Inspect/Cancel | Wrong reference/body, history is replayed, context modified by itself or client-side action treated as success | Per adapter implement only the documented native contract; real vendor acceptance additionally after deterministic Green |
| TDD-06 Platform ownership and permissions; FR-014/FR-023, SC-007 | PlatformOwnershipTests: real target systems, second process, path alias, private permissions, wrong platform, broken store | Second owner is admitted, wrong permissions accepted or unsupported platform silently used | Per platform fulfill only the shared ownership/permission contract; do not duplicate session logic |
| TDD-07 embedded consumer; FR-001/FR-017/FR-027, SC-006/SC-011 | EmbeddedConsumerTests: separate consuming test project, own provider adapter, two sessions, follow-up requests, status, cancellation and stop | Public contract does not route the consumption flow correctly or requires internal special knowledge | Add only missing public behavior; later a real package smoke test without source references and integrator timing evidence |

All remaining acceptance and negative cases of the specification are likewise tested within the matching
unit before the respective behavior change. In particular, retention,
schema/storage errors and data protection are not downstream optional test tasks.
SC-007 continuous operation, live providers and actual NuGet package consumption supplement the
deterministic Red/Green steps but do not replace them.

The work record stores per cycle the FR/SC reference, test identifier and command, checked
source revision/hash, observed functional Red reason as well as Green and regression result.
Without Git, a documented source hash is permissible; a non-existent commit ID is
not invented. Test logs generated after the fact MUST NOT feign an earlier test execution.

Q-06 blocks the release if a new behavior path was only tested green after the fact
or Red is claimed only through infrastructure errors. Pure refactoring starts
with a green suite and ends with a green suite plus a renewed Codemetr check; no artificial
error and no new functional feature solely to satisfy the TDD template.

The existing task list models Test/Red before Implementation/Green and Refactor.
New supplementary cycles receive the same dependencies and current upfront analyses.

## Complexity Tracking

No constitution deviation requiring approval. The deliberate decisions:

| Decision | Current need | Why the simpler alternative is not sufficient |
| --- | --- | --- |
| Common plus two platform packages | Private file permissions and exclusive storage ownership on both target systems | Linux-only mechanics violate portability; platform code in the core impedes independent tests |
| Separate storage package | Transactional admission, FIFO and crash states | In-memory queue or multiple JSON files do not meet the durability boundary |
| Two separate provider packages | Two real different native session contracts and independent consumer choice | A universal chat adapter would feign native session semantics or rebuild its own context |
| Persisted dispatch/fence | Uncertain external effect after crash | Automatic replay can trigger the same work twice |

Not added: own agent loop, generic tool executor, token/context service,
SSH/remote gateway, process supervisor, productive service host or additional message broker.

## Phase 0 — Research

Completed and re-evaluated as documentation work: R-01–R-12 in [research.md](research.md) resolve
stack, package boundaries, provider semantics, storage, ownership and validation strategy.
Verified facts and evidence still to be produced are kept separate there.

## Phase 1 — Design & Contracts

- [data-model.md](data-model.md): identities, states, reservations, commit and crash boundaries.
- [contracts/public-api.md](contracts/public-api.md): public library operations,
  host obligations, errors and lifecycle.
- [contracts/provider-contract.md](contracts/provider-contract.md): native session adapters,
  concrete reference protocols and their limits.
- [contracts/orchestration-details.md](contracts/orchestration-details.md): dispatch data,
  recovery targets/revisions, admission certainty as well as list/cursor limits (T112–T115).
- [quickstart.md](quickstart.md): later executable evidence including SC assignment.

Post-design check under Constitution v2.0.0 see constitution table; agent contract draft reconciled, full behavior acceptance open.
Q-01 actual Codemetr product check (tool/rules approved), Q-02 build/audit/coverage, Q-03 platform/crash behavior,
Q-04 live providers, Q-05 package consumption and Q-06 actual Red–Green–Refactor evidence are open before code release
on the final delivery state. Local partial checks are documented in the current evidence;
this plan claims neither a completed story nor product release.

Addition FR-027: Q-05 also comprises the host-like consumption proof from SC-011 with
a provider implementation registered by the consumer. This is no intervention
in an external application repository and no guarantee of complete migration of existing state.
