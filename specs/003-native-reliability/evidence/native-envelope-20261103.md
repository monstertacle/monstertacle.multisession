# Native envelope from existing CI runs (2026-11-03)

This applies to both `follow-up-windows-admission.md` and `follow-up-capacity-probe.md`. It is a read-only collection: no run was
repeated for it. Sources are the 16 `ci.yml` runs from `26286be` (2026-11-02T22:58Z) to `4b533c9` (2026-11-03T01:26Z): their
`quality-*` and `packages-and-consumer-*` artefacts. The collector lives outside the repository
(`e2e-harness-bundles/multisession-windows-envelope-20261103`).

| Measurement | Windows (windows-latest) | Linux (ubuntu-24.04) |
| --- | --- | --- |
| Forced-restart iterations (PersistenceRecoveryTests) | 160, 0 failed | 160, 0 failed |
| Worker setup per iteration (sum of measured steps) | p50 821 ms, p95 1554 ms, max 4584 ms | p50 279 ms, p95 313 ms, max 347 ms |
| Native scenario cases (NativeScenarioOwnershipTests) | 240, 1 failed (`embedded-consumer`, run 41551018970) | 224, 0 failed |
| `deterministic` scenario test duration | p50 3.4 s, max 26.4 s | p50 0.15 s, max 0.36 s |
| `embedded-consumer` | p50 0.9 s, max 9.0 s (the failure) | p50 0.17 s, max 0.32 s |
| `routing` | p50 19.3 s, max 56.8 s | p50 0.8 s, max 1.6 s |
| `recovery` | p50 12.1 s, max 62.2 s | p50 4.6 s, max 5.2 s |
| Published scenario reports | 197, 1 not passed (`deterministic`, run 41551010991) | 224, 0 not passed |

## What this shows and what it does not

- The two unattributed failures of 2026-11-03 did not recur in the 15 later runs, including 6 runs with the new attribution.
  Their cause is not isolated.
- Windows runs are 3 to 20 times slower than Linux at the median, with heavy tails, in exactly the scenarios that failed.
- The wave probe waits a fixed 5 s for two overlapping sends (`WaveProbeFlow.cs`, `barrier.Entered.Task.WaitAsync`). A tail like
  the measured `deterministic` maximum of 26.4 s could exceed such a wait. That is a hypothesis named from the source, not a
  measured cause. No deadline was changed.
- These are GitHub-hosted `windows-latest` runners. A supported Windows 11 host was not available, so the release-matrix gap
  stays open.
