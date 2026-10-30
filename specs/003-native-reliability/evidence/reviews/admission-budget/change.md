# Opposed architecture review: case for the narrow change

Role: advocate necessity of a narrowly scoped change; independent conclusion with weaknesses below.
Reviewed head: 98948da32fab08abd6b6e5734ee00946ec58478e.
Reported complete source digest: f4d2c1889c44f2978fff81b3cdbb8bc286c787d31b8d048042689d1aa481b490.
Frozen evidence: source/, evidence/, manifest.sha256 in this directory. Independently ran sha256sum -c on every manifest entry: all 23 passed, including ProbeHost.cs, RequestAdmission.cs, both specifications and the failing Windows log. This verifies frozen file integrity, not a fresh computation of the complete repository source digest. No tests rerun; no source edits, Python, or further agents.

## Recommendation

Give only WaveProbeFlow's bounded deterministic/routing/failures host an explicit finite 30-second admission budget. Pass selected RuntimeOptions at its existing ProbeHost.StartAsync call, retaining its current StateDirectory, explicit retention and MaxConcurrentRequests=2. Keep ProbeHost's shared default path and SDK RuntimeDefaults.AdmissionTimeout=5 seconds unchanged. No generalized options abstraction or storage/product correction is justified by this evidence.

This is a validation workload configuration correction, not a demonstrated repair of the original Windows failure. A longer budget is a bounded candidate whose adequacy still needs native evidence. Do not call it a root-cause fix, claim the failed run was harmless, or replay an uncertain request.

## Evidence and contract reasoning

- ProbeHost.StartAsync currently constructs RuntimeOptions without AdmissionTimeout; WaveProbeFlow.ExecuteAsync uses that path. RuntimeOptions therefore inherits the SDK's five seconds. The existing selected-options parameter is sufficient; no new control flow is needed.
- WaveProbeFlow executes precisely the orchestration workloads specified by spec001 SC-001/002/003: 20/100/10 requests across ten sessions, provider overlap, identity/output, session FIFO and complete call counts. These success criteria contain no five-second successful-admission performance guarantee. SC-004's two-second cancellation/execution-timeout observations and ten-second Stop bound concern other phases and must remain unchanged.
- The observed PR failure is concrete: evidence/failed-pr-windows.log lines 229-246 records routing and deterministic failures, AdmissionTimedOut, Stage=8 (FailureStage.AdmissionCommit), AdmissionStatus=Unknown, Certainty=Unknown and absent native codes. Deterministic also retains a separate cleanup failure. No SQLite busy error, OS error or storage mechanism is established. The diagnostic names the admission wait phase, not proof that SQLite's physical commit itself consumed five seconds.
- RequestAdmission.SubmitAsync starts a finite timer before lookup, creates a candidate ID before acceptance, waits for the authoritative operation and returns only a validated committed receipt. An unfinished commit at timer expiry remains Unknown unless nonacceptance is proven. Giving this existing mechanism more finite time does not change that safety rule, cancellation, queue capacity, duplicate handling, transaction atomicity, or reservation semantics.
- SqliteWorkQueue already has one LongRunning serial owner, bounded BlockingCollection and cancellation-before-start logic. SqliteTransactions preserves rollback/nonacceptance versus unknown commit certainty. These sources provide no evidenced additional executor/product correction. Their correctness is not proved by a later successful job.
- evidence/passed-push.json and failed-pr.json identify the same head but divergent native outcomes. The passing push job includes both native quality jobs and both canonical-consumer jobs. It does not erase the failed PR run. The local coverage/test figures are recorded in frozen host-coverage.md; I did not independently remeasure them.

## Spec003 obligation and test integrity

Spec003 FR-002 literally says preserve existing time limits; US1 and SC-001 expressly forbid increasing the isolated starvation deadline. That is a serious constraint, not a phrase to silently reinterpret. My proposed disposition is to explicitly document that SDK/runtime limits and existing deadline regressions stay fixed, while this separately identified Wave consumer intentionally selects a supported host configuration for orchestration acceptance. Root must record this bounded distinction before implementation. If FR-002 is intended to freeze every validation consumer's current configuration as well, the proposal conflicts with it and must not proceed under an undocumented exception.

