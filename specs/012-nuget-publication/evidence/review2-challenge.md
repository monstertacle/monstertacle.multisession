# Challenge, round 2 (narrow): fix diff dc9c67c..64a982d, PR 15

Role: challenger (argues against merge and run where the evidence supports it). Object: commit
64a982d5a34c0cdd9d542f9d4a59c1e9e49efac8; `git diff dc9c67c..64a982d | sha256sum` = e6529aca…8445, identical to
frozen/fix.diff (frozen.sha256 checked: challenge.md, defend.md, fix.diff OK). Repo not modified.

Start 2026-11-02T01:59:35Z, end 2026-11-02T02:03:57Z (both `date -u`), about 4.5 minutes of tool time plus writing; 23 tool calls.

## Verdict

**Fix first, then merge and run. No P0/P1.** The round-1 dispositions are implemented, and I confirmed the main ones independently
(see "Checked and holding"). One P2 remains: the 409 rule matches the substring `409` anywhere in the push output. With a version
containing `409`, or a response that took `409ms` or `1409ms`, any failed push (403 invalid key, 400 validation) is recorded as
`pending`. The real error is not printed, and the run fails 60 minutes later with "not served". It fails closed, so wrong bytes
cannot be published. But it contradicts FR-004 and the PC-F7 disposition, and the fix is two lines. Everything else is P3.

## Findings

### PC2-F1 (P2) `409` is matched as a bare substring, and the swallowed output hides the real error

- Where: eng/Publish-CanonicalRelease.ps1:72-75 (`Select-String -SimpleMatch '409'` over all push output; the output is written
  only in the `else` branch).
- Measured (probe challenge-work/probe-409.ps1, output probe-409-out.txt): a local feed that answers every push with **403**.
  - Version `9.9.9`: the push throws (`Push failed`), which is correct.
  - Version `1.0.409`: no exception, push-result.json shows `"State": "pending"`, and the step succeeds. The 403 text is not in the
    output.
  - With 400 and `9.9.9` it throws; with 409 and `9.9.9` it is pending (the intended behavior).
- Measured (raw-push-403.txt): `dotnet nuget push` prints the elapsed time on the status line, for example
  `Forbidden http://127.0.0.1:44639/api/v2/package/ 458ms`. A failed request that takes 409, 1409, 2409 or 4090-4099 ms therefore also
  becomes pending. That is inferred from the format, not reproduced at exactly 409 ms.
- Consequence (inferred from code): Verify polls until the deadline (60 min, job limit 90 min) and throws
  `Package not served within 60 minutes`. That diagnosis is wrong, and the log never contained the 403/400 body. A rerun repeats it.
  No conflict is masked: Verify still compares content and signature. The harm is a misleading hour-long failure, not a wrong
  publication.
- Fix: classify on the NuGet status line, for example
  `-match 'Response status code does not indicate success: 409\b'` (or `'^\s*Conflict\s'`), and always write `$output` to the log,
  pending and pushed included. Add two test cases: a non-409 failure (403) must throw, and a version containing `409` with a 403
  must throw.

### PC2-F2 (P3) The disposition of PC-F7 says the log carries the push output; after the fix it does so only on failure

- Where: eng/Publish-CanonicalRelease.ps1:73-75; tasks.md, PC-F7 row ("the run log carries the push output (accepted)").
- Before the fix, `dotnet nuget push` wrote straight to the log. Now the output is captured, and for `pushed` and `pending` only
  the JSON result is logged. This is the same fix as F1 (always write `$output`).

### PC2-F3 (P3) The tests do not kill "every push failure is pending" or a broken Verify success path

- Where: eng/Test-PublicationTooling.ps1:173-190.
- The push case only answers 201 and 409, so a mutation of line 74 to `elseif ($true)` would stay green. There is no non-409
  failure case (F1 is exactly that gap). This is inferred by inspection; I did not run a second suite under the "run once" limit.
- Both Verify cases are negative and run with `-SkipSignatureVerification`. No automated case shows that Verify passes and writes
  the receipt with `Signer`/`SignerCertificateSha256`. The case "verify: an unsigned served package is refused" is refused by the
  compare step, not by signature verification. manual-checks.md and plan.md say so, so this is not an overclaim, but it is a gap.
