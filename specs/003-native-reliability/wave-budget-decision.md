# Explicit Wave validation budget

Owner: Alexander Bleissem
Status: accepted for bounded implementation; native verification pending
Reviewed head: 98948da32fab08abd6b6e5734ee00946ec58478e
Source hash: f4d2c1889c44f2978fff81b3cdbb8bc286c787d31b8d048042689d1aa481b490

The complete push CI41054338718 passed Linux/Windows quality, package smoke,
published scenarios and identical-package consumers. Same-head PR CI41054347449
failed routing and deterministic in Windows IntegrationTests with
AdmissionTimedOut, AdmissionCommit (stage8), Unknown acceptance/certainty and no
numeric native cause. Both observations are retained. Stage8 denotes waiting
for acceptance; it does not identify SQLite commit, I/O or scheduling as a cause.

Two opposed reviews, frozen together under evidence/reviews/admission-budget,
conditionally support explicit finite configuration only for Wave workloads.
The change role supports reaching spec001's functional datasets; the defending
role identifies loss of incidental five-second native Wave evidence and rejects
calling this a product/root-cause fix. Root checked the admission catches, queue,
transaction boundaries, defaults, Wave assertions and spec001 SC001–004 directly.

Decision: use the existing selected RuntimeOptions parameter at Wave's existing
ProbeHost.StartAsync call. Configure thirty seconds for admission in these three
functional batch scenarios only. Thirty seconds is an engineering budget within
the existing finite process envelope, not a measured performance percentile.
Keep all session/request counts, two-provider overlap, identities, outputs, FIFO,
terminal certainty, reservations and independent persisted-state assertions.
No replay, pass-on-timeout, new helper, factory, interface or product change.

Spec003 FR002 is explicitly scoped here: public SDK/runtime limits remain fixed;
the Wave consumer now selects a separately documented supported configuration.
The default five seconds, shared ProbeHost fallback and all other callers remain
unchanged. Fake-clock deadlines, caller cancellation, ten-second execution/stop,
five-second overlap and isolated 500ms scheduling regression remain unchanged.
Other native host scenarios and native package consumers still exercise defaults;
they do not restore the lost default-budget hundred-request dataset observation.

The existing failed native scenarios are the observed behavioral Red. Archive
pre-Green state, inspect a narrow source diff, run full local/static/native gates,
published workloads and identical package consumers again. The configured option
must remain local to Wave and both 87-percent gates must stay unchanged. A further
timeout is not authority to widen another limit: retain evidence and investigate.

The unresolved default-budget latency observation and historical Windows cause
remain owned by follow-up-windows-admission.md and feature001 T099. This decision
does not grant release acceptance for that historical requirement.

Implementation check: only Wave's selected options changed. Source comparison
confirms the complete src tree, shared ProbeHost, StoreSchedulerProbe, diagnostic/
deadline tests and independent ValidationScenarioTests are unchanged from the
reviewed head. Local complete quality passed all 1086 tests and both coverage
gates; the receipt is evidence/wave-budget-linux.json. The full static check
passes 489 files/509 types with its existing semantic-reference limitation.
The precise pre-Green patch is compressed under evidence/tdd/wave-budget-pre-green;
decompress privately before verifying the original patch digest. Native evidence
for this changed profile is still required.
