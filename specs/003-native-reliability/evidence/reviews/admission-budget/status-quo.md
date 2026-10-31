# Opposed architecture review: defend the five-second validation budget

Role: defend the existing behavior and challenge a larger validation budget. Read-only source review; no tests, Python, source edits, or further agents.

Evidence: frozen manifest at this directory, head `98948da32fab08abd6b6e5734ee00946ec58478e`, supplied source digest `f4d2c1889c44f2978fff81b3cdbb8bc286c787d31b8d048042689d1aa481b490`. Independently ran `sha256sum -c manifest.sha256`: every listed evidence/source file matched, including RequestAdmission, WaveProbeFlow, spec001, spec003 and failed Windows log. Read the decisive admission, SQLite owner/transaction/work, host/workload, diagnostic tests, integration tests and specifications in full. Read applicable AGENTS instructions.

## Judgment

A larger Wave-only budget is a defensible host configuration for the functional dataset acceptance, but it is not an established fix for the observed Windows latency. I oppose presenting it as the latter, widening the SDK default, changing all ProbeHost callers, or silently weakening spec003's preservation promise. The status quo offers useful native evidence at the documented default and should remain the default everywhere outside the explicitly justified Wave workload.

The strongest argument for preserving five seconds is spec003 FR-002: preserve bounded queue, existing time limits and acceptance certainty. US1 and SC-001 additionally require the isolated starvation regression to pass without increasing deadlines or retries. The current Wave uses the real default and catches a real observable failure that a consumer choosing the default can receive. Removing that failure from this acceptance path loses that particular native latency observation even if every identity/FIFO assertion remains. Fake-clock deadline tests prove semantics under controlled time; they do not prove native completion within five seconds. Neither high coverage nor a later successful same-head CI run repairs that distinction.

## What the evidence proves

- `RuntimeDefaults.AdmissionTimeout` is five seconds. `RuntimeOptions.AdmissionTimeout` is a host option measured from submission, not dispatch or durable queue residence. The option validation accepts valid finite execution deadlines; a larger explicit option is part of the intended configuration surface.
- `RequestAdmission.SubmitAsync` creates one deadline across lookup and acceptance. It changes its diagnostic stage to AdmissionCommit before calling CommitAcceptanceAsync. On timeout it can return Unknown certainty with the candidate identity; it cannot infer where the store is spending time. FailureStage explicitly describes AdmissionCommit as waiting for the acceptance operation, not proof of transaction commit.
- `AcceptanceTransactions.CommitAsync` first queues synchronous work. `SqliteWorkQueue` has one LongRunning owner, bounded FIFO and asynchronous continuations. Transaction execution, durable commit, the queue and continuation scheduling are separate possible contributors to elapsed time. `SqliteTransactions.Run` checks cancellation before transaction start and before commit, preserving unknown commit certainty where needed. No reviewed evidence identifies the actual contributor in the failed runs.
- `failed-pr-windows.log` lines 229-242 records routing and deterministic failing with WaveAdmission_AdmissionTimedOut, Stage 8, Unknown admission and absent numeric native codes. The deterministic report also preserves a cleanup failure. Stage 8 is AdmissionCommit. Null native codes are not proof that SQLite or I/O was healthy, and the diagnostic does not establish physical commit latency. The 27-second and 24-second scenario durations include setup/work/cleanup and are not admission latency measurements.
- `failed-pr.json` and `passed-push.json` both identify this head. The push run passed both native jobs and canonical consumers; the PR Windows run failed its suite and skipped later consumption/published-host steps. These observations establish variability at the reported head, not an environmental root cause, a product race, or a latency distribution. Successful push evidence must coexist with the failure.

## Why my defense of the status quo is insufficient by itself

Spec001 SC-001 requires ten sessions and twenty requests with controlled cross-provider overlap; SC-002 requires one hundred requests, correct identities and per-session ordering; SC-003 requires the five/five failure isolation dataset. None requires native admission to succeed within five seconds. FR-008 expressly declines a general wait-time/starvation promise for scheduling, although that is not itself an admission SLA exemption. RuntimeOptions makes the host's acceptance deadline configurable. Keeping an incidental default as an extra requirement can prevent the test from reaching the dataset that it actually owes.

WaveProbeFlow explicitly starts a consumer host, submits these datasets, checks two concurrent sends, verifies every terminal identity/output, checks FIFO history/context and forbids missing/repeated sends. ValidationScenarioTests independently checks the persisted counts and reservations. Giving this consumer a finite admission option does not inherently bypass these functional checks. No return-before-durability, replay, unbounded wait, changed capacity or relaxed certainty follows from the option alone. It also leaves other callers on the default if the change is placed only in Wave, rather than ProbeHost's fallback configuration.

My position cannot establish that five seconds is the right performance threshold for this fixture on hosted Windows. The frozen evidence has neither per-operation timing nor a requirement making that threshold an acceptance SLA. Nor can it establish that thirty seconds is sufficient: that number would be an explicit engineering budget, not a measured native percentile or root-cause conclusion. Conversely, preserving the current fixture unchanged does not guarantee useful completion in the required CI environment.

## Smallest defensible action if the budget changes

Use the existing `selected` RuntimeOptions argument at WaveProbeFlow's StartAsync call to supply an explicit finite Wave admission budget, while preserving its current state directory, retention, two execution slots and every remaining option. Do not change ProbeHost's global fallback, RuntimeDefaults, SQLite busy timeout, request deadlines, shutdown deadlines, barriers, terminal checks, retries or timeout/uncertainty semantics. This confines the acceptance tradeoff to deterministic/routing/failures, whose common owner is WaveProbeFlow.

Document the decision as host-workload configuration: those scenarios prove their prescribed functional datasets under the explicitly configured admission limit. State plainly that they cease to be evidence that this dataset always admits at the SDK's five-second default. Reconcile that limited fixture change with spec003 FR-002 in the decision record instead of claiming literally that no time limit changed. The product default and isolated deadline regression remain preserved; this is the material distinction the record must express.

Retain the unchanged fake-clock deadline/diagnostic tests and the independently bounded 500ms starvation regression. Their passing evidence still has its original narrow meaning. Keep the observed native default timeout as an unresolved causal limitation with concrete ownership/acceptance disposition under the existing plan rules; do not relabel it an identified storage bug or an identified runner problem. A subsequent green native run with the explicit option may satisfy functional CI acceptance for that configuration, but it cannot retrospectively disprove the observed default-budget failure.

A targeted option-isolation check should establish the Wave selection and unchanged SDK/default ProbeHost configuration; existing full functional/state assertions should continue to run. The present review did not inspect a candidate implementation and therefore gives conditional architectural acceptance only. The existing five-second barrier and ten-second per-request execution/terminal limits can still fail for independent reasons; this proposal is not authority to widen them if that happens.

## Measurement


UTC start: 2026-10-30T12:43:14Z
UTC end: 2026-10-30T12:44:58Z
Elapsed: 104 seconds.
Tool calls through writing this review: 7 functions.exec orchestration calls containing 13 exec_command calls; 1 collaboration progress message (21 calls counting both layers). No test executions.