- Mitigation, measured: I ran Verify end to end, **with** signature verification, against a local flat container serving the real
  nuget.org-signed Monstertacle.SharpQrs 0.2.0-alpha.1. The canonical package was that file with `.signature.p7s` removed
  (challenge-work/e2e-verify.ps1, e2e-verify-out.txt). Result: `Status PASS`, `EQUIVALENT`, 8 compared entries, signer
  `CN=NuGet.org Repository by Microsoft, …`, fingerprint `1f4b311d…482d`. The integration path works. It is just not in CI.
- Fix: the two push cases from F1, plus a positive Verify case with `-SkipSignatureVerification` that asserts the receipt.

### PC2-F4 (P3) Test-RepositorySignature parses localized text and logs nothing when it fails

- Where: eng/Test-RepositorySignature.ps1:8-12.
- Measured: with `DOTNET_CLI_UI_LANGUAGE=de-DE`, the real signed SharpQrs package is **refused**
  (`No repository primary signature`), because the output reads `Signaturtyp:`, `Name des Antragstellers:` and `SHA256-Hash:`.
  This fails closed, and the ubuntu runner is English by default, so there is no run risk today. It is a false refusal on any
  machine or runner with a non-English CLI language.
- When `dotnet nuget verify` exits non-zero, the script throws without printing the NU3xxx output, so the failure cannot be
  diagnosed from the log.
- Fix: set `$env:DOTNET_CLI_UI_LANGUAGE = 'en'` in the script before the call, and write `$output` before throwing.

### PC2-F5 (P3) "Requires the nuget.org repository certificate" is really a subject-prefix match plus chain trust

- Where: eng/Test-RepositorySignature.ps1:7,16; docs/publishing.md step 3; spec FR-005.
- The script accepts any certificate that chains to the SDK's trusted code-signing roots and whose subject starts with
  `CN=NuGet.org Repository by Microsoft,`. The fingerprint is recorded, not pinned.
- Not pinning is right. The repository certificate rotates: Newtonsoft.Json 13.0.3 carries the repository countersignature with
  certificate `5A2901D6…7DF4`, valid until 2024-05-16, while SharpQrs carries `1F4B311D…482D`, valid until 2027-05-19. The wording
  should say what is checked, though.
- Cheap hardening: also require the signed attribute line `Service index: https://api.nuget.org/v3/index.json`, which sits two lines
  above the signer line in the real output.

### PC2-F6 (P3) FR-007 "any other file is refused" misses hidden files and subdirectories

- Where: eng/Test-ReleaseInput.ps1:22; spec FR-007; the comment at lines 20-21.
- Measured: `Get-ChildItem -File` on Linux returns `a.nupkg` but not `.hidden.snupkg` or `sub/x.snupkg`
  (challenge-work/hid).
- This is harmless for the push: only recorded paths are pushed, and `--no-symbols` stops the sibling `.snupkg` lookup. The claim
  is still broader than the code.
- Fix: use `-Force -Recurse` (and refuse directories), or narrow the wording to "non-hidden top-level files".

### PC2-F7 (P3) The test-only switches in the production script are not guarded

- Where: eng/Publish-CanonicalRelease.ps1:10-13, 71, 92.
- They cannot be reached from the workflow. publish.yml:75,92,97 passes fixed arguments, the only input is `version`, it is
  regex-checked (publish.yml:37) and it reaches pwsh through `$env:`, not interpolation. So the risk is a future workflow edit,
  which would go through review.
- Plan and Verify also accept a plain-HTTP `-PackageContent` without any switch, so `-AllowInsecureTestFeed` only gates the push.
- Fix (optional): refuse `-SkipSignatureVerification` and `-AllowInsecureTestFeed` unless `FeedIndex`/`PackageContent` are on
  `http://127.0.0.1:`, or add a one-line guard test asserting publish.yml never names them.

### PC2-F8 (P3, partly open) CI placement and Windows

