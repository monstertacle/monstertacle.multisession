# TDD evidence (feature 009)

All runs on Linux (local, `run-capped.sh -m 3G -s 1G`), .NET SDK 10.0.403. Logs in this directory; paths normalized.

## Behavior tests

- Red, measured 2026-11-01T12:14:33Z: tests written against a signatures-only skeleton (methods returned defaults, no
  refusals, no persistence; built with `-p:NoWarn=CA1822` only because skeleton members touched no state).
  Command: `dotnet test tests/Monstertacle.Multisession.Delivery.Tests -c Release --no-build`.
  Result: 74 tests, 71 failed (`red-skeleton-tests.txt`). Reasons are semantic: expected refusals not thrown
  ("No exception was thrown"), wrong state values, missing receipts, missing files. The three that passed exposed two weak
  tests, which were tightened before Green (the lease test now asserts the lock file exists; the initial-position test now
  creates at 4 and reopens with 9). `ToExpectation` was written with the record and is covered by an existing-behavior test.
- Green: after the implementation commit, same command, 74/74 passed (`green-implementation-tests.txt`), re-run green after
  the naming-rule refactor.

| Requirement | Tests |
| --- | --- |
| FR-003 format, FR-004 opens | CurrentFormatTests, OpenTests, CorruptionTests |
| FR-005 private state, lease | PrivateAccessTests (native branch per OS), OpenTests.SecondOpen… |
| FR-006/007 transitions, bounds | TransitionTests, BoundsTests |
| FR-008…013 grants | ResendAuthorizationTests, AttemptGrantTests, RouteMoveTests, CurrentGrantTests, AtomicityTests |
| FR-014/015 cancellation, crash | CancellationTests, OpenTests.CrashAfterSending…, CurrentFormatTests.Uncertain… |

## Packaging

- Red: with the project registered, `eng/Test-PackageLayout.ps1` refused the eight packed packages ("Expected exactly seven
  library packages", `red-package-layout.txt`). Green after registering the package (`green-package-layout.txt`).
- Isolated consumer `eng/Test-DeliveryConsumer.ps1`: first run failed on nullable warnings in the consumer itself (fixed in
  the consumer source); then PASS with nine conformance cases against the exact packed bytes (lock content hash verified).

## Fixture provenance

`fixture-harness/New-CurrentListenerFixture.ps1` verified the release `bundle-release.json` commit
`2c4a8af153f866d09bfb564fa88d4ac2856b20ec` and the hashes of the Listener, Protocol, core and SQLite assemblies, then ran
the release against private configurations, a fixture feed process and a private Unix-socket RPC fixture in a fresh
`~/dev/.agenttemp` directory. Every Listener invocation, exit code and output is in `oracle.json` (`transcript`). The
resulting checkpoints were merged from WAL and dumped (exact DDL text and rows); SQLite files are not committed per the
repository `.gitignore` rule, their hashes are recorded in `oracle.json`.
