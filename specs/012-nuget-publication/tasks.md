# Tasks

- [x] T001 Release record eng/release-records/0.2.0-alpha.7.json from the canonical producer run 41333282133 (commit 8167d37).
- [x] T002 eng/Test-ReleaseInput.ps1, eng/Test-PublishedPackage.ps1, eng/Publish-CanonicalRelease.ps1 (Plan, Push, Verify).
- [x] T003 .github/workflows/publish.yml (manual, main, environment nuget, pinned actions, OIDC login only when a push is needed).
- [x] T004 docs/publishing.md: account policy fields and the release procedure.
- [x] T005 Local checks against the real producer artifact, including refusal cases and a local test feed for the plan.
- [x] T006 Two opposed reviews of the workflow and scripts on frozen head dc9c67c (evidence/review-{challenge,defend}.md; challenger
  fix-first, defender merge); dispositions below.
- [x] T009 Review fixes, test-first: eng/Test-PublicationTooling.ps1 (Red and mutation evidence as stated in plan.md, Green after),
  eng/Test-RepositorySignature.ps1, quality workflow step, plan.md, corrected guide.
- [x] T007 Required CI on the PR head 70c9d7c (all twelve checks green, Linux and Windows); four review rounds; merged as 02c6129 (PR 15).
- [x] T008 Publish run 41377508020 from main 02c6129 (2026-11-02): OIDC login, eight packages pushed, all served, equivalent and signed by
  the nuget.org repository certificate (evidence/publication-receipt-0.2.0-alpha.7.json); an independent download and comparison outside the
  workflow, on the local development host, gave the same served digests (evidence/publication-independent-check.txt). Owner on nuget.org: bleissem.

## Review dispositions (round 1)

| Finding | Disposition |
| --- | --- |
| PC-F1 (P1) no plan, evidence or automated test; T005 evidence not attached | plan.md with deep dive and constitution check; evidence/ with manual checks, Red, Green and mutation logs; `eng/Test-PublicationTooling.ps1` in the quality workflow on Linux and Windows. The first script version was not test-first; recorded in plan.md. |
| PC-F2 / PD-F1 (P2) signature never verified | Served packages must carry `.signature.p7s` (compare) and pass `dotnet nuget verify --all` with a repository primary signature by the nuget.org certificate (`eng/Test-RepositorySignature.ps1`); signer and fingerprint in the receipt. |
| PC-F3 / PD-F4 (P2/P3) a rerun after a partial push stops on 409 | 409 marks the package pending; Verify requires it served, equivalent and signed. Guide corrected. |
| PC-F4 (P2) unrecorded symbol packages | `--no-symbols`; the input check refuses any file in the package directory that is not a recorded package. |
| PC-F5 / PD-F5 (P3) id-token and GH_TOKEN job-wide | `GH_TOKEN` moved into the one step that reads the producer run. id-token stays job-level (single job, decision in plan.md): only pinned official actions and repository scripts from main run in it. |
| PC-F6 / PD-F2 / PD-F3 (P3) guide errors | Repository secret, primary repository signature, root package scope note, private-repository sentence removed. |
| PC-F7 (P3) minor gaps | API key on the push command line: the documented trusted-publishing usage; the key is masked by the login action and expires in one hour (accepted). No environment reviewer: optional control noted in the guide (owner choice). Receipt on push failure: the run log carries the push output (accepted). |

## Review dispositions (round 2, fix diff dc9c67c..64a982d)

Challenger fix-first (one P2), defender merge (P3 only); reports evidence/review2-{challenge,defend}.md.

| Finding | Disposition |
| --- | --- |
| PC2-F1 / PD2-F1 (P2/P3) "409" matched anywhere in the push output | Only the push client's status message `Response status code does not indicate success: 409 (` marks pending; the push output is always written to the log. Test: a 403 whose reason phrase contains 409 fails the push (Red first, evidence/red2-publication-tooling.txt). |
| PC2 missing tests (P3) | Added: refused push fails; Verify passes with a receipt per package. |
| PC2 German CLI output (P3) | The signature script forces English CLI output and prints the verify output on failure; checked with `DOTNET_CLI_UI_LANGUAGE=de-DE`. |
| PC2 certificate wording (P3) | Docs and spec state a trusted chain plus the nuget.org repository subject; fingerprint recorded, not pinned (rotation). |
| PC2 FR-007 overclaim (P3) | Hidden files and subdirectories are refused (test, Red first). |
| PC2 test-only switches (P3) | Not reachable from publish.yml (it passes neither); accepted. |
| PC2 random port without retry (P3) | Up to ten attempts on a free local port. |
| PC2 Windows not shown (P3) | Shown by the required quality job on Windows for the PR head before merge. |
| PD2-F2 (P3) Red claim overstated | plan.md states exactly which cases had a domain Red and which rest on mutation probes or manual checks. |
| PD2-F3 (P3) untested paths | Verify success is now tested; the signature branch is covered by the manual check and the real publication receipt. |

## Review dispositions (round 3, fix diff 64a982d..f04582c)

Both roles: no P0-P2; merge after required CI. Reports evidence/review3-{challenge,defend}.md.

| Finding | Disposition |
| --- | --- |
| PD3-F1 (P3) German-language check not recorded | Recorded in evidence/manual-checks.md. |
| PD3-F2 / PC3-F3 (P3) nested-file check redundant with the name and count checks | Accepted: defence in depth, refusals are equivalent. |
| PC3-F1 (P3) a 409 for a deleted, unreusable version waits 60 minutes before failing | Accepted: fails closed; the receipt names the package. |
| PC3-F2 (P3) refusal cases accept any error | Accepted for this suite; each refusal case changes one input. |
| PC3-F4 (P3) English CLI setting persists in the process | Accepted: the scripts run in their own step processes. |
| PC3-F5 (P3) redacted path differs from the frozen copy | The repository copies are redacted for the public repository; the frozen copies are the reviewed originals. |
| PC3-F6 (P3) Windows not shown | See the CI finding below. |

## CI finding on f04582c

The required Linux quality job failed after the tooling suite printed PASS (19 of 19): the runner's PowerShell step ends with the last
native exit code, here a deliberately refused test push. The same applies to the publish workflow: a pending (409) push of the last
package would have failed the push step. Test first (evidence/red3-exit-code.txt: after a pending push of the last package the new assertion on
the exit code fails, direct and in the CI wrapper form; this is the test's own failure, the CI symptom itself, PASS followed by exit
code 1, was reproduced only by the round-4 reviewers' wrapper experiments, evidence/review4-*.md), then the publish script and the suite end with an explicit `exit 0` on success
(evidence/green-publication-tooling.txt, both forms).
