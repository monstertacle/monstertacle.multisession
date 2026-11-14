# Callback fixture lifecycle review

Finding RT11-CALLBACK-LIFETIME-001, origin: native net11 quality attempts in
artifacts/quality/linux/07fc8e27005c446f96c46bae34d45c4c and
97ec881396324aa190a0780ff2b5bda8. Source parent ce974f8c435232852b3667dfed273826ff5982ad;
only targeting/test-host infrastructure had changed when these failures occurred.
Owner: current implementation session for Alexander Bleissem. Priority: P1 for the
new runtime qualification. Assignment: feature014 T007; historical observation and
remaining Windows-cause questions stay linked to feature008 T006.

Observed: the callback-entry wait timed out, first in StopCallbackDeadlineTests,
then StopIntentCancellationTests. The first diagnostic reported stopping completed,
worker cancellation requested and callback entry false. Focused net10/net11 reruns
passed; the complete instrumented net10 contract suite also passed. These results
alone establish neither a .NET 11 defect nor the historical cause of the timeout.

Frozen evidence was hashed before review at
.agenttemp/multisession-callback-review-20261114/SHA256SUMS. Separate opposing reviews
used that same set. Change advocate: 2026-11-14T10:07:55Z–10:08:30Z, six orchestration
calls/eleven nested tools. Status-quo defender: 2026-11-14T10:07:54Z–10:08:41Z,
six orchestration calls/ten nested tools. Both explicitly challenged their own
position and agreed that the fixtures did not guarantee their held-callback premise.
The source API reviews were corroborated by root's controlled installed-runtime probe.

Both provider fixtures signal readiness before constructing a cancellable infinite
delay. Cancellation can precede that delay; the already-cancelled await can complete,
allowing disposal to unregister an unstarted callback before CancelAsync invokes it.
A private deterministic control registered a gate callback last, waited for its
entry, disposed the earlier registration after the cancelled delay, then released
the gate and awaited cancellation. Installed runtimes 10.0.14 and 11.0.0 both returned
PASS with cancellationRequested=true and callbackEntered=false. This establishes a
sufficient mechanism on the installed binaries, not its occurrence in prior failures.

Decision: retain the provider's registration with `await release.Task` until the test
releases it. Preserve callback entry, cancellation-request, error/certainty, deadline,
lease and cleanup assertions and both callback variants. No product cancellation
code or timeout changes. The trade-off is deliberate test-controlled provider
completion; these held-callback scenarios do not independently prove callback-task
retention after provider completion. Other cooperative-cancellation contract tests
remain unchanged. Full native gates and focused cleanup checks must pass before
this qualification finding can be closed. Historical Windows causality is not closed.
