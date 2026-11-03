# First attributed Windows admission timeout (pull request #22, 2026-11-03)

Part of `follow-up-windows-admission.md`. This record keeps the measurement, the inference and what is open separate.

## Observed

Push run 41683919153 at `8054481` failed `quality (windows-latest)`. The pull request run 41683979706 on the same head passed
Windows quality. Only one case failed:
`PlatformTests.NativeScenarioOwnershipTests.ExecutesOfflineScenarioAndReleasesNativeState(scenario: "capacity")`. The report
was kept in the failure message by the attribution added in pull request #21:

```
{"Scenario":"capacity","Status":"FAIL","Reason":"AdmissionTimedOut","RunId":"d719a94b-38b2-46dc-8fbb-fa4b350bc763",
 "Failure":{"Error":{"Code":24,"Certainty":0,"SessionId":"a744e8d6-4e5e-4f05-a605-3d3adfa752fa",
 "RequestId":"eba8c519-136b-4f24-9f1a-36d6944d2819","AdmissionStatus":0},"Details":{"Stage":7}},"ExitCode":2}
```

Decoded from the source enums:
- code 24 is `ErrorCode.AdmissionTimedOut`;
- stage 7 is `FailureStage.AdmissionLookup`, the session read before any commit;
- admission status 0 is `AdmissionStatus.NotAccepted`;
- certainty 0 is `NotStarted`.

The capacity probe uses the default admission budget of 5 s (`CapacityProbeFlow.cs:17` sets no `AdmissionTimeout`;
`RuntimeDefaults.AdmissionTimeout`).

## What this establishes

- The candidate `eba8c519…` was not accepted, so no outcome is uncertain. Nothing needs a lookup, and nothing may be replayed.
- On the hosted Windows runner, the prerequisite session read of one admission took longer than the default 5 s budget.

## What remains unknown

- Whether the time went to waiting for the store's serial worker or to the read itself. The outer deadline cancelled first, so
  no store stage was reported.
- The scenario's private state root was removed by the test's cleanup, so it cannot be inspected.
- The runner-level cause.

No timeout, assertion or product code was changed. The run was not repeated to obtain green.
