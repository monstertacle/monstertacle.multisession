# Research: Agent Multisession

**Date**: 2026-10-27 | **Basis**: [spec.md](spec.md)

The following decisions resolve the technical selection questions of Phase 0.
Official documentation, release metadata and package metadata were read.
In the original research, no packages were installed, no builds or product/vendor tests were executed, no credentials
were read and no paid provider calls were made. Documentation is not runtime evidence.

## R-01 — Runtime and Platform Matrix

**Decision**: C# 14, .NET 10 LTS, `net10.0`; pin SDK 10.0.403 exactly in the
`global.json` with `rollForward: disable` and without prereleases.
First acceptance matrix: Windows 11 25H2 x64, Windows Server 2025 x64 and Ubuntu 24.04 LTS x64.
Arm64, other distributions, network file systems and AOT are not a release guarantee.

**Rationale**: Supported shared runtime; the queried official
release metadata name SDK 10.0.403 and runtime 10.0.14, published on 2026-10-25.
A concrete matrix limits the portability acceptance without equating Windows with Windows Server
or all Linux systems with a single Linux test.

**Alternatives considered**: .NET 8 with a shorter remaining support horizon;
unpinned SDK roll-forward; additional target frameworks without current need.

**Approved rework (2026-10-27)**: Instead of the originally planned 10.0.403,
explicitly use the locally available 10.0.402. `dotnet --info` confirms
10.0.402 and 10.0.303, not 10.0.403. `global.json` now pins 10.0.402;
no SDK installation or implicit roll-forward was performed.

**Approved update (2026-10-28)**: SDK 10.0.402 is no longer installed;
now available are 10.0.303 and 10.0.403. The project owner has
explicitly confirmed „10.0.403 nutzen“ ("use 10.0.403"). The pin is updated to
10.0.403 without roll-forward; no installation and no change to OpenCode 1.19.0.
Earlier 10.0.402 check results remain unchanged as historical evidence.

