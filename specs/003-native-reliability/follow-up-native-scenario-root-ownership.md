# Native scenario state-root ownership (T021)

- ID: SDK-NATIVE-SCENARIO-ROOT-OWNERSHIP-20261104 (review alias DEF-S33-OWNERSHIP-QUESTION).
- Owner: Alexander Bleissem. Root accepts.
- Priority: P2. This is a resource ownership question, not an established product defect.
- Status: open, not fixed.

## Observation (2026-11-04)

**What was observed**
- On the writer's Linux host, `~/dev/.agenttemp` held 37 `multisession-native-scenario-<guid>` directories, from 2026-10-30 to
  2026-11-04. Each local full PlatformTests run added one; the run at 07:15 on 2026-11-04 added the latest.
- Each holds `.multisession.lock`, `.multisession-validation-root` and one `run-<guid>` directory.
- Correction: an earlier handoff called the total of 134 `multisession-*` entries such directories. Most of those entries are
  other, unrelated scratch files and directories. Only the 37 above belong to this question.

**Source at `main` `4751cc4`**
- `tests/Monstertacle.Multisession.PlatformTests/NativeScenarioOwnershipTests.cs`, `ReleasesNativeStateAfterConcreteScenario`
  (lines 45-61), added in `0a9da51` ("Preserve native failure evidence and isolate SQLite execution"):
  - it creates `multisession-native-scenario-<guid>` under the user profile's `dev/.agenttemp`;
  - it runs `ScenarioRunner.ExecuteOwnedAsync`;
  - it writes `.native-owner-release-probe` and checks that the lease was released;
  - it has no `finally` that deletes the root.
- The theory `ExecutesOfflineScenarioAndReleasesNativeState` (14 cases, lines 25-41) deletes its own
  `multisession-cli-scenario-*` root in a `finally`.
- `ValidationRunState.DisposeAsync` releases the lease only. `ScenarioRunner.ExecuteOwnedAsync` disposes the state and records
  cleanup failures, and deletes nothing.
- This matches the documented host contract: the ValidationHost "deletes no foreign/production directories"
  (`specs/001-agent-multisession/quickstart.md`, section 1).

## Question

Is keeping the root after this test intended, for example as native failure evidence as the commit title suggests? Or is it a
missing test cleanup, given that the sibling theory deletes its roots? Keeping roots after a *passing* run has no stated purpose
in source or docs.

## Requirements for the answer

- The answer is decided from source, history and reviews, not assumed from the directory count.
- A cleanup must never delete foreign directories. It may only delete the root that the test itself created, and only after the
  release probe has run.
- If failure evidence is to be kept, the rule must be explicit: for example, keep the root only when the test fails, and record
  where it is kept.
- Existing roots are not deleted as part of the decision. Any removal of old local roots is a separate, owner-approved action.

## Next step (executable, source only)

1. Read the history and reviews of `0a9da51` and the 003 evidence for an intended retention of the native-scenario root.
2. Compare it with the theory's cleanup and with the hosted CI behaviour. Hosted runners are discarded after each job, so the
   effect is local accumulation only.
3. Write the decision, intentional retention or missing cleanup, here, with root's acceptance.

**Dependencies:** root's decision on the retention intent. A native Windows CI run is needed only if a cleanup change is made.

## Acceptance

One of these two:
- **Missing cleanup:** a Red/Green PlatformTests change. The test removes only its own root after a pass, the lease-release
  assertion stays unchanged, and it passes in native Linux and Windows CI.
- **Intentional retention:** documented in this file and in the test's XML documentation, with the retention condition, where
  the roots are kept, and who removes them.

Either way, no foreign directory is deleted and no product code changes.
