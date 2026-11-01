# Handoff to the product integration actor (feature 009)

Measured: 2026-11-01 (UTC times from CI and `date -u`). This is implementation qualification of the package only; it is not
host integration, live activation or a release.

## Qualified candidate

- Repository `monstertacle/monstertacle.multisession`, PR #10, branch `feature/009-delivery-checkpoint`,
  head `51d453962d80f2f8617c93598b0b8292c39e3347` (base `origin/main` `5d4aa2fc693a43e3302cf50bd0ff8ecbbed7d435`).
- GitHub Actions run 41307843896 (pull_request) for that head: quality ubuntu-24.04 success, quality windows-latest
  success, canonical-consumer ubuntu-24.04 and windows-latest success; codemetr-guard success on both platforms.
- Quality source hash (both platforms): `c65b8922c4e3e04e91bb66bf2489ac41e438447795d64a1142baeabc41cf845c`.
- Product coverage including the new assembly: Linux 97.8976 percent, Windows 97.7381 percent (gate 87).

## Package bytes (development identity, not a release)

The CI pack uses the unchanged root version `0.2.0-alpha.4`; these bytes must not be published or consumed as a release,
because canonical alpha.4 bytes of the other packages already exist. The release identity is decided with the version owner
of PR #11 (current-version-only, root `0.2.0-alpha.5`) and the supervisor.

- `Monstertacle.Multisession.Delivery.0.2.0-alpha.4.nupkg` sha256 `d0b291e8513afbed8ae70a4af1c2a364ef5d1841c433d37cd7be4bc97aaa3aad`
  (NuGet lock content hash `31JgvzGnZGJFXZ11MC6XGUsxmnOdatzLeoRsYjuliFVyHjKqz/iW5o7HAl/9n8boXlsP1Dw2aOfTc1lzrgQ9Ag==`).
- `lib/net10.0/Monstertacle.Multisession.Delivery.dll` sha256 `55ad744da4e494e104c6113296dfbb8833ff3266e307e7b5f92d8386d35fc012`.
- Dependency: `Microsoft.Data.Sqlite` 10.0.14 only. MIT expression, copyright and LICENSE in the package.
- Independent consumer (`eng/Test-DeliveryConsumer.ps1`): restored the exact bytes on Linux and Windows from the canonical
  producer artifact; nine conformance cases PASS on both.

## Host adapter steps (SharpClaw Listener, after the no-legacy cleanup)

1. Reference the package from the repo-local feed with locked restore at the released identity.
2. `DeliveryChannel(InstanceId, ProjectId, Store.Route(config))`; `DeliveryCheckpointOptions(StatePath, channel)
   { InitialPosition = InitialCursor }`; `OpenAsync` for `run/once`, `OpenExistingAsync` for admin commands.
3. Payload = UTF-8 of `Notification.Json()`; `StageAsync(e.Cursor, bytes)`; adapter `sending` callback = `MarkSendingAsync`;
   `AcceptAsync(bytes, receipt)`; failure: `MarkUncertainAsync` or `RecordPreparationFailureAsync` by phase.
4. Keep host keys (`PreparationCheckpoint`, `DeliveryReplayCheckpoint`, `DeliveryRebindCheckpoint`), digests, feed
   comparison, readiness, configuration reload and request validation; map them to `CheckpointExpectation` and pass the
   `Wire.Json` receipt text; item id = `EventId` of the pending notification; context digest = configuration digest.
5. Rearm exhaustion: `AttemptGrants.ListAsync` gives the stored grants (receipt + attempts) for the host's custody check.
6. Map `DeliveryRefusal` to the current operator codes (`IdentityMismatch`/`UnsupportedFormat` → `listener_state_identity`,
   `CheckpointMissing` → `checkpoint_missing`, `TransitionConflict` → `checkpoint_conflict`, `StaleExpectation` → `*_stale_*`,
   `AllowanceConsumed` → `delivery_replay_consumed`, `GrantCapacity` → `*_receipt_capacity`, `RequestConflict` →
   `*_request_conflict`, `NotEligible` → `*_not_eligible`/`rebind_phase`).
7. Follow-up F-009-1 (`tasks.md`): read-only live-copy proof and host black-box suites on the package-based Listener.

## Findings disposition

All review findings are dispositioned in `review.md`; open owned item F-009-1 (integration actor) and the version/merge
decision (supervisor with the PR #11 owner). No P0/P1 open in the package.
