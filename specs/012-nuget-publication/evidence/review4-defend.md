# Review 4, DEFENDER: exit-code fix 70c9d7c (SDK PR 15)

Object: the SDK checkout at
`70c9d7c7704026b51c8633d68403871f6de88b33` (worktree clean before and after). Frozen packet verified with
`sha256sum -c ../frozen.sha256`: all 4 files OK.

## Verdict

**The fix is sufficient. No P0/P1/P2.** Two P3 notes below; neither blocks.

## Evidence (measured here)

- **Diagnosis, probe** (`defend-work/probe/`, pwsh 7.6.9, the runner's appended line
  `if ((Test-Path -LiteralPath variable:\LASTEXITCODE)) { exit $LASTEXITCODE }`, run as `pwsh -command ". '<file>'"`):
  - step whose last native command exits 1, then prints output: exit=1. This reproduces the CI failure mode. The same step without
    the appended line gives exit=0, so the appended line is the cause.
  - step calling a child script with a native exit 1 followed by `exit 0`: exit=0. The child's `exit` ends only the child script.
  - step `$r = ./child.ps1`, where the child throws before reaching `exit 0`: exit=1, and the following `'after'` line does not run.
    So the trailing `exit 0` cannot hide a throw.
- **CI excerpt** (`frozen/ci-linux-f04582c-excerpt.txt`): `PASS 19/19`, followed by `Process completed with exit code 1`, with shell
  `pwsh -command ". '{0}'"`. This matches the diagnosis.
- **Suite, one run in CI wrapper form** (`defend-work/gh-suite.ps1`, under `run-capped.sh`): `Status PASS, Passed 19, Failed []`,
  exit=0. Log: `defend-work/suite-run.txt`.

## Assessment

1. **The diagnosis is correct.** Probe and CI excerpt agree. At f04582c, the 403 case (and the pending case) leave the last
   `dotnet nuget push` exit code at 1. The suite then prints PASS and ends without its own `exit`, so the runner's appended line exits 1.
   The publish Push step has the same exposure when the last absent package returns 409/pending. Without the fix, a real release would
   end red after a correct publication.
2. **The fix does not mask failures.** Every failure path in `Publish-CanonicalRelease.ps1` is a `throw` under
   `$ErrorActionPreference='Stop'`:
   - missing key
   - byte drift
   - non-409 push failure (`throw "Push failed"`)
   - plan conflict, through `Test-PublishedPackage.ps1`, which throws on any difference
   - verify timeout
   - signature failure (`Test-RepositorySignature.ps1` checks `$LASTEXITCODE` of `dotnet nuget verify` and throws)

   No helper signals failure only through an exit code. Grep of `Test-CanonicalPackageHashes.ps1` shows no native calls and no `exit`.
   A throw unwinds past the trailing `exit 0` (probe 3).

   In the suite, `exit 1` on failures stays ahead of `exit 0`. A throw during setup or in `finally` also skips `exit 0`.
3. **The Red is a genuine domain Red** (`frozen/red3-exit-code.txt`). The f04582c scripts were run with only the test extended (409
   moved to `$ids[-1]`, plus the `$LASTEXITCODE` assertion). The failure is exactly the new assertion: `exit code 1 after a successful
   push step`, in both direct and CI-wrapper form. The test pins the publish script's `exit 0` in particular. With the conflict on
   `$ids[0]` (the old setup), the last push returned 201 and the assertion could not bite. Moving the conflict to `ids[-1]` is therefore
   necessary, not cosmetic. Push order follows `record.packages`, and `ids[-1]` is last there.
4. **No other step in `.github/workflows/publish.yml` fails after success for the same reason:**

   | Step | Last native command and outcome |
   | --- | --- |
   | Check the requested version (bash, `set -euo pipefail`) | the `[[ =~ ]] \|\| {exit 1}` test; 0 on a valid version |
   | checkout / setup-dotnet / NuGet login | actions, not shell steps |
   | Verify the producer run (bash, `-euo pipefail`) | `gh run download`. Each earlier command either succeeds or stops the step, which is intended |
   | Verify the canonical packages (pwsh) | `Test-ReleaseInput.ps1` and `Test-CanonicalPackageHashes.ps1` call no native commands, so `LASTEXITCODE` is unset and the runner line is skipped. Failures throw |
   | Plan (pwsh) | Plan mode calls no native commands (Invoke-RestMethod/WebRequest are cmdlets). The script ends with `exit 0`, and the following lines are cmdlets only |
   | Push (pwsh) | this is the fixed case. `exit 0` follows a pending last package; a non-409 failure throws |
   | Verify (pwsh) | the last native command is a successful `dotnet nuget verify` (a failed one throws). The script ends with `exit 0`, and the following lines are cmdlets/redirections |

## Findings

- **PD4-F1 (P3, no block):** the trailing `exit 0` in `Publish-CanonicalRelease.ps1` is a blanket reset. A future failure path that
  reports only through a native exit code, with no throw, would be hidden. This is not true today (see 2). A narrower option would be
  to reset only after the push loop, or to rely on `$PSNativeCommandUseErrorActionPreference`. Either is a matter of taste, not a defect.
- **PD4-F2 (P3, no block):** no test pins the suite's own `exit 0`. Only the CI run itself would show a regression, and it would
  show as red (fail-safe), not as a false green.

## Weaknesses of this position

- The probe reproduces the runner's appended line from knowledge of the GitHub runner. I did not read it from runner source. The CI
  excerpt and red3 (wrapper form) corroborate it independently.
- I did not re-run red3. I checked its content and that the failure message matches the new assertion verbatim. Re-running would
  have cost a second suite run, and this brief allowed one.
- The Windows matrix leg (`windows-latest`) was not run. The pwsh semantics are the same, but this is not measured.
- The real nuget.org path (OIDC, a real 409, the real `dotnet nuget verify` on nuget.org signatures) cannot be exercised locally.
  The claim for row 4 rests on reading the code, not on a run.

## Cost

Start `2026-11-02T02:11:56Z`, end `2026-11-02T02:13:07Z` (`date -u`). Tool calls: 9 (8 Bash, 1 Write). No edits, commits, pushes,
logins, or Python in the repo. Temp files only under `review4/defend-work/`.