Leave the isolated 500ms scheduler regression and fake-clock admission deadline tests unchanged. The frozen AdmissionDiagnosticTests.IdentifiesCommitTimeoutWithoutInventingNativeCause advances the configured admission timeout and verifies AdmissionCommit plus Unknown without invented native cause. Preserve this, caller-cancellation diagnostics, receipt-after-known-commit tests and transaction-certainty tests. The isolated 500ms regression is specified by the task but its source was not included in this evidence bundle; I have not independently inspected that exact assertion.

Keep all Wave counts, overlap barrier, FIFO/output/context checks, trace.Calls count, terminal certainty and durable dataset assertions unchanged. ValidationScenarioTests independently verifies persisted request/history/state counts and no unreleased reservations. Do not convert AdmissionTimedOut or cleanup failure into PASS, discard diagnostics, loosen coverage thresholds, replace native tests with fakes, or skip a scenario. Existing process-level bounds remain failure bounds, including the routing five-minute bound; a 30-second per-admission timer does not authorize extending the whole job indefinitely.

## Weaknesses of my own case

1. There is no measured latency distribution or causal explanation for the expiry; 30 seconds is a conservative finite configuration choice, not an empirically established repair. A pathological product slowdown could merely become harder to detect.
2. Five seconds is a published default. Running Wave at 30 seconds removes these particular scenarios' incidental exercise of successful native admission at the default. Deterministic deadline tests preserve deadline semantics but do not prove native default-budget throughput. The native failures must remain recorded as failures at the default, without overstating coverage of that operating point.
3. The existing Stage=AdmissionCommit can arise while awaiting queue/transaction/continuation work. Numeric error absence limits diagnosis; it does not establish healthy I/O or prove host scheduling as the cause.
4. Spec003's broad FR-002 wording could reasonably support the status-quo objection. The narrow harness/product distinction needs an explicit architecture disposition, not only a passing revised test.
5. Increasing only admission leaves ten-second execution/End/Stop and five-second overlap bounds intact. A broader infrastructure stall may still fail elsewhere, which should remain observable rather than trigger further speculative increases.

## Concrete verification and acceptance

1. Archive the present failed Windows evidence and the precise pre-change source/diff. Treat the observed routing/deterministic scenario failure as the existing behavioral Red; do not invent a configuration-only failure as evidence of a product bug.
2. Inspect the candidate diff: only Wave selected-options composition plus necessary decision/evidence documentation. Confirm SDK five seconds, fake-clock tests, 500ms starvation limit, provider barrier, execution/Stop/process bounds and all independent persisted-state assertions are unchanged.
3. Run the required relevant/full local suite and complete static check under the mandated capped scope; retain both complete product and expanded-host 87-percent coverage gates, source inventory, and zero unmeasured changed host files. Verify safe diagnostics and certainty tests still pass.
4. Require both native Linux and Windows CI jobs, identical package-byte cross-platform consumer jobs and actual published Wave workloads to pass for the candidate source. Record outcomes by head/run; do not substitute the older passing push run for current-head acceptance. Repeat the native bounded scenarios as required by the plan, without resubmitting an uncertain request in an existing state directory.
5. If expiry recurs at 30 seconds, retain failure/cleanup evidence and stop increasing deadlines speculatively. Investigate with an isolated external measurement or an owned follow-up plan; do not assert an I/O diagnosis from the existing phase alone. A recurring acceptance blocker prevents completion despite a follow-up plan.

No new actionable product defect is established beyond the retained native admission-timeout observation. Track that observation and the unproven latency cause explicitly in the parent disposition; the proposed consumer configuration must not silently close a historical causality claim.

Measured review start UTC: 2026-10-30T12:43:07Z
Measured review end UTC: 2026-10-30T12:44:59Z
Measured duration: 112 seconds (first tool timestamp through final write).
Tool calls: 8 functions.exec orchestration calls containing 13 exec_command calls, plus 1 progress send_message; no test runs.
