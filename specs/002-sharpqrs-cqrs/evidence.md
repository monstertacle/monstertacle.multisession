# Execution evidence
Owner: Alexander Bleissem
Updated: 2026-10-30T01:39:03Z

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
Pending final full checks and native CI. Feature001 historical checks do not validate this extension. Its live-provider, Windows 11, independent-integrator and release decisions remain open in feature001; no new release claim is made.
