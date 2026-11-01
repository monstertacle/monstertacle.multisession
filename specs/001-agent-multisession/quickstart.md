# Quickstart & Validation: Agent Multisession

**Date**: 2026-10-27 | **Basis**: [plan.md](plan.md)

Implementation reconciliation: 2026-10-29T21:57:58Z. Solution, projects, native leases,
ValidationHost and package consumption script are implemented. Local as well as native Windows
Server/Ubuntu results are under evidence; a command alone is not verification evidence.
Windows 11, real provider accounts and independent integrator acceptance remain open.
Constitution v2.0.0 and FR-027 apply, including the actual agent/target host contract:
the following acceptance commands do not replace the preceding test-first flow.

## 1. Prerequisites

- Exact .NET SDK 10.0.403; C# 14; development on a target system named in plan.md.
- Checked-in solution and unchanged package lock files.
- Private local test directory under one's own identity; no production stores.
- No provider accesses needed for unit, contract, FIFO, crash and platform tests.
- For live probes: explicitly approved test accounts of both vendors, a suitable
  model, permitted native storage and possible charges. Do not configure any tools.
- Q-01: CLI 0.1.0, default thresholds without a debt baseline and the guard skill are approved
  and set up; see `evidence/codemetr-approval.md`. Product check remains open.

All following paths are relative to the repository root. The ValidationHost
accepts absolute `--state-root` paths, uses a new subdirectory per run and
deletes no foreign/production directories. `--scenario` and the scenario names given below
are its implemented acceptance contract. The libraries are embedded in the host process.

## 2. Restore, Build and Static Checks — Q-02

On every build host with the same source revision:

```text
dotnet --version
dotnet restore Monstertacle.Multisession.slnx --locked-mode
dotnet build Monstertacle.Multisession.slnx -c Release --no-restore
dotnet format Monstertacle.Multisession.slnx --verify-no-changes --no-restore
dotnet list Monstertacle.Multisession.slnx package --vulnerable --include-transitive
```

Expected: SDK 10.0.403, successful restore/build, no new compiler/analyzer
warnings and no unresolved release findings. Assess the audit output by content;
a process exit code alone does not automatically mean no security findings.

The configured CI runs restore, build, format/analyzers and tests on Windows and Linux
and repeats the NuGet vulnerability check at least weekly and before release.
Record package and native SQLite version, runtime, platform and source revision in the evidence.

Codemetr: load the project-local skill `codemetr-guard`; run from the project root:

```text
dotnet tool restore
dotnet tool run codemetr-csharp -- --help
dotnet tool run codemetr-csharp -- check --format json
```

The full check covers own classes including tests/ValidationHost.
Save results, measurement limits and source revision, refactor findings and check again.
Exit 1/2 blocks release; exit 0 with zero classes is not product evidence and is
rejected by the CI workflow. No debt baseline and no silently changed thresholds.
Tool restore requires NuGet network access; no global installation necessary.

### Coverage Gate — At Least 87 % Line Coverage (Q-02)

Collector 10.1.0, `tests/coverage.runsettings`, scope inventory and gate smoke tests are
set up. The local runner corresponds to the configured CI flow and requires
PowerShell 7 and the pinned SDK; no Python runtime:

```text
pwsh -NoProfile -File scripts/Invoke-Quality.ps1 -Platform windows
pwsh -NoProfile -File scripts/Invoke-Quality.ps1 -Platform linux
```

Run each command only on its native operating system. The runner uses
a new report directory and the same source hash for build/tests/coverage. Current
Windows run: 88,2576 % (233/264 product lines; 177 .NET tests passed), exit 0
at the explicitly approved minimum value of 87 % (Constitution v2.0.0);
Linux and real GitHub CI still NOT RUN. Current source revision: `evidence/coverage.md`.
This is not a finished application; see `evidence/coverage.md`.

- Windows and Linux each measure all shared product libraries and the
  native platform package, including assemblies not loaded. The scope inventory
  must cover all six libraries across both native runs; no silent omission.
