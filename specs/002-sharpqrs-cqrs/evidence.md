# Execution evidence
Owner: Alexander Bleissem
Updated: 2026-10-30T01:55:50Z

## Test-first record
Before routing refactor, all 241 existing contract tests passed.
Five new CqrsRuntimeTests compiled and ran with the public contract stubs; all five failed because SessionRuntime.Commands/Queries threw NotSupportedException. This is the observed behavioral Red, not the earlier IDE0005 compile failure.
Pre-Green archive: ignored artifacts/tdd/sharpqrs/pre-green (base HEAD, UTC, tracked binary diff, complete untracked source tar); artifacts/tdd/sharpqrs/sha256.txt binds the archive and Red TRX/console output.
After implementation seven CQRS tests passed, covering all fourteen registrations, nullable lookup envelopes, explicit recovery resolution, cancellation, unknown mappings, immutable execution views and independent runtime state.
The initial error assertion incorrectly expected InvalidConfiguration for an absent profile; inspection of ProviderCatalog confirmed the existing contract is NotFound. The test was corrected to preserve that contract; no domain behavior was changed.

Source-hash extension: the regression test failed with "Source hash omitted package input: codemetr-cover.json". Archive in artifacts/tdd/sharpqrs/source-hash-pre-green. Adding the config to the source inventory made all five source-hash tests pass.

## Coverage setup
Repository-local Cover 0.1.1 installed through the existing tool manifest; static tool remains 0.1.0. Doctor reports exactly four xUnit/VSTest projects with coverlet.collector.
Root config excludes artifacts/** (generated package-consumer copies and measurement outputs) and examples/** (validation executable, outside the existing library product denominator). Tests are automatically excluded by the tool. Each native quality run derives cover-native-config.json excluding exactly the opposite platform, required in its matching native CI. All common product sources remain measured, unreported code counts zero, and the original native five-module 87% gate is also retained. No baseline, threshold lowering or unreported-ignore mode.
Cover reads the same fresh per-suite Cobertura and TRX files as the original gate, checks completeness and test results, and retains default diff/CRAP limits.

The whole six-library Linux exploration is retained in evidence/cover-cross-platform-observation.json: complete=true, line 91.2%, seven CRAP findings, all unausführbares Windows-Interop. The matching native configuration passed: complete=true, 216 files, 220 types, 447 members, source-based line coverage 97.7%, no unreported members, zero findings; existing instrumented-line coverage 3051/3121 = 97.7571%. The final refactor is being revalidated.

## Static metrics and shutdown
The full static check after extracting RuntimeWorker and splitting failure tests passed: 478 files, 497 types, zero violations, zero parse errors, zero skipped. Existing platform-reference-only semantic-model limitations of static tool 0.1.0 remain; the real solution build is the compile verdict.
The expanded shutdown regression first failed 1/4 cases, then all 250 contracts passed after tracking final public tasks. Token and original exception identity tests subsequently raised contracts to 252, with all 1047 native tests passing before the final responsibility-only refactor.

## Acceptance
Final local full quality run on b174a48 passed all 1047 tests (359 unit, 252 contract, 405 integration, 31 native platform), locked restore, build, format, audit and coverage. Native instrumented line coverage: 3053/3123 = 97.7586%. Source-based Cover: complete=true, passed=true, 217 files/221 types/448 members, line 97.7%, diff line 99.1%, diff branch 100%, zero findings and zero unreported members. Raw warnings remain in evidence/local-validation.json.
SourceHash: 1c90c394e617eb4e9f23858735e2fb25bf8266dbb9056cd868939aa8d53c3213. Local and Ubuntu CI run 40987921024 match exactly. Static guards passed natively on Linux and Windows for b174a48.
All six own packages 0.2.0-alpha.1 packed without warnings. The isolated package-only consumer passed two sessions, five requests and five persisted reopened results; its locked dependency graph resolves SharpQrs 0.2.0-alpha.1 from NuGet. Local manifest: artifacts/package-smoke/multisession-package-smoke-4f4b2d46e2a24658bfc9bb60bbd959c6/manifest.json.
Native Windows quality and both canonical-consumer jobs passed in [run 40987921024](https://github.com/monstertacle/monstertacle.multisession/actions/runs/40987921024). Windows: 1053 tests, zero failed/skipped; native instrumented coverage 3114/3187 = 97.7094%; source-based Cover complete=true, passed=true, 97.6% line coverage, 99.1% diff line, 100% diff branch, zero findings. Both platforms have the exact SourceHash above. Static guards passed in [run 40987921018](https://github.com/monstertacle/monstertacle.multisession/actions/runs/40987921018). Native summaries and report digests are in evidence/native-ci-validation.json.
Feature001 historical checks do not validate this extension. Its live-provider, Windows 11, independent-integrator and release decisions remain open in feature001; no new release claim is made.

## Self-review and finding disposition
Reviewed responsibilities and contracts: existing guarded ports own business decisions; messages/handlers only translate; registration is private, exact and instance-local. Existing durable DTOs and schema are unchanged. No new provider-specific branch, retry, sync-over-async, service locator, global mutable registry, duplicate transaction or magic scheduling value. The polling loop reuses RuntimeDefaults. All public additions have XML documentation and compile with warnings as errors.

| Finding | Disposition and verification |
| --- | --- |
| Public recovery Task incomplete after stop | Final dispatcher tasks tracked under lifecycle gate; expanded typed/legacy inspect/resolve test passed in the full suite. |
| Lifecycle maintainability and test coupling | RuntimeWorker extraction and separate failure tests; full static check zero violations. |
| Stable packages depend on prerelease | Six unpublished own packages use 0.2.0-alpha.1; pack and locked outside consumer pass, NU5104 not suppressed. |
| Foreign Windows CRAP on Linux | Explicit native scope required by Constitution; common libraries always included, opposite platform mandatory in its native CI; original cross-platform observation retained. |
| Duplicate attachment reports / input discovery | One distinct SHA256 report group and one TRX required for each of four suites; complete local and Ubuntu gates pass. No retry or partial-report fallback. |

No new unresolved implementation or CI finding remains. Existing external release follow-ups remain in [feature001 tasks](../001-agent-multisession/tasks.md) and its linked evidence; they are not silently closed here.
