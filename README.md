## Agent Framework

Host integrations use the provider-neutral `Monstertacle.Multisession.AgentFramework` package with Microsoft Agent Framework 1.27.0. Durable CQRS, request identity and Unknown recovery remain authoritative. Only the current store format is read; stores of any other format are refused, never converted.

# Monstertacle.Multisession

Provider-neutral C#/.NET libraries for managing parallel agent sessions.
**In development, no product release.** SQLite persistence, native Windows/Linux leases,
protocol adapters for Conversations/Responses and Interactions, as well as Cancel, Stop and Recovery are implemented.
Live, target-system, integrator and final package acceptances remain required;
the current scope is described in the [work record](specs/001-agent-multisession/evidence/implementation-progress.md).

## Local packages 0.2.0-alpha.7

Eight libraries are packed locally: `Common`, `AgentFramework`, `Storage.Sqlite`, `Linux`,
`Windows`, `Providers.Conversations`, `Providers.Interactions` and `DeliveryCheckpoints`, each with the prefix
`Monstertacle.Multisession.`. `DeliveryCheckpoints` is independent of the session packages: one opaque durable
delivery checkpoint per channel with typed atomic acknowledgement/CAS primitives, see [Delivery checkpoint](docs/delivery.md). Test projects and ValidationHost are not packages.
No publication takes place. The libraries are licensed under the MIT License
(SPDX `MIT`, file [LICENSE](LICENSE)); every package contains the license text with
the copyright notice. Dependencies are carried only as NuGet references and keep
their own licenses; the packages contain no third-party code. Files under
`.opencode/skills/codemetr-guard/` keep their own license file.

```powershell
dotnet build Monstertacle.Multisession.slnx -c Release --no-restore
dotnet pack Monstertacle.Multisession.slnx -c Release --no-build -o artifacts/packages
pwsh -NoProfile -File eng/Test-PackageLayout.ps1
```

An application references `Common`, `Storage.Sqlite` and exactly its native
platform package. On Windows, `net10.0-windows` is to be used; on Linux, `net10.0`.
Vendor adapters are optional; custom adapters implement
`ISessionProvider` exclusively via the public contracts.
The library is embedded in-process and requires no additional service.
The [embedding guide](docs/embedding.md) walks through package reference, custom adapters,
vendor configuration and publish. [Operations and recovery](docs/operations.md)
describe deadlines, reservations, diagnostics and safe resumption.

## Development environment and checks

CQRS is integrated via `Monstertacle.SharpQrs` **0.2.0-alpha.1**.
`SessionRuntime.Commands` and `SessionRuntime.Queries` provide typed
asynchronous operations after start. The existing ports remain compatible and go through the same
dispatchers. [CQRS contract and examples](docs/cqrs.md), [SpecKit feature](specs/002-sharpqrs-cqrs/spec.md).

- .NET SDK **10.0.403** as per `global.json`, no implicit roll-forward.
- PowerShell 7 (`pwsh`), Git and NuGet access; no Python.
- Approved local `Codemetr.CSharp.Tool` **0.1.0**; no debt baseline.
- Repository-local `Codemetr.CSharp.Cover.Tool` **0.1.1**; configuration in `codemetr-cover.json`.
- At least **87 %** line coverage of the entire native product scope per platform.

From the project root:

```powershell
dotnet tool restore
dotnet restore Monstertacle.Multisession.slnx --locked-mode
pwsh -NoProfile -File scripts/Invoke-Quality.ps1 -Platform windows
dotnet tool run codemetr-csharp -- check --format json
```

On a native Linux host, use `-Platform linux`. The checks must not
be replaced by a mere TFM switch on a different operating system.
The quality check runs Codemetr Cover on the fresh Cobertura/TRX files of all
four test suites and requires `complete: true`. In addition to the native 87 % gate, Cover checks
at least 87 % of the entire native library code and of the complete validation host, including unmeasured lines
with zero hits, as well as the unchanged default limits for diff coverage and CRAP.
The separate product gate keeps the complete library scope as its own denominator;
co-measured validation-host lines cannot compensate for a product gate that falls short.
Build artifacts and test code do not count as separate application code. The run generates
`cover-native-config.json` and excludes exactly the foreign platform; it is mandatorily
checked in the respective other native CI run. All shared libraries remain included.
Reports are located under `artifacts/quality/<platform>/<run>/`.
The three functional wave scenarios choose an explicit admission budget of
30 seconds. The SDK default remains five seconds; the dedicated
deadline regressions and all other scenario deadlines remain in place.
This functional test load is not evidence of a five-second admission SLA;
the native observation on this is in the [investigation plan](specs/003-native-reliability/follow-up-windows-admission.md).
Detailed rules are in [AGENTS.md](AGENTS.md), the
[Constitution](.specify/memory/constitution.md) and the
[Quickstart](specs/001-agent-multisession/quickstart.md).

## Handover to a remote agent

Repository: <https://github.com/monstertacle/monstertacle.multisession>.
**Active assignment: implement all open Speckit tasks autonomously.**
Work status and dependencies: [Overall implementation](specs/001-agent-multisession/evidence/implementation-progress.md).
T112–T115, dispatchers and public runtime composition are implemented; evidence and limits
are in [tdd-us1.md](specs/001-agent-multisession/evidence/tdd-us1.md) and the other story evidence records.
Conversations/Responses and stable Interactions are implemented against local HTTP fixtures;
[Provider contract and limits](docs/providers.md). Public adapter integration, SQLite transactions,
crash recovery as well as Cancel, Stop and Close have since been implemented with real local store tests;
evidence and remaining acceptance limits are in
[tdd-us3.md](specs/001-agent-multisession/evidence/tdd-us3.md) and
[tdd-us5.md](specs/001-agent-multisession/evidence/tdd-us5.md).
Bootstrap options reconciliation and explicit local retention/deletion are also implemented;
their complete acceptance limits remain visible in the tasks. Storage failure probes,
diagnostics and native process checks are present; package consumption and final
acceptances are in the work record. The controlled store remains a test projection.
No product release.

