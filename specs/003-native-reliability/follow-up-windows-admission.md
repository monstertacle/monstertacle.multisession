# Native default-budget admission variability

Owner: Alexander Bleissem
Status: ready; no production latency or historical root-cause claim accepted
Origin: PR run41054347449 at head98948da, source f4d2c1889c44f2978fff81b3cdbb8bc286c787d31b8d048042689d1aa481b490;
routing/deterministic AdmissionTimedOut at AdmissionCommit with Unknown outcome.
Related release work: feature001 T099 and its required real native host evidence.
The failed runner's original stores were not retained in standard artifacts;
their later candidate outcomes were not captured. Future controlled probes must
retain their owned state for lookup and cannot reconstruct that missing evidence.

## Bounded scope and next steps

1. Reproduce the default five-second public admission workload on actual supported
   Windows hosts and a Linux control using one exact package set. Use fresh private
   stores and a controlled provider; never resubmit an uncertain candidate.
2. Put measurement infrastructure outside the repository under
   ~/dev/e2e-harness-bundles. Record per-operation elapsed time, queue/store activity
   and appropriate native I/O evidence to distinguish waiting from transaction or
   commit latency. The existing stage alone cannot make that distinction.
3. Preserve each failure, candidate ID, later lookup outcome, raw trace provenance,
   runtime/SQLite/OS versions and fixture pressure. Compare instrumented and normal
   execution; do not infer a runner problem or a product race from variability.
4. Change runtime code only after an actual defect is isolated and a deterministic
   regression demonstrates it. Otherwise document the measured operating envelope
   and obtain the responsible performance/release decision for the default profile.

Dependencies: native Windows Server and required Windows11 host access for the
release matrix; offline controlled workloads need no provider credentials. The
Wave consumer's explicit thirty-second budget is a separate acceptance profile.

## Acceptance

Source/package/trace hashes and measured timings account for the observed expiry
or explicitly state the remaining uncertainty. Every timed-out candidate is looked
up without replay; no duplicate acceptance/send or invented completion is allowed.
Any isolated defect has a Red/Green regression and unchanged certainty/deadline
tests. Native default-profile observations and configured-Wave results are reported
separately. Open release blockers remain open in feature001; this follow-up is not
a substitute for a successful current Wave/native quality gate.
