# Windows validation-scenario failures on pull request #20 (2026-11-03)

Part of `follow-up-capacity-probe.md`. This record covers what was observed, what was changed and what remains unknown. It
does not assume a shared cause with the earlier `AdmissionTimedOut` record.

## Observed (preserved)

Pull request #20 changed evidence and documentation only. Its head `85e0728` differs from the passing producer `efb3f20` only
by the release record and documentation. Windows quality failed in both runs:

- **Push run 41551010991.** All test suites passed. The published validation scenario `deterministic` then failed:
  `{"Status":"FAIL","Reason":"ScenarioExecutionFailed","Failure":{"Error":{"Code":16,...,"RequestId":null},"Details":null},"ExitCode":2}`.
  Its stderr was empty.
- **Pull request run 41551018970.** `PlatformTests.NativeScenarioOwnershipTests.ExecutesOfflineScenarioAndReleasesNativeState`
  failed for `embedded-consumer`: `Expected: 0 Actual: 2` after 9 s. The test asserted only the exit code, so the scenario report
  was printed to the console and not captured.

Unrecoverable:
- For `deterministic`, the exception type and throw site. `ScenarioExecutionFailed` with code 16 only says the exception was
  neither a typed product error nor a probe check.
- For `embedded-consumer`, the whole report.
- Both private scenario state roots were deleted by their cleanup.

## Changed (validation host and tests only)

- `ValidationFailure` keeps the generic public code for an untyped failure. It adds `ExceptionType` (the CLR type name) and
  `Site` (the first non-runtime frame as `Type.Method`, with async state machines unwrapped). It carries no message, path or line.
  Typed product errors are unchanged.
- `ScenarioRunner.RunAsync` can write its report to a supplied writer. The platform test passes a writer and includes the
  report in its failure message. Its requirement (exit code 0) is unchanged.
- No product, timeout, assertion, retry or threshold change.
- Red first: `ScenarioFailureAttributionTests` (category and site missing; the report was not written to the writer).
- Mutations: dropping `Site` or `ExceptionType` turns the test red.

## Not concluded

The cause of either failure is unknown. A fixed 5 s wait in the wave probe is one place an untyped `TimeoutException` could
arise. That is only a possibility named from the source, not an observation. The next failure of this kind will report its
type and site.
