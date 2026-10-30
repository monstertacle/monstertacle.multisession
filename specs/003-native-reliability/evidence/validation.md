# Validation and remaining acceptance

Owner: Alexander Bleissem
Status: in_progress; no release acceptance

The chosen design is recorded in ../architecture-decision.md. Both original
opposed reviews and their narrow decision reviews are retained in reviews/.
The exception/report changes were retested after rejecting the original
PublicError/codec and generic ProbeExecution proposals. Those earlier prototype
Reds and Greens are historical evidence only.

## Observed behavior and provenance

Local pre-Green archives and executable results are under
`artifacts/tdd/native-reliability/`, with the chosen design's archives also retained
in `tdd/` beside this report. The SHA lists use the original artifact paths;
validate copied files against the same basename and content digest. The scheduler archive preserves the original
bounded worker-starvation failure. The final `review-delta-pre-green` archive
preserves the changed exception contract and concrete cleanup owners.
`review-delta-red/ownership-original-semantics-red.trx` records three behavioral
failures with original await-using semantics; the stub-only ownership failure
is not a reproduction of the original implementation.

The subsequent full Linux quality run is
`artifacts/quality/linux/8fdfc90e82e84d5abc104ca8e3043fd9`, SourceHash
`1a4de79b343079d81837916111b0ceb9cff6d1374d41135b64976026192a5850`.
Unit 366, Contract 252, Integration 407 and Platform 32 passed without skips.
Product line coverage was 3074/3144 (97.7735%). This is an intermediate snapshot:
two later regression tests passed separately (AdmissionDiagnosticTests 4/4,
AcceptanceFailureTests 3/3); the final aggregate must be rerun on their source hash.

Cover 0.1.1 completed its measurement but FAILED: eight changed ValidationHost
files were classified as `unmeasuredChange` because the existing product scope
excludes examples. This is not a passing quality gate. No exclusions, thresholds,
baseline, or final diff scope have been changed. A user decision on product-only
diff scope versus inclusion of the entire validation executable is pending.

The full static CLI measured 487 files and 506 types with no violations, parse
errors or skipped source. Its semantic references are incomplete, so structural
metrics do not replace actual successful builds. Both CLI versions remain pinned.

## Finding disposition

The final Linux candidate is recorded in `linux-validation.json`: SourceHash
`da86cc3e4a782a1f829141ea4a41620abc04e8ba04b7f9ad6cb8142a7c764d98`,
368 Unit, 252 Contract, 408 Integration and 32 Platform tests passed (1060 total,
no skipped tests). Restore, build and format passed with zero build warnings.
Product coverage remains 3074/3144 (97.7735%). Cover still has exactly eight
unmeasured-host violations, so the complete quality command exits 1.
Static structural check passes for 487 files/506 types; its 5559 semantic
diagnostics remain an explicit tool limitation. The nested-cleanup regression
first failed with an invented extra ExecutionFailed item; after the narrow fix,
all five actual-owner tests and the full native suites passed.

After PR3 CI exposed a Linux assembly in Windows coverage, UnitTests now follows
the existing native multi-target pattern and quality selects its matching target.
The actual Windows Red and pre-Green state are retained under
`tdd/native-test-target-*`. Both dependency graphs contain only their corresponding
platform library. The post-correction Linux receipt is
`linux-native-test-target-validation.json`, SourceHash
`c2118349915f323fb7dd9437de26d59a5eef13ed40656f8944e4b6666040a1c5`:
1060 tests passed, product line coverage 97.7735%, and only the same eight known
host measurement findings block the full gate. The corrected native Windows
run remains required; this local graph check is not its substitute.

| Finding | Disposition / verification |
| --- | --- |
| Idle store blocked by unrelated managed worker | Corrected inside SqliteWorkQueue; isolated scheduler regression and queue lifecycle/race tests. Does not prove independence of async host continuations or store startup. |
| Native failure evidence discarded during normalization/admission | Closed phase and numeric codes on existing exceptions; actual SQLite trigger/commit, admission and typed CQRS tests. PublicError and persisted format unchanged. |
| Primary failure overwritten during cleanup | Existing concrete owners preserve primary and ordered cleanup failures; actual host assembly ownership tests, including native PlatformTests target. |
| Preliminary generic helpers and public error/codec expansion | Rejected and removed after opposed reviews; decision records narrower responsibilities. |
| Unit project can select Linux host target on Windows | Platform-neutral unit cases only; native ownership cases reside in matching multi-target PlatformTests. Windows execution remains required under T010. |
| Historical intermittent Windows storage failure | Cause not established by scheduler reproduction. T010 owns repeated native scenarios and inspection of new safe details; original feature001 T099 remains open. |
| Cover unmeasured validation-host changes | T009 remains open awaiting scope decision and full rerun. No acceptance through a later unrelated green. |
| Current SharpClaw process-wide cleanup | Actual negative/isolated positive measurements in feature004; full integration has an owned bounded follow-up there. |

## Required next evidence

T009: resolve coverage scope explicitly. The existing Cover configuration already
uses fixed diff base `4494f9a0749c76975418682113f19f7839340f54`; the earlier concern
that clean CI would silently check an empty diff was incorrect. PR3 Linux CI
41041065004 reproduced all eight unmeasured-host violations after passing all
1060 tests and the product coverage threshold. A later green must not erase this
finding. Run locked restore/build/format, full
static check, native suites, coverage, audit and package proof for one final
SourceHash. T010: run native Windows CI and repeat failing scenario families;
retain recurrence evidence and use safe stage/native codes to investigate before
claiming the historical issue resolved. T011: commit/push, link exact native CI
results and review all findings. These are acceptance tasks, not waived by this report.