- Test code does not count toward the denominator; exclude generated code/third-party code only with justification.
- Merge reports of the test projects per platform line by line, do not average percentages.
- Less than 87 %, missing/empty/invalid reports or missing product assemblies
  cause the gate to fail. Exactly 87 % passes; perform the assessment before rounding.
- In `evidence/coverage.md`, document tool versions, commands, source revision, platform, project inventory,
  exclusions, raw reports and gate results. Coverage does not replace TDD evidence.

The existing `.github/workflows/codemetr-guard.yml` checks Codemetr, not coverage.
After the repository is connected, both coverage/test CI and Codemetr checks must be
set up as required branch checks; local files do not activate branch protection.

### Test-First Cycle Before Every Behavior Implementation — Q-06

The units TDD-01–TDD-07 are in the plan. For each small behavior step:

1. Prepare the test and minimal behaviorless signatures so that the test compiles
   and is discovered. Only create test infrastructure, not production behavior yet.
2. Run the targeted test and actually observe the functionally expected failure.
   Example for the existing routing test group:

   ```text
   dotnet test tests/Monstertacle.Multisession.UnitTests/Monstertacle.Multisession.UnitTests.csproj -c Release --no-restore --filter FullyQualifiedName~SessionRoutingTests
   ```

   Expected Red: a concrete assertion shows, for example, a missing or
   wrong session assignment. Missing SDK, compile errors, installation errors or
   zero discovered tests are not valid evidence. Test groups not yet implemented
   MUST NOT be described as actually red in this documentation phase.
3. Implement only the behavior needed to satisfy this test. Run the same test
   again, demonstrate Green and start the relevant regression test suite.
4. Refactor with green tests; then repeat tests and the prescribed Codemetr check.
   No new functional scope in supposedly structural refactoring.
5. Record FR/SC reference, test identifier, command, source revision/hash, Red reason and Green result in the
   change evidence. A later green run alone does not prove the order.

For TDD-03, targeted SQLite/crash tests go first into IntegrationTests, for TDD-05
controlled HTTP probes into ContractTests and for TDD-06 native ownership/permission probes
first into the respectively matching PlatformTests target variant. The infrastructure for this may
be prepared; the production behavior to be checked only comes into being after functional Red.
Bug fixes start with a failing regression test. Pure refactorings,
by contrast, start with an already green relevant suite and require no invented Red.

The configured CI runs the reproducible Green/regression check; in addition,
the documented Red–Green order is checked in review. Live probes, package smoke test,
load/soak runs and complete target system acceptance remain further release evidence.

## 3. Deterministic Tests — Q-03

```text
dotnet test tests/Monstertacle.Multisession.UnitTests/Monstertacle.Multisession.UnitTests.csproj -c Release --no-restore
dotnet test tests/Monstertacle.Multisession.ContractTests/Monstertacle.Multisession.ContractTests.csproj -c Release --no-restore
dotnet test tests/Monstertacle.Multisession.IntegrationTests/Monstertacle.Multisession.IntegrationTests.csproj -c Release --no-restore
```

These projects have no automatic live execution. Provider fakes and controlled
HTTP handlers use the same public contract as the real adapters.
They measure exclusively management and assignment, not vendor response quality.

Run platform tests separately; do not skip a non-matching platform as green:

Windows:

```text
dotnet test tests/Monstertacle.Multisession.PlatformTests/Monstertacle.Multisession.PlatformTests.csproj -c Release -f net10.0-windows --no-restore
```

Linux:

```text
dotnet test tests/Monstertacle.Multisession.PlatformTests/Monstertacle.Multisession.PlatformTests.csproj -c Release -f net10.0 --no-restore
```

Expected: valid private permissions, rejection of foreign or invalid stores,
second owner in a separate process rejected, path aliases checked, lease
reacquirable after real process termination and uncertain reservation data nevertheless preserved.
Windows 11 and Windows Server each get a real run; Linux does not replace them.

## 4. Consuming Acceptance Host

Windows example in PowerShell:

```powershell
dotnet run --project examples/Monstertacle.Multisession.ValidationHost -c Release -f net10.0-windows -- --scenario deterministic --state-root "$env:LOCALAPPDATA/Monstertacle.Multisession.Validation"
```

