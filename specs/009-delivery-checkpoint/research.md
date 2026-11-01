# Research: existing-code deep dive and architecture analysis

Measured start: 2026-11-01T12:00Z (`date -u`). Sources read in full unless stated.

## Source state

- SDK: `monstertacle/monstertacle.multisession` origin/main `5d4aa2fc693a43e3302cf50bd0ff8ecbbed7d435` (own full clone, branch
  `feature/009-delivery-checkpoint`).
- Host reference (read only, frozen review clone, never built): SharpClaw server `2c4a8af153f866d09bfb564fa88d4ac2856b20ec`,
  `src/SharpClaw.Listener`:
  - `Store.cs` sha256 `34295900faaa31a271f7885817697ddb2ecfa2ace57e7a7b870bba353a1988ab`
  - `PreparationRearms.cs` sha256 `2865f3a1e039924695ab5547c1660d512e03a7eb80f99528f3ecb963611d80e0`
  - `DeliveryReplays.cs` sha256 `f60fe47c92b105b77753d3be3fb7909a106eabd7c2a8ed6fb2e4994e231e8cbf`
  - `DeliveryRebinds.cs` sha256 `87a4dbe4e67f42559a581a9643befbc28413e7baf0ab29345659836fbee6951e`
  - `Program.cs` sha256 `26d9fdf159545494ad7abedb59e1925b3a9a7e6a0d61cc12f8e5699c6c805a32`
- Executed reference: the immutable Listener release `2c4a8af…` whose `SharpClaw.Listener.dll` sha256
  `5b66f62afcd533ce38d71b32f7bd19ece114f610cc72310d98d669f130af9453` matches its `bundle-release.json` (checked by the fixture harness).
- Architecture inputs: both opposed reviews (CHANGE `775cb8dc…cf21`, STATUSQUO `464587f9…cc96`, digests re-measured), the
  source-checked decision, owner consumer-neutrality and extraction records.

## Findings in the host code (what the package must preserve)

1. Schema and open (`Store.cs:13-43`): one row `checkpoint(id=1, schema, instance, project, route, cursor, pending, phase,
   attempts, receipt)`; owner open sets WAL and `synchronous=FULL`, creates the table, inserts identity once, refuses another
   identity (`listener_state_identity`), converts `sending` to `uncertain`; `existingOnly` refuses a missing file and neither creates
   nor normalizes; lease `<state>.lock` with `FileShare.None`; refused open disposes connection and lease (`:37-42`).
2. Transitions (`Store.cs:50-72`): CAS updates on `id=1`. `Accepted` compares the exact pending text. `FailedPreparation` and
   `Uncertain` silently do nothing outside their phase.
3. Host coupling is limited to `Notification` decoding/`Json()` and `Validate` (`Store.cs:45-52,70`, `*Rearms/Replays/Rebinds.Key`):
   the package receives canonical bytes and never needs the type.
4. Grants (`PreparationRearms.cs:79-96`, `DeliveryReplays.cs:56-80`, `DeliveryRebinds.cs:66-94`): lazily created tables (exact DDL
   recorded in the fixture), capacity 1024 without eviction, receipt JSON serialized by the host. Replay: receipt insert and
   `uncertain → sending` in one transaction with CAS on cursor, attempts, route and bytes; `pending_id UNIQUE`. Rebind: receipt
   insert and route CAS in one transaction. Rearm: receipt only; baseline derived from earlier rows (`:41-71`).
5. Host policy that stays outside: allowance 3, request schema/digest, configuration digest and reload, readiness probe, feed
   byte comparison (`PreparationRecovery.cs`, `DeliveryReplay.cs`, `DeliveryRebind.cs`, `Program.cs:74-105`), Unix umask.
6. Observed behaviour of the current release (fixture transcript, `tests/…/Fixtures/CurrentListener/oracle.json`): after Accept
   and a new Stage the previous acknowledgement stays in `receipt`; a rebind after the move answers only from the stored receipt;
   a fresh replay request after a lost replay is refused.

## SDK findings

- No existing delivery capability. Session recovery (`IRecovery`, `ExecutionCertainty.Unknown`) has no resend; it is a different
  authority and is not reused (decision F4/F6).
- Existing leases are directory leases in platform packages that depend on Common (`Monstertacle.Multisession.Linux/Windows`);
  the current checkpoint uses a sibling file lease. Reusing them would move live state (migration) and add a forbidden
  dependency. The package therefore owns a small file lease and private-access policy for both platforms.
- Build/CI: `scripts/Invoke-Quality.ps1` enumerates test suites; `tests/coverage-scope.json` and `tests/coverage.runsettings`
  enumerate product assemblies; `eng/Test-PackageLayout.ps1` enumerates packages. All three need one registration each.
- `.gitignore` forbids SQLite databases as source fixtures; the fixture is therefore committed as the Listener-captured exact
  DDL and rows and rebuilt per test (the binary hashes of the generated files are recorded in `oracle.json`).

## Open questions and decisions

- Q: Public error codes? Decision: typed `DeliveryRefusal`; host maps to its literal operator codes (decision C4 permits a small
  explicit mapping). Q: Who serializes receipts? Host (decision C2/F2); library stores and returns the text exactly.
- Q: Item identity for unique consumption? The stored row has no separate item column; the host attests the item id it derived
  from its canonical payload; the expectation binds the library-computed digest of the stored bytes.
- Q: Version? New identity at the root version; no root bump by this feature (coordinated with the supervisor 2026-11-01).

## Test plan

Real SQLite in temporary private directories, real file leases, fixture rebuilt from the current Listener dump; tests for each FR
including identity/corruption refusal, cancellation, crash (Sending at reopen), transaction rollback, stale expectations, capacity,
bounds and permissions/lock on the native platform. Package-level conformance in the isolated consumer.
