# Defender review, round 3 (narrow): fix diff 64a982d..f04582c, PR 15

Head under review: f04582c9f4cf232cd727b29f6838eefb37c92825 (repo read only, working tree clean). Frozen packet verified against
../frozen.sha256 (challenge.md, defend.md, fix.diff: OK).

## Verdict

**Merge after the required CI (Linux and Windows) is green on f04582c; the 0.2.0-alpha.7 publication is not put at risk.**
No P0, P1 or P2. Every round-2 disposition holds in code, tests and docs. Two P3 notes (evidence wording, redundant guard untested).

## What I measured

- `pwsh -NoProfile -File eng/Test-PublicationTooling.ps1 -WorkRoot .../defend-work/tooling` at f04582c: `Status PASS, Passed 19,
  Failed []`, exit 0 (defend-work/tooling.out, sha256 bf0d0b83...d11f). Matches spec.md "19 cases" and green-publication-tooling.txt.
- `eng/Test-ReleaseInput.ps1 -Release eng/release-records/0.2.0-alpha.7.json -ArtifactDirectory .../producer-download`: PASS, manifest
  0085125f...852f, eight packages, exit 0 (defend-work/input.out, sha256 e1300d06...d5a6). `find` shows exactly the eight
  `.nupkg` files flat in `packages/`, no hidden or nested file, so the new recursive guard cannot refuse the real artifact.
- Not run: the Red at 64a982d (one tooling run allowed); I checked red2 by reading the old code against the new cases.

## Dispositions against code, tests, docs

| Disposition | Holds? | Evidence |
| --- | --- | --- |
| PC2-F1/PD2-F1 strict 409 | yes | eng/Publish-CanonicalRelease.ps1:72-78: exit code captured before logging; pending only on `Response status code does not indicate success: 409 \(`; anything else throws. Positive side proven with the real SDK: the existing 409 case passes at head, and pending is reachable only through the strict pattern, so the SDK's real conflict text matches it. Negative side: new 403 case (Test-PublicationTooling.ps1:193-197, feed :138-140, reason phrase `Forbidden for quota 409`) is refused. |
| PC2-F2 output always logged | yes | Publish-CanonicalRelease.ps1:74 `Write-Host` for every push, success or not; Write-Host does not enter the JSON return value; publish.yml does not capture the Push step output (push-result.json is written by the script, :82). |
| PC2-F3 missing tests | yes | 403 refusal case; `verify: served equivalent packages pass with a receipt per package` (Test-PublicationTooling.ps1:207-212) requires Status PASS and one EQUIVALENT + RepositorySigned receipt per id. |
| PC2-F4 German CLI | code yes, evidence no | Test-RepositorySignature.ps1:9 sets `DOTNET_CLI_UI_LANGUAGE=en`; :11 prints output on failure. The "checked with de-DE" claim has no recorded artifact (PD3-F1). |
| PC2-F5 certificate wording | yes | docs/publishing.md:35, spec.md FR-005 now say trusted chain (verify exit code, :11) plus subject prefix (:19), fingerprint recorded not pinned. Matches code exactly. |
| PC2-F6 FR-007 hidden/nested | yes | Test-ReleaseInput.ps1:22-25 `-Recurse -Force` plus directory check; test :98-100. |
| PC2-F7 test-only switches | yes (accepted) | publish.yml passes neither switch (checked all three script calls). |
| PC2 random port | yes | Test-PublicationTooling.ps1:121-129, ten attempts, failed candidates closed. |
| PC2-F8 Windows | open by design | tied to T007 (unchecked) - correct state. |
| PD2-F2 Red claim | yes, exact | plan.md:34 matches red-publication-tooling.txt (domain Red only for symbol and unsigned-compare; 409 failed on plain HTTP; Verify cases did not fail first) and the note in manual-checks.md; mutation file shows no-pending and no-signature-required red. red2: at 64a982d the old `-File` non-recursive listing does not see `nested/.extra.snupkg` and the loose `409` match turns the 403 into pending, so both new cases must fail and the third new case (Verify success) must pass: 16+1=17 passed, 2 failed - exactly red2-publication-tooling.txt. |
| PD2-F3 untested paths | yes | Verify success tested; signature branch: manual check (SharpQrs) and the real receipt. |

## Findings

### PD3-F1 (P3) "checked with DOTNET_CLI_UI_LANGUAGE=de-DE" has no recorded evidence (specs/012-nuget-publication/tasks.md:35)
manual-checks.md is not in the diff and nothing in evidence/ records the de-DE run. Whether NuGet's verify process honors the variable
is therefore asserted, not shown. Scenario for harm: none for alpha.7 - publish.yml runs on ubuntu-24.04 with English default output, so
the parser sees English either way. Fix: add one line to manual-checks.md with command, SDK version and result, or reword the
disposition to "forced English output; not separately recorded".

### PD3-F2 (P3) The directory guard in Test-ReleaseInput.ps1:23 is not independently tested
The only new case uses a file that is both hidden and nested. Removing `-Recurse` or `-Force` turns it red (the file is no longer
listed), but removing the `DirectoryName` throw does not: `.extra.snupkg` is still refused by the name check (:27), and a nested file
with a recorded name is refused by the count check (:29). The guard is therefore redundant, not wrong. Empty subdirectories and directory
symlinks are not refused (not files); harmless because Push and Verify only open recorded paths (Publish-CanonicalRelease.ps1:66, :44).
Fix: none needed; optionally separate hidden-top-level and nested-visible cases.

Briefly (P3, cosmetic): docs/publishing.md:35 and spec.md:35,37 lines now exceed the surrounding wrap width.

## Publication risk for 0.2.0-alpha.7

- Input check passes on the real artifact at head (measured above).
- Plan/Push/Verify flow: a real nuget.org 409 prints the runtime `EnsureSuccessStatusCode` text `... 409 (<reason>)`; the pattern
  needs only `409 (`, so an empty or custom reason phrase still matches. If the text ever differed, the failure mode is a stopped run
  (fail closed), not a wrong publication; a rerun's Plan would then see the package served as present.
- Signature check unchanged except forced English and logging; real-signature path covered only by the manual SharpQrs check and the
  real receipt (as dispositioned).

## Weaknesses of my position

- I did not reproduce red2 (single run allowed); its consistency is argued from the old code, not measured by me.
- Captured tooling output does not show the push client texts (Invoke-Case output not in stdout), so "real SDK 409 text matches" rests
  on inference: the 409 case can only pass through the strict pattern.
- nuget.org's actual 409 response was not observed; the runtime message format is assumed stable for the global.json SDK.
- Windows behavior (path comparison in Test-ReleaseInput.ps1:23, listener) is not shown by me; it depends on the required CI.
- PD3-F1 could be called P2 by a strict reader of "belege vor Bericht"; I keep P3 because it has no effect on the Linux publication.

## Duration and tool calls

Start 2026-11-02T02:07:15Z, end 2026-11-02T02:09:11Z (date -u). Tool calls: 8.
