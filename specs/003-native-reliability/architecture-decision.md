# Architecture decision after opposed reviews
Owner: Alexander Bleissem
Status: accepted for implementation, not release
Evidence: frozen manifest e9e392b4dc94e05d16d1fdd1e52e70e8086376ffaacd9799eb4d66efc23c33fe; both original reviews and narrow delta reviews in evidence/reviews/.

Root checked the underlying queue/CAS, serialization, error reconstruction, actual probe owners and native project selection. This decision supersedes the preliminary candidate in analysis.md.

| Topic | Decision | Reason / acceptance |
| --- | --- | --- |
| SQLite executor | Keep the bounded synchronous owner inside existing SqliteWorkQueue | Both roles support the measured narrow scheduling correction. Add enqueue/dispose races and multi-store lifecycle coverage. No IExecutor or shared scheduler configuration. |
| PublicError.Details and codec modifier | Reject and restore unchanged public error value and StoreSerialization | Safe operational details belong on the two existing exception types. Keep old constructors, equality and persisted JSON unchanged. New real SQL/admission/CQRS tests use exception details. |
| StorageFailureDetails class | Remove; mapping stays within existing StorageFailurePolicy | Same authority; no independent responsibility requiring another type. |
| ProbeExecution | Remove with prototype-specific tests | A generic delegate/resource wrapper does not own acquisition or all failure paths. |
| Probe lifecycle | Existing ScenarioRunner, WaveProbeFlow and ProbeHost own their exact resources explicitly | Preserve visible primary failure; cleanup-only is FAIL with no primary; append every visible cleanup failure in order. No replacement resource framework. |
| ValidationFailure | Validation-internal immutable report payload only | Error and diagnostic details must stay paired for each cleanup item. A closed pure projection is part of this payload, not an execution abstraction. |
| Composition testing | Real host assembly via named friend test assemblies | Unit fakes test failure precedence without claiming native coverage; matching multi-target PlatformTests exercise actual Linux/Windows StateRoot. No library -> host or test project reference. |
| SharpClaw | Separate current package/adapter contract proof | Core backend interfaces unchanged at 8ca4862. No copied host policy or new backend kernel without a red demonstrated gap. |

The scheduler reproduction does not identify the historical Windows cause. Full static metrics have incomplete semantic references; real builds and native full coverage remain required. Earlier Reds/Greens prove the rejected candidate only; the changed exception/report contract requires new observed Red before Green.

Root will disposition every reviewer finding against tests or concrete follow-up work before closing. Initial failed probes are not discarded because later runs passed.

Final source check of ValidationReport.WithCleanup and
ValidationProbeException.WithCleanup identified one remaining boundary case from
both delta reviews: a nested cleanup-only probe has no primary, so projecting it
as a new generic primary cleanup item would invent an extra failure. Keep its
existing ordered items exactly once. Add a regression through the actual scenario
owner and through startup's existing exception boundary before changing these two
payload methods; no new abstraction or ownership change is needed.

Root implementation review: the existing queue owns connection access and final
disposal; its lock prevents enqueue/close races while the existing work CAS owns
queued cancellation. There is one dedicated thread per open store, not a global
ThreadPool setting. Known failure reconstruction preserves optional details;
numeric SQLite/OS information stays separate from public error value semantics.
The original constructors and stored JSON are unchanged. Actual scenario/wave/
startup owners attempt cleanup explicitly; nested cleanup-only payloads contribute
only their real ordered failures. New literal phase values are closed enums,
not provider-name dispatch. No executor interface, runner framework, reverse
SharpClaw dependency or policy copy was added. Root checked source and tests;
the two agent reports reviewed the frozen candidate and decision delta, not this
final implementation. Native Windows and complete coverage acceptance remain open.
