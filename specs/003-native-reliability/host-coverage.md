# Complete host measurement

Owner: Alexander Bleissem
Status: in_progress
Source: c31c1c87ec61ae56bdab6a0633f707df496aa66a; native measurement receipts in evidence/native-ci.json.

The owner requested autonomous implementation and correction while retaining the
87-percent gate. Root selects additional complete validation-host measurement.
The existing product inventory, native platform selection, diff base, thresholds
and absence of a baseline remain unchanged. The full static check retains tests
and host source.

Source analysis: tests/coverage.runsettings currently instruments the six product
libraries; codemetr-cover.json excludes examples. UnitTests and PlatformTests now
reference the actual matching host assembly. ScenarioRunner owns native state and
dispatches fourteen bounded offline scenarios plus the separate actual hour soak.
RecoveryProbeChild starts the assembly containing its implementation, so an
instrumented host reference can also supply owned child execution. No production
execution wrapper or new adapter abstraction is required.

scripts/Test-Coverage.ps1 owns the strict src-project inventory and complete native
product threshold. It must accept only explicitly registered additional validation
assemblies without counting test fixtures or host lines as shipped product lines.
It must require that declared validation assembly to be present. Codemetr Cover
then independently checks the expanded complete source inventory including all
examples, counting unreported source as zero. Both gates retain 87 percent; no
source is moved or excluded to pass.

Test sequence: first observe synthetic guard failures for a co-collected declared
host and for its missing report; retain the unchanged rejection of foreign modules
and below-threshold products even when host data is present. Archive the pre-Green
state. Expand collection, registration and full Cover scope, then measure before
adding tests. Exercise real native scenario owners with status, state creation and
lease-release assertions. Use measured uncovered members for remaining meaningful
boundary cases. No live credentials and no fake short replacement for the hour soak.

Initial expanded measurement: run4c5e8c20f77e43ed8cc12c7d764c3de9 measures
4053 executable lines, 76.7% coverage and 25 findings, including CRAP defaults.
The eight unmeasured changes are gone. ValidationHostBuild.WorkerPath currently
always selects the examples build, so existing successful subprocess tests do
not record the instrumented test-output host. IntegrationTests will follow the
already verified native UnitTests/PlatformTests target pattern and reference the
real host output; its existing protocol compile links remain internal test-side
contracts. The shared test fixture will prefer the complete local executable
when present, retaining repository-build fallback for older isolated consumers.

CrashRecoveryWorker's deliberate hard kills cannot flush in-memory collector
hits. Retain those actual kill tests unchanged. Separately test valid crash
preparation boundaries with unavailable protocol output, verifying failure and
persisted preparation state. Console redirection is confined to a nonparallel
native test collection and restored in finally; no product hook is introduced.

Acceptance: complete local and native Windows/Linux suites, both 87-percent gates,
zero unmeasured changed host files, full static CLI, package consumption and actual
published-host scenarios pass for one source hash. Review-ready PR status follows
that evidence. Original historical Windows causality and the explicitly bounded
SharpClaw integration claims remain governed by the existing plans.

## Local verification

The two synthetic guard Reds were observed before the pre-Green archive in
evidence/tdd/host-coverage-*. The final behavior tests also prove that 100-percent
host coverage cannot rescue an 86-percent product result, missing host reports
fail, and foreign platform assemblies remain rejected. No baseline or threshold
changed. The examples exclusion was removed from the full Cover scope.

The pre-Green unified patch is stored as tracked.patch.gz to preserve its exact
context whitespace. Decompress it into a private directory when checking the
original tracked.patch digest; the original manifest is unchanged.

Fourteen actual offline scenarios assert successful CLI receipts and native lease
reacquisition. Six crash preparation points assert signal-output loss preserves
two sessions and the correct number of durable requests; two ownership cases
assert EOF rollback, incompatible scope refusal and lease release. The xUnit
console-isolation metadata was named WorkerConsoleIsolation after the ordinary
reserved-collection-name analyzer rejected its initial name; no suppression was
added and that compile error is not counted as behavioral Red.

Final local receipt: evidence/host-coverage-linux-final.json, source hash
f4d2c1889c44f2978fff81b3cdbb8bc286c787d31b8d048042689d1aa481b490.
All 1086 tests passed without skips; locked restore/build/format and audit passed.
Product coverage is unchanged at 97.7735%. Complete expanded Cover passes with
95.5% line coverage, 82.7% branch coverage, 95.6% diff line coverage, zero findings,
zero unreported code and no unmeasured changed files. The initial/final full
reports are archived as gzip files. Full static CLI passes 489 files/509 types;
its existing semantic-reference limitation remains disclosed in the receipt.
Native CI, package consumption and published scenarios remain the next acceptance
step before marking the PR ready.