- Where: .github/workflows/ci.yml:37-39.
- The step runs in the `quality` job. On `push` to main, that is the job that produces the canonical artifact, and the step sits
  before `Pack`. The test feed picks a random port with no retry (Test-PublicationTooling.ps1:121-124), so a bind collision is a
  rare flake that fails a producer run. The fix is a small bind retry.
- Open: when I reviewed (`gh pr checks 15`, read only), both `quality (windows-latest)` runs on head 64a982d were still
  **pending**. FR-008 and plan.md say the suite runs "on Linux and Windows", but no Windows result exists yet. Merge must wait for
  it under T007 anyway.
- Inferred, not tested: on Windows, `HttpListener` with an `http://127.0.0.1:port/` prefix goes through http.sys, which can need a
  URL ACL for non-admin users. GitHub runners are elevated, so CI is unaffected; a local run on Windows without elevation may fail.

## Checked and holding (the defender's side, confirmed by me)

- **Line-number arithmetic.** The real `--verbosity detailed` output (verify-sharpqrs.txt) has the signer line at line 12; `Select-String`
  returns `LineNumber` 12, so `$output[12..16]` is lines 13-17: Subject, SHA1, SHA256, Issued by, Valid from. The SHA256 line falls
  inside the window. If the signer line is the last line, the reversed range yields no Subject line and the script throws, which is
  fail-closed.
- **Author-signed package.** Newtonsoft.Json 13.0.3: the first `Signature type` is `Author`, so the script refuses it (measured).
  Canonical packages are unsigned, and the input check refuses a canonical `.signature.p7s`.
- **Repository countersignature.** The real text is `Verifying the repository countersignature with certificate`, which does not
  match the required `repository primary signature` phrase, so it is refused. The timestamp block
  (`Verifying repository primary signature's timestamp …`) does not match either; the leading "the" differs.
- **Multiple certificate blocks.** Chain certificates appear only on `trace:` lines, which the `^\s*Subject Name` anchor excludes.
  Only the first signer line is used.
- **Real signed package.** SharpQrs passes with fingerprint `1f4b311d…482d`, matching manual-checks.md.
- **The 409 rule cannot publish wrong bytes.** Verify has no `if:` condition, runs after a successful push, and compares content
  and signature. A conflicting package that is accepted later is refused at Verify (inferred from code plus the conflict cases).
- **Suite run once.** `pwsh -NoProfile -File eng/Test-PublicationTooling.ps1 -WorkRoot …/challenge-work/suite`: PASS 16/16, exit 0
  (suite-out.txt, 2026-11-02T02:01:41Z to 02:01:45Z). The 16 cases match the spec's count (grep).
- **Mutation log.** It is consistent with the cases; the "symbols-allowed" survivor is explained by the second guard.
- **GH_TOKEN scope.** It is now step-scoped (publish.yml:50-51), as the PC-F5 disposition says.

## Weaknesses of this challenge

- F1 is fail-closed and unlikely on any single run: the planned version 0.2.0-alpha.7 has no `409`, and timing hits are about one
  in a thousand failed pushes. A defender can fairly call it P3. I keep P2 because it contradicts FR-004 and the PC-F7 disposition,
  and it would turn a 403 into an hour with the wrong diagnosis.
- I did not reproduce an exactly-409 ms response; F1's timing variant is inferred from the measured output format.
- I did not run the F3 mutation; that finding comes from inspecting the cases.
- I did not test the Windows `HttpListener` behavior, and Windows CI was still pending.
- F4 to F7 are robustness and wording issues with no effect on the planned 0.2.0-alpha.7 run.

## Artifacts (challenge-work, sha256)

probe-409.ps1 01b21af4…c566 · probe-409-out.txt 1e05027c…25e · raw-push.ps1 c2e2a4ec…6c7 · raw-push-403.txt d56b4ba0…0f0 ·
e2e-verify.ps1 4fda524d…b90 · e2e-verify-out.txt f6bebcc9…b2b · verify-sharpqrs.txt 830d73fd…2b2 · verify-newtonsoft.txt
cfc9d371…25e · suite-out.txt d657c183…25e