1. Create your own working branch from the current remote state; no parallel
   changes by multiple agents in the same working tree.
2. Read AGENTS.md, the Constitution, the [Plan](specs/001-agent-multisession/plan.md),
   the [Tasks](specs/001-agent-multisession/tasks.md) and the
   [orchestration contract](specs/001-agent-multisession/contracts/orchestration-details.md).
3. Set the feature context locally, since `.specify/feature.json` is intentionally not versioned:

```powershell
$env:SPECIFY_FEATURE_DIRECTORY = 'specs/001-agent-multisession'
& ./.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireSpec -RequireTasks -IncludeTasks
```

For an agent session started from Bash, beforehand:

```sh
export SPECIFY_FEATURE_DIRECTORY=specs/001-agent-multisession
```

The Codex skills are already located under `.agents/skills/`. For a client with this
skill support, the agent assignment (not a shell command) is:

```text
$speckit-implement Implement all open tasks in dependency order.
Reuse the existing runtime, dispatch, recovery and paging contracts.
Before implementing, document a current deep dive,
observe a domain Red before Green, run the regression, the native quality check and the complete
Codemetr guard. Reuse T112/T025; continue all open stories.
Return changes and check evidence
on a working branch for review; no merge, deployment or NuGet push.
```

If the client does not detect skills automatically, load the skill
`.agents/skills/speckit-implement/SKILL.md` with this overall implementation assignment.
The project-local Spec Kit scripts use PowerShell, also on Linux.

The bundled Spec Kit extension `git` (1.0.1) is installed only for explicit
invocation. All of its hooks are disabled in `.specify/extensions.yml`:
`$speckit-specify` does not create a branch, and no command commits automatically. Each
feature receives exactly one branch, created by its author: either the own
working branch from step 1 or, once, `$speckit-git-feature` (OpenCode:
`/speckit.git.feature`) on the current `main`, which creates `feature/{number}-{slug}` with the
next spec number. `$speckit-git-validate` checks the branch name. The
Python copy of the extension scripts is not installed because of the Constitution.
`specify extension update git` or a reinstallation re-enables the hooks,
removes the Codex skills and restores `scripts/python`. This diff is to be reviewed before
use and reverted to the approved configuration;
`scripts/Test-SpecKitConfiguration.ps1 -ForbidPython` checks it in the guard CI and
rejects an unreviewed extension version.

## Evidence and repository boundaries

Work evidence is located under [evidence](specs/001-agent-multisession/evidence/README.md).
The [repository/clone check](specs/001-agent-multisession/evidence/repository-bootstrap.md)
documents the tested initial commit and the limits of this local check.
Raw reports/TRX/source manifests under `artifacts/` and `TestResults/` remain local
or are transferred as CI artifacts; a fresh clone does not contain them.
Missing original artifacts must not be reconstructed and presented as a historical execution.
The documented initial manifest gap remains open.

Earlier statements „kein Git-Repository“ (“no Git repository”) in historical evidence describe the state
at that time. Initialization and the first push were subsequently explicitly approved.
This does not fulfil T111/Required Checks or the product acceptances that are still open.
SharpClaw remains a later consumer, OpenCode **1.19.0** an optional reference.

`.gitignore` is based on the [official GitHub VisualStudio template](https://github.com/github/gitignore/blob/main/VisualStudio.gitignore),
extended with local agent data, SQLite state and the explicit inclusion of the coverage inventory.
No credentials, native sessions or productive state stores are versioned.

SQLite supports explicit `SqliteStoreFactory.CreateNewAsync` or
`OpenExistingAsync`, session/request lookup, session/binding commit and durable admission
via a bounded single DB worker. The lease port prepares private files;
metadata, trust scope, header and actual schema are checked before use.
Reopening preserves waiting inputs and sequences. Native DB busy time is initially
fixed at one second; other values are explicitly rejected before file access.
Functional process-kill and native ownership evidence remain open;
[evidence](specs/001-agent-multisession/evidence/tdd-us5.md).

The SQLite dispatch slice secures reservation and unchanged input before sending,
native identifier callbacks before confirmation, and known completions atomically with result,
history, binding progress and release. Session/request/history pages are available.

The SQL recovery projection holds uncertain reservations, opens persistent cases
and supports revision-protected explicit decisions. Bootstrap treats old
Running as Interrupted/Unknown; open cases are readable with pagination. Before worker start,
local provider/binding/execution support is checked. A registration that is present again
releases only its own configuration locks. Public overall process-kill acceptance and further
lifecycle/platform tasks remain open; no product release.

Optional diagnostics are passed to `SessionRuntime` via a host-owned `SessionDiagnostics`
and disposed of after the runtime ends. Confirmed admissions and stored completions
emit structured ILogger events and Activities with local identifiers, state,
certainty, duration and public failure category. Inputs, outputs, bindings and
workspace contents are not reported. Logger/listener exceptions are isolated;
`FailureCount` counts them. A dedicated diagnostics object per runtime additionally holds BCL duration histograms, a counter of observed completions and cached queue/reservation gauges. The monitor reads initially and with a one-second pause; missing or stale values are withdrawn on read failure/stop. Complete diagnostics acceptance is still open.
