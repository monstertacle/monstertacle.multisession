# Native published capacity probe investigation

Owner: Alexander Bleissem
Status: ready
Origin: push41065255148, head18a9f1d9140c62e752e961775283926854168608,
Windows published capacity scenario; PR41065265199 at the same source passed all
four quality/canonical-consumer jobs and fourteen published scenarios on both OS.
Source: 3683bf361f9b8930a13c68108fa1899d5ff3b7735979241307221c020f6040c5.
Evidence: evidence/worker-protocol-capacity/capacity.json and publish-failure.log.gz.

## Finding and boundary

The failed report says ScenarioExecutionFailed, ErrorCode.ExecutionFailed16,
Details null, no cleanup failures. It does not identify an admission expiry,
assertion, barrier, provider or store cause. Do not classify it as the earlier
Wave admission issue or an infrastructure flake. The native product/expanded
coverage gates and all tests passed in that push before publication. The separate
T015 worker CRAP gap is closed by the complete same-head native PR receipt.
Current evidence does not close published capacity reliability or full release.

## Assignment and next steps

1. Freeze exact current packages and run CapacityProbeFlow's unchanged workload on
   native Windows and a Linux control: ten sessions, two held running requests,
   eight simultaneous submissions, three queued accepts and five QueueFull refusals.
2. Use an external owned harness under ~/dev/e2e-harness-bundles; retain the failure
   class, boundary and elapsed measurements without raw exception text in reports.
   Keep original owned store, candidate IDs and later lookup results on failure.
3. Distinguish the five-second entry barrier, default admission deadline, ten-second
   execution/end/stop bounds and capacity/count assertions. No timeout broadening,
   assertion removal or automatic replay to make the probe green.
4. Isolate an actual cause before code changes. A production defect needs an observed
   behavioral Red and regression; a fixture defect needs a deterministic fixture test.
   Preserve existing native identity, certainty and product gates in either case.
5. Run fresh complete native quality plus all published scenarios and bind original
   failure and counter-evidence. Reconcile with feature001 release acceptance and
   follow-up-windows-admission.md only if evidence proves they share a cause.

Dependencies: actual supported Windows host/CI access; Windows11 remains required
for the release matrix. Offline controlled providers require no live credentials.

## Acceptance

Failure boundary/cause is measured or uncertainty explicitly retained as a release
blocker. Expected capacity, exact accepted IDs/results, QueueFull NotAccepted
refusals and finite call counts pass unchanged on both native OS. No candidate is
blindly resubmitted. Fresh complete native/published acceptance and applicable
regressions pass before this finding is closed; a retry alone is not a cause fix.