Linux example in Bash, after preparing a private test store of one's own:

```bash
dotnet run --project examples/Monstertacle.Multisession.ValidationHost -c Release -f net10.0 -- --scenario deterministic --state-root "$HOME/.local/state/monstertacle-multisession-validation"
```

The host wires Common, the respective platform library, SQLite and test providers.
It opens sessions, submits requests, reads receipt/status/history and stops in a controlled way.
It MUST NOT incidentally set up a service installation, public ports, credential prompts or
production sign-ins. Details of the library call are in the
[public contract](contracts/public-api.md), not as complete application code here.

## 5. Mandatory Scenarios and Expected Results

| Scenario | Execution / expectation | Reference |
| --- | --- | --- |
| deterministic | Ten sessions, two vendor fakes, two messages each; free independent sends overlap | SC-001 |
| routing | 100 mixed requests; correct identity, no context crossover, FIFO per session | SC-002 |
| failures | Five errors and five independent successes remain correctly assigned | SC-003 |
| cancellation | Monotonic measurement from receipt of the cancellation request or the actual deadline end; for a cooperatively held-open request, a durably queryable final state including certainty within two seconds | SC-004 |
| shutdown | Monotonic deadline from stop receipt: local end within ten seconds; remaining Queued requests Cancelled/NotStarted, unconfirmed external effect Unknown with reservation | SC-004, FR-013 |
| capacity | Two requests are already running, held in a controlled way; then eight simultaneous arrivals with queue limit 3: three additionally accepted, five rejected; a total of five of ten accepted | SC-005 |
| workspace | Same writing workspace never simultaneously, independent third workspace can use free capacity | FR-016 |
| extensions | Third different test contract without change to the core or consumer; compatible endpoint only via configuration | SC-008 |
| recovery | Ten process aborts with limit 2: one completed result each in A/B, three Queued in A, one Running in B, separate workspaces; A uses a free slot, B remains blocked without replay | SC-009 |
| native-context | Follow-up requests keep native assignment; context errors assigned unchanged; zero own tools/compaction/replay | SC-010 |
| isolation | Two instances and stores mix neither identifiers nor data/provider registrations | FR-026 |
| embedded-consumer | Consuming application with its own provider adapter: two parallel sessions, follow-up requests, status, cancellation and termination without library change or additional multisession process | FR-027, SC-011 |
| retention | Only a completed conflict-free session can be deleted locally; no unresolved work silently removed | FR-021 |
| soak | One monotonic hour from start, at least 60 accepted requests per target system; all demonstrable under their original identifier with final state or saved state/reservation | SC-007 |
| limits | Requirements for empty inputs as well as exactly permissible inputs/outputs and inputs/outputs one UTF-8 byte too large; no truncated success response and no change to existing history | FR-021, FR-025 |

Each scenario name is recognized as `--scenario` following the same command pattern as above.
Soak and crash probes run explicitly separately, not as random long runners in the unit test.
The host returns a structured Pass/Fail/NotRun summary and exit code 0 only if the
selected probe passed. Machine-readable evidence contains no contents/secrets.

For recovery, actually kill a child process, not just dispose normally. Crash points:
before/after admission commit, after commit before wake-up signal, after reservation before network handover,
after external acceptance before saving the operation ID, before/after result commit.
Additionally check storage failure, read-only, full storage medium, corrupted/unknown schema
and missing expected store. A probe MUST NOT create an empty substitute store.
The supplementary contract cases are defined in [orchestration-details.md](contracts/orchestration-details.md)
and implemented by T112–T115: frozen dispatch payload, uncertain opening
without request, publicly readable recovery revision, CAS conflict, admission Unknown with
candidate identifier and range-bound cursors. Their executed Red/Green and
SQLite probes are documented in the story-related TDD reports.
The first delivery performs no format migration: a differing schema/binding
must be rejected before changes. A migration/rollback test plan of its own is
only required with an explicitly planned later format change; it is
not replaced here by an apparent check of not-yet-existing migrations.

