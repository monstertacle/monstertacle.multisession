# Current source and architecture evidence
Created: 2026-10-30T01:21:40Z
Owner: Alexander Bleissem
Base: 4494f9a0749c76975418682113f19f7839340f54
Prior validated product SourceHash: d51bf9f9efd33341e34037a6a027cac887abb117111fa6afa2d56d66d6d37cbf

## Deep dive before implementation
RuntimePreparation.PrepareAsync composes RuntimeSessions, RuntimeRequests and RuntimeRecovery around SessionCatalog, RequestAdmission and RecoveryService. RuntimeComponents stores these three ports plus RequestDispatcher, options and statistics. SessionRuntime exposes those ports after RuntimeLifecycle preparation. RuntimeLifecycle owns operation tracking, startup/shutdown and workers.
RuntimeSessions.CreateSessionAsync and RuntimeRecovery.InspectRecoveryAsync/ResolveRecoveryAsync use RunOperation. Other methods preserve RequireReadable/RequireRunning. RuntimeRequests.SubmitAsync records accepted diagnostics and pulses workers exactly once.
ISessions has six operations, IRequests four, IRecovery four. Their ValueTask APIs and nullable lookup contracts remain compatible. SessionCatalog, RequestAdmission and RecoveryService own durable transactions and concurrency; CQRS must not duplicate them.
Callers: examples/ValidationHost, ContractTests startup/recovery/lifetime suites, IntegrationTests SQLite persistence and the external package-consumer fixture. Existing tests cover lifecycle, queued admission, held reservations and recovery revision CAS.

## Dependency evidence
Official NuGet flat-container package Monstertacle.SharpQrs 0.2.0-alpha.1 has MIT license, net10.0 and no dependencies. Published nuspec repository commit 554f4a02f23d20c2e0012d3420ff58f7370b4a62 matches the inspected sibling source. CommandDispatcher/QueryDispatcher register exact request/result mappings; they reject actual null results and forward tokens/exceptions. Registrations are instance-local and must be complete before concurrent dispatch.
Codemetr.CSharp.Cover.Tool 0.1.1 is pinned locally. Its default project discovery also sees ignored package-smoke copies, so discovery scope must explicitly exclude build artifacts, not product code. Existing coverage.runsettings excludes test assemblies, includes all own product modules; foreign platform coverage remains a separately required native check.

## Decision and alternatives
Use standalone SharpQrs asynchronous dispatchers; retain existing guarded ports as handler targets. Group RuntimeFacades and RuntimeDispatch to avoid increasing six-argument constructor coupling. Expose execution-only views so registrations cannot be changed after startup. Existing facades translate to typed requests; original ports execute once behind handlers.
Use a non-null LookupResult<T> envelope for nullable lookups and CommandAcknowledgement for local intent. Wrap SubmitRequest rather than changing its persisted domain shape.
Rejected: separate read store/event sourcing (no requirement), ASP.NET DI dependency (host-neutral library), synchronous blocking, a decorative bus bypassed by existing APIs, duplicated lifecycle checks and mutable global registries.

## Risks and test-first plan
Prove actual typed dispatch, unknown mapping rejection, non-null missing lookups, immutable registration exposure and isolated runtimes. Run all legacy tests after routing change. Handler tests cover all fourteen mappings and original token/error propagation. Native package smoke must allow SharpQrs from NuGet while own six packages remain local. Add Cover configuration/source hashing and gate checks without weakening existing 87%.
First Red: compile public contract stubs throwing NotSupportedException, then execute behavioral route tests. Archive HEAD, working diff, untracked sources, measured UTC and SHA256 before implementing handlers. Compile/restore failures do not count as Red.
No implementation-relevant architecture question remains open. Coverage-tool input/provenance compatibility will be investigated before claiming its gate.

## Observed shutdown compatibility finding
The first full native quality run on this working state failed RecoveryStopOwnershipTests.StopCancelsRecoveryWithoutPublishingLateEvidence(resolve: true): the outer legacy command Task was still incomplete after StopAsync. RuntimeOperations only tracks the original RunOperation task; SharpQrs and acknowledgement conversion add continuations beyond that task. Existing 241-contract baseline was green before routing. This is a deterministic contract and existing test, so no judgment-based role review is necessary.
Decision: register the final dispatcher Task under RuntimeLifecycle's existing gate and RuntimeOperations pending set before exposing it. This adds no cancellation policy or domain guard. Return void legacy ValueTask directly around Task<CommandAcknowledgement>, avoiding another untracked continuation. Lifecycle gate synchronizes registration with stop; existing guarded long I/O still cannot begin after stopping.
Test plan: exercise inspect and resolve through both typed and legacy routes; stop must finish their actual public tasks before cleanup, preserve unknown reservations and reject late publication. Archive failure and full pre-fix source under artifacts/tdd/sharpqrs/drain-pre-green.

## Measured coverage scope decision
The first complete Cover report measured all six libraries on Linux: 91.2% lines and seven CRAP findings, all in Windows-only native interop with zero execution data. These are not CQRS defects and cannot be exercised on Linux. The Constitution explicitly defines the native platform plus all shared product libraries, with mandatory opposite-native CI. Therefore the quality command derives a native Cover configuration from the checked-in common rules, excluding exactly the foreign platform source directory. It retains that path, its rationale and the whole six-library exploratory report in evidence; every product directory is measured in its matching mandatory native CI. No limit, baseline or unreported policy changes.
Cover's source-based line denominator differs from Coverlet's instrumented-line denominator; both metrics are reported separately. The original native whole-product 87% gate remains mandatory. Native Cover must independently return complete=true, passed=true with defaults for diff/CRAP. Root-only manual Cover checks remain a stricter cross-platform exploration unless the native configuration from the quality run is selected.

## Static metric refactor
The full static check measured 476 files/495 types and found RuntimeLifecycle maintainability index 14 (minimum 15) and CqrsRuntimeTests coupling 42 (maximum 40). All 1047 behavioral tests were green beforehand. Extract the unchanged polling/dispatch loop from RuntimeLifecycle.WorkAsync to RuntimeWorker.RunAsync; lifetime fault handling remains in RuntimeLifecycle. Split token/error contract tests into their own class. This is a behavior-preserving responsibility split, with no threshold changes. Rerun all checks after refactoring.

## Packaging and evidence selection
Measured 2026-10-30T01:45:21Z on 7d9626e4db09e939330fddf674f0fc5b14557b0b: packing Common as stable 0.1.0 fails NU5104 because SharpQrs 0.2.0-alpha.1 is prerelease. Directory.Build.props, both pack workflows, PackageSmoke generated references and Test-PackageLayout pin the old own version; README and embedding instructions describe it. Decision: version the six unpublished own packages as 0.2.0-alpha.1 too, without suppressing the NuGet warning. No release publication. Validate rebuilt packages and an outside-repository locked consumer.
VSTest creates both GUID collector reports and byte-identical TRX In/ attachment copies, yielding eight Cobertura paths for four suites. Requiring exactly four raw files was incorrect. Select one byte-identical report group per suite and exactly one direct TRX per suite; distinct nonidentical reports fail rather than being silently omitted. Every suite remains mandatory. Earlier glob-driven Cover input rejection is retained as an observation; explicit per-suite evidence selection is validated by complete Cover runs rather than an unproven upstream-root-cause claim.

