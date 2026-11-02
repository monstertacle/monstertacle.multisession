# Round 4 challenge: exit-code fix 70c9d7c (SDK PR 15)

Role: challenger (argue against where the evidence supports it). Object: `70c9d7c7704026b51c8633d68403871f6de88b33`, repo read only,
`git status --short` empty before and after. Frozen packet: `sha256sum -c ../frozen.sha256` OK for all four files. The script part of
`fix.diff` (both `.ps1` hunks) is identical to `git diff 70c9d7c^ 70c9d7c`; the commit also adds evidence and `tasks.md` text that
the frozen diff does not contain.

## Verdict

**No blocking finding (no P0/P1/P2).** The diagnosis is right, `exit 0` masks no failure path in the publish script, the publish.yml
callers keep working, and the new assertion really checks what the runner's appended line reads. Merge after the required CI on the PR
head is green. Only P3 notes.

## Evidence (measured in this round)

| # | What | Result |
| --- | --- | --- |
| E1 | Synthetic step files in `challenge-work/exp/` built exactly like the runner template (`$ErrorActionPreference = 'stop'` prepended, `if ((Test-Path -LiteralPath variable:\LASTEXITCODE)) { exit $LASTEXITCODE }` appended), run with `pwsh -NoProfile -command ". '<file>'"` | step1: child script ends with `/bin/false`, step prints all output, **exit=1** (the CI symptom: success output, then exit 1). step2: same child plus `exit 0`, caller continues (`after-call` printed), **exit=0** |
| E2 | step3: `$receipt = ./child.ps1` where the child ends with `exit 0` | output before `exit` is kept (`receipt=[child-out]`), exit=0 |
| E3 | step4/step6: child throws inside `@(foreach ...)` after a failing native call, `exit 0` after it; step5: throw inside `ForEach-Object` pipeline under StrictMode | all **exit=1**, error shown, caller's next line not reached: `exit 0` is never reached on a throw |
| E4 | The one permitted suite run, wrapper form, `-WorkRoot challenge-work/suite` (02:13:03Z-02:13:08Z) | `Status PASS`, `Passed 19`, `Failed []`, exit=0 (`challenge-work/suite-wrapper.txt`) |
| E5 | `grep` of the publish path for native commands | only `dotnet nuget push` (Publish-CanonicalRelease.ps1:72, exit code captured :73 and either `pushed`, `pending` or `throw`, :78) and `dotnet nuget verify` (Test-RepositorySignature.ps1:10, non-zero -> `throw`, :11). No `PSNativeCommandUseErrorActionPreference` anywhere in `eng/` or `.github/`. |

## Focus questions

1. **Diagnosis.** Right. The CI excerpt shows `shell: /usr/bin/pwsh -command ". '{0}'"`, PASS 19/19 and `Process completed with exit
   code 1`; the suite's own `exit 1` is only taken with failures, so the only source of 1 is the appended `$LASTEXITCODE` line. E1
   reproduces the mechanism. (The template text itself is from my knowledge of the runner, not shown in the log; E1 shows that this
   template gives exactly the observed symptom.)
2. **Can `exit 0` in Publish-CanonicalRelease.ps1 mask a failure?** No. Every failure path is a `throw` (or a cmdlet error under
   `$ErrorActionPreference = 'Stop'`), which ends the script before line 107 and fails the step with exit 1 (E3), including a throw
   inside the Verify receipt pipeline (`Compare-Served` -> Test-PublishedPackage.ps1:50, Test-RepositorySignature.ps1:11/14/20) and
   the push loop (:69, :78). Both native calls are checked explicitly (E5), so no failure relied on the runner's `$LASTEXITCODE` line.
3. **Effect on publish.yml callers.** The script is invoked (not dot-sourced) in all three steps, so `exit` ends only the script and
   sets `$LASTEXITCODE = 0` in the step (E1 step2). Plan step (:75-77): continues to the `GITHUB_OUTPUT` lines. Verify step (:97-99):
   `$receipt` keeps the JSON emitted before `exit` (E2). Push step (:92): ends with 0 after a 409-pending last package, which was the
   latent publish failure.
4. **Other publish.yml steps ending with a native non-zero after success?** None. Bash steps run with `set -euo pipefail`; "Verify the
   canonical packages" (Test-ReleaseInput.ps1, Test-CanonicalPackageHashes.ps1) runs no native command; Verify's only native call is
   `dotnet nuget verify`, which must be 0 or the script throws.
5. **Does the test genuinely exercise the wrapper behavior?** Yes, at the level that matters: the new check reads `$LASTEXITCODE` right
   after `& $publish -Mode Push`, which is exactly the value the appended runner line reads after `./eng/Publish-CanonicalRelease.ps1`.
   Moving the conflict to `$ids[-1]` makes the refused push the last native call (push order = plan order = record order). red3 shows
   it red at the f04582c script (`exit code 1 after a successful push step`), E4 green at head. The old `$ids[0]` variant could not
   have caught it (the second, successful push reset the code).

## Findings

| Id | Sev | Location | Scenario | Fix |
| --- | --- | --- | --- | --- |
| PC4-F1 | P3 | `specs/012-nuget-publication/tasks.md` (CI finding paragraph), `evidence/red3-exit-code.txt` | "leaves exit code 1, direct and in the CI wrapper form": in red3 both forms exit 1 through the suite's own `exit 1` (a failed case), not through the appended line. The original CI symptom (PASS then exit 1 from the wrapper) is not reproduced in the evidence; only the publish-script variant is. E1 here reproduces the mechanism synthetically. | Optional wording: "the new case fails in both forms"; or accept. |
| PC4-F2 | P3 | `eng/Test-PublicationTooling.ps1:229-230` | Nothing tests the suite's own trailing `exit 0`; in the wrapper form it exits the dot-sourced process before the appended line, so the green wrapper run (green (2), E4) shows the outcome, not that the appended line would be harmless. Removing it again would only show in CI (exactly the f04582c failure). | Accept: CI is that test. |
| PC4-F3 | P3 | `eng/Publish-CanonicalRelease.ps1:107` | `exit` in a script that someone dot-sources (`. ./eng/Publish-CanonicalRelease.ps1`) would end the caller's whole session/step with 0, skipping later lines. No current caller does this (publish.yml and the suite invoke it). | Accept, or document "invoke, do not dot-source". |
| PC4-F4 | P3 | `eng/Publish-CanonicalRelease.ps1:106` comment | Says only "409 pending"; the `exit 0` also defines the Plan and Verify results. Cosmetic. | None needed. |

## Weaknesses of my position

- I argued against and found nothing blocking; all four notes are wording or hypothetical misuse.
- The runner template text is from knowledge of the runner source, not visible in the log; E1 only shows that this template yields the
  observed symptom, not that the runner uses it verbatim.
- I ran the real suite once (head, wrapper form) and did not re-run f04582c to reproduce the CI symptom with the real suite; red3 is the
  author's evidence and, as PC4-F1 notes, shows a related but not identical failure.
- The publish workflow itself was not run (forbidden); its step behavior is inferred from the synthetic template runs.
- Windows behavior of the same template was not checked by me; it is the same `pwsh` template, gated by the required CI.

## Duration and tool calls

Start `2026-11-02T02:11:50Z`, end `2026-11-02T02:13:21Z` (both `date -u`; end taken before writing this file).
Tool calls: 10 (9 Bash, 1 Write).
