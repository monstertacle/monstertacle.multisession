# Bounded native reliability closeout

Owner: Alexander Bleissem
Status: done for this implementation; full release governed by feature001
Source: 8d25c9a067989110c00327af988fff8969c35fb601b8100d0e6f07a224f254e9
Implementation head: 636a11000aee198678506f99f74edc1a71dcb082
Measured completion timestamps: RecordedUtc in the two native receipts below.

## Acceptance evidence

Both push41059372988 and PR41059379755 completed all four quality/canonical-
consumer jobs successfully. Static runs41059373151 and41059379746 passed both
operating systems. The complete receipts, including package-byte hashes,
published payload/scenario manifests and source identity, are
evidence/wave-budget-native-push.json and evidence/wave-budget-native-pr.json.

| Native PR result | Linux | Windows |
| --- | --- | --- |
| Tests passed / skipped | 1086 / 0 | 1092 / 0 |
| Complete native product lines | 97.7735% | 97.7556% |
| Complete expanded Cover lines | 95.5% | 95.6% |
| Cover complete / passed | true / true | true / true |
| Coverage findings | 0 | 0 |
| Published offline scenarios | 14 passed | 14 passed |
| Native producer package consumer | passed | passed |
| Identical Linux-produced package consumer | passed | passed |

Both independent line gates retain 87 percent. Expanded measurement includes the
whole host; test fixtures do not inflate the product denominator. Full static
CLI measures 489 files/509 types without violations or skipped source; its TPA-
only semantic-reference limitation is retained, with successful real native
builds/analyzers providing compilation evidence. The SDK default remains five
seconds, Wave explicitly uses thirty seconds and all other existing bounds and
independent functional assertions remain unchanged. PR3 is ready for review.

## Finding reconciliation

| Finding | Disposition and evidence |
| --- | --- |
| Idle SQLite executor waits for unrelated managed workers | Existing queue owns a bounded LongRunning worker; executable starvation Red and unchanged 500ms regression, lifecycle/race tests and full native suites. |
| Storage/admission details lost across reconstruction/CQRS | Closed exception-local metadata; real SQLite/timeout/cancellation and typed runtime tests. Old constructors and PublicError/store serialization remain unchanged. |
| Primary failure lost during cleanup or fake nested cleanup item | Actual scenario/wave/startup owners preserve ordered safe payloads; ownership/nested-cleanup Reds, real host assembly tests and native scenarios. |
| Proposed generic runner and PublicError/codec extension | Rejected after the first opposed reviews; removed from implementation, retained only as historical evidence. |
| Linux host contaminates Windows unit coverage | Matching native test targets and strict unchanged inventory; observed native Red followed by native product gates passing. |
| Eight unmeasured changed host files | Entire host included, additional validation report required, real instrumented/native/worker tests added. Both complete Cover gates now pass; no baseline, threshold or diff narrowing. |
| Host coverage might rescue an under-covered product | Separate denominator retained; synthetic 86%-product/100%-host case fails, exactly87 passes, missing/foreign assemblies fail. |
| Reserved xUnit metadata class name | Renamed to WorkerConsoleIsolation; no analyzer suppression, native builds warn zero. Compile error was not counted as behavior Red. |
| Local smoke feed contains old0.1 and current0.2 packages | Guard correctly rejected the mixed output; fresh private feed passed all six current packages. Clean native CI plus canonical consumption passed. |
| Same-head native Wave failure at default five seconds | Failed PR41054347449 is retained with successful counter-run41054338718. Two fresh opposed roles approved explicit Wave-only finite profile; root decision, pre-Green state and new native passes are retained. This does not identify the underlying cause. |
| Default-budget variability / historical Windows latency | Ready owned follow-up-windows-admission.md specifies source, scope, measurement, dependencies and acceptance. Feature001 T099 and real Windows11 release requirements remain open. No historical root-cause fix or default-budget hundred-request guarantee is claimed. |
| Actual SharpClaw process-wide cleanup and incomplete backend equivalence | Bounded source/package transport proof in feature004; its owned follow-up.md retains full host, late-ID, cancellation and live/migration work. |

The two opposed review rounds use separate conclusions over frozen common
inputs and explicitly state weaknesses. Root checked the decisive source rather
than accepting role conclusions alone. Current changes preserve certainty,
identity, FIFO, native ownership and backend neutrality; no new executor/runner
framework or production reverse dependency was introduced.

This closes the bounded feature003 work and its automated acceptance tasks.
Feature001's remaining release matrix/owner/live evidence and feature004's
explicitly unsupported integration cases are not relabelled complete.