Sources: [.NET 10 release metadata](https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json),
[SDK selection](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json),
[Windows support](https://learn.microsoft.com/en-us/dotnet/core/install/windows),
[Ubuntu support](https://learn.microsoft.com/en-us/dotnet/core/install/linux-ubuntu-install).

## R-02 — Package Boundaries and KISS

**Decision**: Six libraries with real responsibilities:

- `Monstertacle.Multisession.Common`: public contracts, session catalog, durable
  admission rules, dispatch admission, FIFO, recovery and structured diagnostics.
- `Monstertacle.Multisession.Storage.Sqlite`: transactional local state.
- `Monstertacle.Multisession.Windows`: preparation/verification of private state directories,
  Windows ACLs and exclusive storage ownership; target `net10.0-windows`.
- `Monstertacle.Multisession.Linux`: corresponding contract with Unix permissions and
  Linux path/ownership checks; target `net10.0`, explicit Linux runtime guard.
- `Monstertacle.Multisession.Providers.OpenAI`: native Conversations/Responses connection.
- `Monstertacle.Multisession.Providers.Gemini`: native Interactions connection.

All concrete libraries depend on Common, never the reverse. Common and
Storage enforce no Windows, Linux, provider, hosting, SSH or UI stack.
No additional abstractions/hosting packages, no dynamic assembly loader and
no agent framework foundation of its own. Facets for sessions, requests and recovery
avoid an overloaded public class and unjustified universal abstractions.

**Rationale**: Shared domain logic is independently testable. Platform packages contain
concrete security/ownership tasks, not duplicated session logic or empty markers.
SQLite and providers can be consumed separately with their own third-party dependencies.

**Alternatives considered**: A single library with all native dependencies;
duplicated Windows/Linux cores; a separate package per domain term; a finished service instead of a library.

## R-03 — Native Provider Sessions Instead of Own Session Features

**Decision**: Two different reference contracts via BCL `HttpClient`:

| Contract | Opening and continuation | Own responsibility |
| --- | --- | --- |
| OpenAI `/v1/conversations` + `/v1/responses` | Create conversation ID; send each new message to the same conversation | Save conversation ID and separate response ID |
| Gemini Interactions `/v1/interactions` | First message opens interaction; follow-up requests reference the last confirmed `previous_interaction_id` | Distinguish local SessionId, first native reference and changing continuation head |

For Google, the native binding only takes place with the first message. A locally opened
session with an empty binding is therefore permissible as long as no request has been sent yet.
A missing reference after a possibly completed handover, by contrast, is uncertain and blocks.

Hand over only new messages and explicit context-relevant settings; no locally
reconstructed conversation, token counting, summary or tool loop.
For Google, among other things, system instructions and generation settings must be
transmitted again per call; this is configuration and not transcript replay.

**Rationale**: The APIs have native stored states and distinguishable operations.
The current official Google documentation designates Interactions as GA since June 2026;
the stable `/v1` reference is implemented, not a mix of older beta examples.

**Alternatives considered**: Stateless chat completions/messages protocols with local
replay or an agent loop of its own violate the scope decision. Separate CLI runtimes
would be possible as later adapters but require additional process, version and
platform contracts and are not required for this first delivery.

Sources: [OpenAI Conversation state](https://developers.openai.com/api/docs/guides/conversation-state),
[Create conversation](https://developers.openai.com/api/reference/resources/conversations/methods/create),
[Gemini Interactions overview](https://ai.google.dev/gemini-api/docs/interactions-overview),
[Gemini `/v1` reference](https://ai.google.dev/api/interactions-api-v1).

## R-04 — Success, Cancellation and Uncertain Handover

**Decision**: Background operations with `store:true`; save native operation identifiers early
and query their state. Only a confirmed `completed` without required
client-side action is success. `requires_action`, `incomplete` and mere HTTP 2xx responses
are not a successful completion of a managed request.

OpenAI: check response via `GET /responses/{id}`; background cancellation via
`POST /responses/{id}/cancel`. Google: `GET /interactions/{id}` or
`POST /interactions/{id}/cancel`; a confirmed cancellation request is not yet a final state.
The library itself configures no provider tools. Client-side requested
tool actions are reported as UnsupportedProviderAction and not executed.

Local timeout or connection abort is no proof of termination. A known operation ID
permits pure re-reading, no renewed send. If it is missing despite possible acceptance, the
request remains Unknown. After a host restart, there is no automatic continuation of an
interrupted job; the recovery interface can explicitly check its state.

**Rationale**: Local commit and vendor call do not form a shared transaction.
Conservative reservations avoid duplicate external effects.

**Alternatives considered**: Automatic POST retries, substitute vendors, deriving success from
process/HTTP status, rebuilding client-side tools as a provider function.

Sources: [OpenAI Background](https://developers.openai.com/api/docs/guides/background),
[OpenAI Function calling](https://developers.openai.com/api/docs/guides/function-calling),
[Gemini Background](https://ai.google.dev/gemini-api/docs/background-execution).

## R-05 — Limits of Native Storage and Compatible Accesses

**Decision**: Endpoint, API contract version, model, host-side credentials and HttpClient
are configurable per adapter. Compatibility means the actually required
session contract, not just similar chat messages. OpenAI-compatible chat completions
alone do not fulfill the Conversations/Responses contract.

According to the documentation, OpenAI conversation objects are not subject to the ordinary
30-day response TTL. Google documents Interactions retention of one day in the
free tier and up to 55 days in the paid tier, with configurable paid periods.
These provider rules are not a local guarantee and not an unlimited model context window.
Expired or deleted native states block continuation; no new session
under the old identity and no replay from local history.

**Rationale**: The user wants to manage sessions, not replicate their functions.
Local persistence retains evidence and queues but does not replace lost provider state.

**Alternatives considered**: Guaranteeing arbitrary OpenAI-compatible endpoints; cataloging all
vendor features; reconstructing missing native state from chat history.

Sources: the same official session and retention documentation as R-03/R-04.

## R-06 — Durable FIFO and Persistence

**Decision**: `Microsoft.Data.Sqlite` 10.0.14; local store, one active owner,
short serialized transactions; `journal_mode=DELETE`, `synchronous=EXTRA`, foreign keys
enabled, no shared cache use. A dedicated bounded database work queue
isolates the synchronously working SQLite calls from the caller and permits no unbounded
task creation. No SQL transaction remains open across a provider call.

Commit admission transactionally with monotonic session sequence and queue capacity.
Persist dispatch with reservation before the external call. Commit result, continuation state
and safe release together. Channels serve only as bounded wake-up signals;
the database remains authoritative, even with a wake-up signal lost after commit.

**Rationale**: The initial target is ten sessions, not a distributed broker. A serialized
store makes WAL/checkpoint complexity unnecessary. According to the documentation, even the async methods of Microsoft.Data.Sqlite
execute synchronously. DELETE/EXTRA is a deliberate alternative to WAL/FULL;
durability additionally depends on the actual file system and storage medium.
The busy timeout is explicitly limited to one second; the longer default value MUST NOT
covertly exceed the local cancellation observation and the finite shutdown deadline.

**Alternatives considered**: An in-memory queue loses accepted work; multiple JSON files
do not form a shared transaction; PostgreSQL/broker force a service; WAL/FULL
is a separate change if a parallel read need is demonstrated later.

Sources: [Transactions](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/transactions),
[SQLite Async limitations](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/async),
[Database errors](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/database-errors),
[SQLite synchronous](https://sqlite.org/pragma.html#pragma_synchronous),
[Channels](https://learn.microsoft.com/en-us/dotnet/core/extensions/channels).

## R-07 — Storage Ownership, Permissions and Trust Domain

**Decision**: Host specifies an absolute private state directory. Windows checks/restricts
ACLs for the executing identity; Linux directories 0700 and files 0600.
An exclusive lock file kept open during runtime prevents a second cooperating
owner. Path aliases, links, UNC/network paths and existing foreign stores are explicitly
checked or rejected; no lock is deleted solely on the basis of a PID file.

One trust domain per instance. No authentication of its own, tenant management,
DPAPI key storage or sandbox. Secrets come from a host resolver when needed.
Provider endpoints are taken from trusted host configuration, not from
model texts; authentication headers are not forwarded to other hosts.

**Rationale**: A real platform task without operating system dependency in the core. Local permissions
do not protect against arbitrary code of the same user or a malicious provider plugin.
The actual lock semantics are to be demonstrated on both platforms by separate process probes.

**Alternatives considered**: DI singleton as a supposed process lock; systemd as a
library prerequisite; premature encryption/credential infrastructure of its own.

Sources: [FileStream.Lock](https://learn.microsoft.com/en-us/dotnet/api/system.io.filestream.lock),
[UnixFileMode](https://learn.microsoft.com/en-us/dotnet/api/system.io.unixfilemode),
[DirectorySecurity](https://learn.microsoft.com/en-us/dotnet/api/system.security.accesscontrol.directorysecurity).

## R-08 — Versioned Dependencies and Tests

**Decision**: Central later package versioning with lock files and locked restore.
The following concrete package versions and license information were read from official NuGet
index/nuspec metadata; introduction and transitive checking only take place during implementation.

| Package | Version | License | Use |
| --- | --- | --- | --- |
| Microsoft.Data.Sqlite | 10.0.14 | MIT | exclusively Storage.Sqlite |
| Microsoft.Extensions.Logging.Abstractions | 10.0.14 | MIT | structured core diagnostics without host requirement |
| Microsoft.NET.Test.Sdk | 18.11.0 | MIT | test development |
| xunit | 2.9.3 | Apache-2.0 | test development |
| xunit.runner.visualstudio | 3.1.5 | Apache-2.0 | VSTest execution |

Providers use HttpClient/System.Text.Json from .NET 10; no additional vendor SDKs
or automatic retry middleware packages. Test agents and controlled HttpMessageHandlers
replace live accesses in deterministic tests. Real provider and platform acceptance remain
separate gates; missing credentials mean NOT RUN, never PASS.

**Rationale**: Smaller dependency surface, no agent loop of its own, the same contracts
for test and real adapters. Compatibility of the entire test package combination is not
derived from the mere existence of their NuGet metadata.

**Alternatives considered**: Floating versions; a second test framework; a complete
agent framework only for two narrow native HTTP contracts.

Sources: official flat container nuspecs under
`https://api.nuget.org/v3-flatcontainer/<paket>/<version>/<paket>.nuspec` for the packages above.
Before release, additionally check the actual native SQLite version, transitive licenses and
vulnerabilities; no audit/security evidence has been provided yet.

## R-09 — Approved Official Codemetr CLI Guard

**Decision**: The project owner has explicitly approved the official CLI guard
`Codemetr.CSharp.Tool` 0.1.0 with default thresholds without a debt baseline.
Constitution v1.6.0 names this checker instead of an unconfirmed
marketplace extension identity. The tool is pinned locally and restored.
The guard from `codemetr/skills`, commit `b3d1f948e536fe18903ed9e4fcad5962ec0e0a88`,
plugin 0.2.0, is integrated as the OpenCode project adaptation `codemetr-guard` with MIT license.
Skill and tool have independent versions; a skill is not automatic enforcement.

For Q-01, approval and setup are documented in `evidence/codemetr-approval.md`.
The actual check of every changed own class including tests/host with
source revision reference and re-check after refactoring remains mandatory before code release.
Full check: `dotnet tool run codemetr-csharp -- check --format json` from the project root;
CI on Windows/Linux, exit 1/2 and an empty measurement block. No product check is claimed.

Approved CLI defaults, explicitly recorded in `codemetr.json`:
type lines ≤600, constructor parameters ≤6, public methods ≤10, instance fields ≤8,
member complexity ≤25, maintainability index ≥15, coupling ≤40, inheritance depth ≤3,
member parameters ≤7 and nesting ≤4. Under these defaults, named classes MUST NOT,
among other things, end in Manager, Helper or Processor.

The package documents analyze/check/baseline and machine-readable reports. Syntax and
complete measurement coverage are to be verified on the pinned package; the current repository main
does not prove the same published version. Partial types and unresolved external
references are explicit measurement limits. No debt baseline import to turn new findings green.

**Rationale**: Metrics support review but replace neither tests nor an
identified mandatory checker. Unverified tools MUST NOT count as passed.

**Alternatives considered**: Passing off the CLI as a marketplace extension without consent;
using arbitrary IDE metrics; treating a pure numbers check as SOLID/behavior evidence.

Sources: [NuGet 0.0.1](https://www.nuget.org/packages/Codemetr.CSharp.Tool/0.0.1),
[Publisher sources](https://github.com/codemetr/Codemetr.CSharp),
[Skills marketplace](https://github.com/codemetr/skills).

Rework: [NuGet 0.1.0](https://www.nuget.org/packages/Codemetr.CSharp.Tool/0.1.0),
[pinned guard skill](https://github.com/codemetr/skills/blob/b3d1f948e536fe18903ed9e4fcad5962ec0e0a88/plugins/codemetr-csharp/skills/guard/SKILL.md).
`dotnet tool restore` and `--help` executed successfully. With currently zero C# files,
the root check returns exit 0; this is only setup evidence, not a code release.

## R-10 — Deliberate Acceptance Limits

**Decision**: Design decisions have been made; no technical selection question remains
as an unresolved placeholder. Evidence not yet produced is tracked as gates:
Q-01 Codemetr release, Q-02 reproducible restore/build/audit, Q-03 cross-platform
FIFO/crash/lock/permission probes, Q-04 real native sessions of both vendors,
Q-05 NuGet consumption without source references. The unknown ratification date of the constitution
remains a governance TODO to be confirmed manually and is not invented.

**Rationale**: A plan may define future verification work but MUST NOT claim a green build,
live access or an approved governance baseline.

**Alternatives considered**: Equating design review with product release;
hiding missing live accesses behind fake results.

## R-11 — Long-Term Consuming Target Host

**Decision**: SharpClaw is to use the library in the long term in its own process as a
NuGet dependency. This explicit product requirement is taken into account via the existing
public host/provider boundary and an isolated consumption proof.
Neither a reverse library dependency nor a mandatory
additional multisession service is created. No renewed adoption of temporary research material.

**Rationale**: The intended productive consumer is not to require library internals
or source code copies. It can register its own provider adapters and
continue to be responsible for lifecycle and configuration itself. FR-027 and SC-011
check this prerequisite without premature intervention in the consuming application.

**Alternatives considered**: Application-specific fork, direct source references,
forcing a separate service or immediate complete migration of existing state.
The actual integration and adoption of existing sessions are a separate
later project; no compatibility evidence is derived from documentation alone.

## R-12 — Renewed Planning with Mandatory TDD

**Decision**: Constitution v1.6.0 is the binding basis. Existing
requirements including FR-027 are implemented test-driven: first observe an executable
behavior test as functionally red, minimal implementation until Green,
then refactoring with green regression tests and a renewed Codemetr check.
The plan maps TDD-01–TDD-07 to the requirements and success criteria.

**Rationale**: This is an explicit user/governance decision, not a new
technology option. The existing provider fakes, controlled HTTP contracts and real
temporary SQLite/process probes enable Red without production credentials.
A non-compiling test, a missing library or a missing API key
is not functional Red evidence. Real vendor, package and target platform acceptance
remain additional evidence after deterministic Green.

**Alternatives considered**: Tests only after complete implementation; only one green
CI run at the end; live provider errors as an apparent Red substitute; artificially red tests
for purely behavior-preserving refactorings. These alternatives do not fulfill the requirement.

Q-06 records test identifier, command, source revision/hash, observed Red reason and subsequent
Green. After re-evaluation, the preceding technical decisions remain
suitable; a further vendor/framework comparison is not necessary due to TDD.
The source of this decision is the updated project constitution and the
explicit user instruction. In this renewed planning run, no test sources,
production files or artificial Red/Green records were created and no tests were executed.

## R-13 — Retroactive Reconciliation with the Current Target Consumer

**Source revision**: SharpClaw `origin/main`, commit
`71d3cbe731ff5f94a113aaf82584daad8eccbedd`, updated by fast-forward on 2026-10-27.
Clean working tree before and after the update. Only versioned sources,
documentation and tests read; no instance data, sign-ins or session logs.
No SharpClaw sources changed and no SharpClaw tests or agents executed.

**Findings**:

- The host consumes `IAgentBackend.ExecuteAsync(RunContext, prompt, observe,
  cancellationToken)`. CLI protocols implement `ICliProtocol` and are executed via
  `CliBackend` with a shared process runner; SDK backends can
  implement `IAgentBackend` directly. The current plugin contract is version 3.
- The registry selects registered backends via technical IDs and polymorphism;
  unknown/duplicate IDs are rejected. Bundled are Codex, Grok and Claude.
  No dedicated Gemini/OpenAI REST adapter was found in the examined C# source revision.
  Codex is an agent CLI protocol, not equivalent to the Responses model API.
- The external OpenCode protocol plugin passes the configured `provider/model`
  unchanged and uses the stored opaque `ses_…` identifier for continuation.
  It knows no OpenAI/Gemini branching. The source revision explicitly pins OpenCode
  **1.19.0**; this is no evidence of compatibility with the current OpenCode V2 API.
- Sessions bind backend, workspace, model and native ID. The host stores
  Attempted before execution, observed native IDs before further use and refuses
  an unexpected identity change. Completion requires a terminal success signal
  in addition to exit code 0; intermediate tool steps are not completion.
- The existing OpenCode tests describe an identical session for a follow-up job,
  rejection of a wrong version, a missing/changing ID and false success signals.
  These tests were read, not executed. The existing RunHost uses
  Linux/systemd process mechanisms; it is not a Windows acceptance of the new library.

**Impact on the previous design**: FR-027 requires real
embeddability evidence. An arbitrary test adapter plus two model REST adapters
do not prove that the execution, event, workspace and continuation contract
of an existing agent backend can be represented. A plain model request and an agent job
MUST NOT be equated; according to the scope requirement, the library also MUST NOT itself rebuild the missing tool/agent
runtime.

**Open design review before functional implementation**: The generic adapter boundary
must actually be able to represent agent backends alongside model accesses, in particular
workspace, stable native ID, completion/error signal, cancellation and uncertain handover.
Reconcile the existing ISessionProvider/ProviderReply design against this and add suitable
contract cases before Green. Direct OpenAI/Gemini connections are thus not an
already confirmed integration solution for the target host. Choosing a mandatory
OpenCode runtime would moreover change FR-022 and is not done silently here.

The dependency direction remains target host → library. No source code copies,
cross-project source references, migration of existing state or changes to running
native sessions. The previous statement of complete implementation readiness
is explicitly restricted until this contract reconciliation.

## R-14 — Generic Contract Instead of Equating Agent and Model

**Decision**: `ISessionProvider` remains a transport-neutral adapter boundary. Added
are explicit capabilities and ExecutionKind, frozen workspace context,
awaitable session/operation observations and a confirmed terminal reply.
Concrete mapping and contract cases to be written first are in
provider-contract.md §8 and public-api.md §9. HTTP endpoint configuration remains with the
HTTP adapter; it is not a mandatory field of host-owned CLI/SDK backends.

The project owner has **explicitly retained OpenCode 1.19.0**.
For the reference adapter, this concrete reference replaces any unconfirmed assumption
about newer V2 contracts. OpenCode remains optional, since FR-022 permits no mandatory runtime.
The already planned native model adapters are explicitly model accesses, not a substitute
for a guaranteed agent job. Own tools/compaction remain excluded.

**Rationale**: A generic registration boundary can accommodate both existing agent
backends and model protocols without checking product names in the core
or rebuilding an agent runtime. Stable native identity and changing
continuation remain separate; observation callbacks preserve the persistence boundary.

**Alternatives**: OpenCode as the only mandatory runtime (violates FR-022); treating a model response
as agent completion (violates Constitution II); source dependency on the
target host (violates FR-027). Actual tests/consumption acceptances explicitly remain open.

## R-15 — Renewed Reconciliation after Changes in the Target Host (2026-10-28)

Current loaded SharpClaw revision: `7854a122218382156789d520b0a00f2060b1f8f3`.
The comparison since R-13 confirms unchanged backend/session/plugin contracts and
still OpenCode 1.19.0. New are in particular explicit fleet recovery,
task-specific observation, declared output paths and hash-bound immutable
context sources. These functions remain host responsibility, not a new multisession
dependency or license to rebuild an agent runtime. Prepared inputs are
accepted unchanged, not replaced by re-reading mutable context files.
Commit, file hashes, deep-dive findings, regression risks and the next bounded
test plan are documented in `evidence/architecture/2026-10-28-sharpclaw-refresh-request-admission.md`
before implementation. Source analysis is not a new binary/live/migration acceptance.