For each probe, document the initial state, limits, accepted/rejected identifier set,
target order and monotonic points in time. With full reservation capacity,
waiting work is permissible; uncertain slots MUST NOT be released to satisfy an apparent
progress proof. On unavailable storage, a
successful persisted shutdown is not a guarantee and MUST NOT count as such.

## 6. Real Provider Probes — Q-04

Host configuration contains endpoints, native contract version, model and CredentialReference.
Secrets come from a secure source prepared by the operator, not from
command-line arguments or versioned files. Do not enter any keys in this guide.

After explicit cost/storage approval, T105's concrete live execution for
`live-openai` and `live-gemini` is set up and executed with the same host pattern.
Until then, the current host explicitly reports NOT RUN for these.
Prerequisites and native operations are in the
[provider contract](contracts/provider-contract.md).

Per vendor and target system:

1. Send a new local session and first text message; check native identifiers in the private
   store, do not write them to public logs as confidential content.
2. Send a follow-up request referring to the first message; the same native session reference
   or confirmed interaction chain head is used.
3. Restart the host without a running request and continue again; do not replay any history.
4. Demonstrate missing/expired session and context errors with controlled contract fixtures,
   do not deliberately create a real long or expensive conversation.
5. Reconcile result and cancellation states against native operation states.

Missing credentials, model access or a missing target host result in NOT RUN and block
the corresponding release. Successful fakes do not replace a live probe.
Explicitly document configured retention/storage conditions of both vendors.

## 7. NuGet Consumption and Publication Preparation — Q-05

```text
dotnet pack Monstertacle.Multisession.slnx -c Release --no-build -o artifacts/packages -p:PackageVersion=0.1.0
```

`pwsh -NoProfile -File eng/PackageSmoke.ps1` creates a fresh consuming sample application
outside the product source tree, restores exclusively the generated library packages
and their approved NuGet dependencies and executes first message/follow-up request.
No Source/ProjectReference to foreign repositories; no already existing installation
as a hidden prerequisite. Check package version and dependencies for completeness.

The embedded-consumer scenario proof uses exclusively public contracts and
a provider implementation defined in the consuming test project. It is the first
integration capability proof for the long-term target consumer SharpClaw, but no
claim that the productive application has already been migrated. No source reference
to or installation of this target consumer is required for the proof.

For the ValidationHost, check start/publish variants per TFM/RID without installing a service:

```text
dotnet publish examples/Monstertacle.Multisession.ValidationHost -c Release -f net10.0-windows -r win-x64 --self-contained false
dotnet publish examples/Monstertacle.Multisession.ValidationHost -c Release -f net10.0 -r linux-x64 --self-contained false
```

Publication on nuget.org, production deployment, Windows service installer and
Linux service units are explicitly not part of this planning phase or these commands.

## 8. Release Record

Record Q-01 to Q-06 separately with source revision, platform, versions, result and evidence file.
Additionally check SC-006 by an integrator without knowledge of the source code:
after the prerequisites have been provided, first message and follow-up request in at most 30 minutes.
Cases not executed remain visible. Metrics, successful builds or documentation
alone do not replace behavior, crash, package or vendor acceptance.
Q-06 additionally requires the actually observed Red reason before Green for each new
behavior change. In the current planning state, all Red/Green evidence is NOT RUN.

## Current Evidence Scope

The 14 finite non-live scenarios are executed locally and in native CI also from a
framework-dependent publish copy outside the source tree. `soak` additionally executes
an actual monotonic hour with 60 original receipts, one in
each minute window, a subsequent native reopen and a separate raw evidence checker.
Source/assembly hash and original IDs are part of the evidence; UTC buckets do not replace
a monotonic measurement. New product/test/build inputs require a new identical
candidate. Individual older checkpoints do not thereby become the final release.

`live-openai` and `live-gemini` are currently explicitly blocking NOT RUN reports
with Exit3/LiveApprovalRequired; the current host executes no live requests even when credential
environment variables are present. The concrete account/retention/cost order
and executable live acceptance only follow T105 after explicit approval. These names
are not presented as an already passed or complete live implementation.
Windows11 and an independent integrator are additional external acceptances.
