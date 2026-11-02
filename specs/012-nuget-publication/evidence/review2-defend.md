# Defender review, round 2: PR 15 fix diff dc9c67c..64a982d

Role: defender (argue that the fixes are sufficient where the evidence supports it). Repository read-only at
`64a982d5a34c0cdd9d542f9d4a59c1e9e49efac8` (branch `feature/012-nuget-publication`, clean tree before and after).
Frozen packet checked against `../frozen.sha256`: challenge.md `87cc27fc…`, defend.md `0bf2cdcb…`, fix.diff `e6529aca…`, all three OK.

## Verdict: merge after the required CI is green; publication of 0.2.0-alpha.7 is not put at risk by the fixes

Every round-1 disposition is implemented in code, tests and docs as stated. I found no P0, P1 or P2 defect.
There are three P3 findings: one is a loose 409 classifier and two are about the wording and coverage of the evidence.
None of them can publish wrong bytes or block the alpha.7 run.

Open gate, not a finding: at review time (about 02:01Z) the required checks `quality (ubuntu-24.04)` and `quality (windows-latest)`
on the head were `pending`, and `mergeStateStatus=BLOCKED`. The new CI step has therefore not run on Windows yet. T007 is still open
for this reason.

## What I measured

- The test suite was run once:
  `pwsh -NoProfile -File eng/Test-PublicationTooling.ps1 -WorkRoot …/defend-work/tooling-run`. It ran on head 64a982d with
  pwsh 7.6.9 on Linux and gave `Status PASS, Passed 16, Failed []`, exit 0. Log: `defend-work/tooling-run.txt`.
- `eng/Test-RepositorySignature.ps1` was run with SDK 10.0.403, the same version that `global.json` pins (`rollForward: disable`) and
  therefore the same SDK the ubuntu-24.04 runner installs. The packages were copies from `~/.nuget/packages`:
  - `monstertacle.sharpqrs.0.2.0-alpha.1.nupkg` passes, exit 0. Signer
    `CN=NuGet.org Repository by Microsoft, O=…, C=US`, certificate SHA-256 `1f4b311d…bc482d`. This matches manual-checks.md.
    `dotnet nuget verify` uses the SDK fallback bundle `sdk/10.0.403/trustedroots/codesignctl.pem` and `timestampctl.pem` on Linux.
    The chain goes to DigiCert Trusted Root G4, which is in that bundle, and the result is "Successfully verified".
  - `newtonsoft.json` 13.0.1 and 13.0.3, and `xunit.core.2.9.3`, are refused with "No repository primary signature". These are
    author-signed packages where the repository signature is only a countersignature. The canonical packages are unsigned, so this
    is the intended strictness.
  - The full verify output is in `defend-work/verify-sharpqrs.txt`.
- The receipt enrichment in Verify mode (`Publish-CanonicalRelease.ps1:92-96`) is not covered by any test, because every Verify
  case uses `-SkipSignatureVerification`. I reproduced it in isolation under StrictMode Latest against the real signed package.
  Adding `Signer` and `SignerCertificateSha256` to the `[ordered]` receipt and serializing it works.
- The NuGet HTTP log format was read from `NuGet.Protocol.dll` of SDK 10.0.403. The string is `{0} {1} {2}ms`, which prints the
  status, the URL and the elapsed milliseconds. PD2-F1 depends on this.
- The commit 64a982d has the author and committer `Alexander Bleissem <alexander@bleissem.com>` and no trailers.
  The added lines contain no German text. The only non-ASCII characters are `…` and `—` in Markdown.

## Dispositions against code, tests and docs

| Round-1 finding | Holds? | Basis |
| --- | --- | --- |
| PC-F1 plan/evidence/tests | yes, with the caveat in PD2-F2 | `plan.md` has a deep dive, decisions, a constitution check and a test plan. `evidence/` has Red, Green, mutation and manual logs. `ci.yml:37-39` runs the suite in the `quality` matrix (Linux and Windows). The non-TDD first version is admitted in `plan.md:31-33`. |
| PC-F2/PD-F1 signature | yes | The comparator now refuses a served package without `.signature.p7s` (`Test-PublishedPackage.ps1:36`), and a test plus a mutation probe cover it. `Test-RepositorySignature.ps1` requires exit 0 from `dotnet nuget verify --all`, `Signature type: Repository` as the first signature, and a subject starting with `CN=NuGet.org Repository by Microsoft,`. It reports the SHA-256 fingerprint. I confirmed it on a real nuget.org signature on Linux with the pinned SDK. The fingerprint is not pinned, which is correct: a certificate rotation must not break publication. |
| PC-F3/PD-F4 rerun after partial push | yes (see PD2-F1) | `Publish-CanonicalRelease.ps1:72-76` marks a 409 `pending` and continues. Verify (`:84-98`) still requires every recorded package to be served, EQUIVALENT and signed. The docs (`publishing.md:37-39`) now describe this. The test "push: a 409 … is pending" passes, and the `no-pending` mutation turns it red. |
| PC-F4 symbols | yes | `--no-symbols` is passed at `:70`. `Test-ReleaseInput.ps1:20-26` refuses unrecorded files, and the count check also refuses them. Test and mutation are in place. Removing only one guard stays green, and the evidence discloses this. |
| PC-F5/PD-F5 token scope | yes | `GH_TOKEN` is now step-scoped (`publish.yml:50-51`). `id-token: write` stays job-level as an explicit decision (`plan.md:21-23`). |
| PC-F6/PD-F2/PD-F3 guide | yes | `NUGET_USER` is called a repository secret, "repository primary signature" replaces "countersignature", the root-scope note was added, and the private-repository sentence was removed (`publishing.md:16-19, 33-36`). |
| PC-F7 minor | accepted as stated | These are owner choices or accepted risks. The StrictMode `Exception.Response` message for a non-HTTP failure (`:34`) is unchanged. The run still fails, so this is cosmetic. |

## Red/Green/mutation honesty

- The Red log (`red-publication-tooling.txt`, 01:53:52Z) is consistent with the scripts at dc9c67c. Of 16 cases, 13 passed and 3
  failed. The failures were the symbol package (dc9c67c counted only `*.nupkg`), the unsigned comparison (dc9c67c returned
  EQUIVALENT) and the push (plain-HTTP refusal).
  - The two Verify cases "passed" trivially. Each expects a refusal, and the parameter `-SkipSignatureVerification` did not exist
    at dc9c67c, so parameter binding threw.
  - `manual-checks.md:18-23` discloses both non-domain effects correctly.
- The timestamps are in order: Red 01:53:52Z, mutation 01:56:54Z, Green 01:57:46Z. My own run reproduced the Green result.
- I did not reproduce Red, because only one tooling run was allowed. The consistency argument above is reasoning, not a measurement.

## Findings

### PD2-F1 (P3) The 409 classifier matches any "409" in the push output and discards the output (`eng/Publish-CanonicalRelease.ps1:74-75`)

- `Select-String -SimpleMatch '409'` searches all output lines. NuGet prints `  <Status> <url> <elapsed>ms` for every HTTP response
  (format `{0} {1} {2}ms` in `NuGet.Protocol.dll`). A non-409 failure whose elapsed time contains "409" is therefore classified as
  `pending`, for example `Forbidden https://www.nuget.org/api/v2/package/ 1409ms`. That affects roughly 0.3 % of responses between
  0.1 and 5 s.
- The `pending` branch also never writes `$output`. A genuine 409 (for example a deleted version that will never be served) and a
  misclassified 403 are both reported only as `"State": "pending"`.
- Scenario: the first trusted-publishing push gets a 403 because of a policy or scope mistake, and the request happens to take
  1409 ms. The run continues. Verify waits the full 60 minutes and fails with "not served", and the 403 text is lost.
  The risk is low in practice. The other seven pushes would fail with the same 403 and print it, and no wrong bytes can be
  published, because Verify still requires every package to be served, equivalent and signed.
- Fix: match the status line, for example the regex `\b409 \(Conflict` or `^\s*Conflict https?://`. Also write `$output` in the
  pending branch, and add a test-feed case where a 403 is answered.

### PD2-F2 (P3) `plan.md:33-34` says that all review fixes "were driven by tests that failed first"

- Only two fixes have a domain Red: the symbol package and the unsigned comparison.
  - The 409 rule failed first for an infrastructure reason (the plain-HTTP refusal). Constitution `:150` says that is no Red
    evidence.
  - The Verify-unsigned case did not fail first.
  - `Test-RepositorySignature.ps1` has no automated test at all (`plan.md:47` says "manual").
- The caveat is in `manual-checks.md`, and the mutation probes do show the domain failure (`no-pending` gives "Push failed").
  `plan.md` and `tasks.md:13` ("Red before the fixes") state the claim without that limit.
- Fix: in `plan.md:33-34`, name the two domain Reds and point to the caveat and the mutation substitutes. This is a wording fix;
  no code change is needed.

### PD2-F3 (P3) Untested production-only paths

- No test or mutation covers:
  - the removal of `--no-symbols`;
  - the signature branch of Verify, which every test skips with `-SkipSignatureVerification`;
  - a positive Verify path, because both Verify cases are refusals and a refusal for an unrelated reason would also pass.
- Mitigation:
  - `--no-symbols` is defense in depth behind two input guards that are tested.
  - The signature script is checked manually, and in this review, on the real nuget.org signature with the pinned SDK on Linux.
    The receipt enrichment is checked by my isolated reproduction.
- Fix (optional): add a positive Verify case with `-SkipSignatureVerification`, and run `Test-RepositorySignature.ps1` in CI
  against a committed, real nuget.org-signed fixture, for example SharpQrs 0.2.0-alpha.1. Add a mutation probe for `--no-symbols`.

## Publication risk for 0.2.0-alpha.7

- **Signature check on the Linux runner.** This works with SDK 10.0.403 on Linux and the fallback trust bundle, using the real
  nuget.org signature of a package from the same owner that was uploaded unsigned. The certificate is valid until 2027-05-19. The
  parser depends on the English CLI output. That output is deterministic because the SDK is pinned and the runner locale is not
  localized. Revocation checking needs network access, which the runner has.
- **60-minute Verify timeout vs nuget.org indexing.**
  - Shared deadline for all packages, poll every 30 s; job timeout 90 min.
  - nuget.org validation and indexing usually finish well inside 60 min, but for eight new IDs it is not guaranteed.
  - If the limit is exceeded, the bytes are already pushed and only the receipt is missing. A rerun then works:
    - Plan sees served packages as present, compares them and stops on a conflict.
    - Packages that are not yet served are absent. Login and push follow, push gets 409, and the package is marked `pending`.
    - Verify runs again.
  - This is the case the PC-F3 fix exists for, and it is tested.
  - Residual risk: the receipt may need a second run. Publication itself is not at risk.

## Constitution

- Plan and evidence are present.
- English throughout.
- No Python in the change. The empty `python3` invocation is disclosed (`manual-checks.md:25-26`).
- Least privilege improved (`GH_TOKEN`); the job-level `id-token` is a documented decision.
- TDD is satisfied only partly, as admitted (PD2-F2).

## Weaknesses of my position

- The quality CI on the head is still pending. The HttpListener and ThreadJob test feed on `windows-latest` has not been observed
  green by anyone. I infer that it works because the runners run as admin (no URL ACL is needed); I did not measure it.
- Red was not reproduced (one-run limit). Its honesty rests on a consistency argument.
- For two of the three behaviors the constitution's strict domain-Red rule is not met. Mutation probes replace them, and the
  constitution does not name mutation probes as a substitute. A strict reader can call PD2-F2 a P2.
- The signature check was proven on one real package (SharpQrs) only. The text-parsing approach is brittle across SDK upgrades,
  although the SDK is pinned.
- PD2-F1's probability estimate is my own arithmetic over the log format, not a measurement against nuget.org.

## Duration and tool calls

Start 2026-11-02T01:59:40Z, end 2026-11-02T02:02:12Z (both `date -u`), plus writing this report. 15 tool calls including this write.
Work dir: `<private staging directory>`. It holds `pkgs/` (copies of cached packages),
`tooling-run.txt` and `verify-sharpqrs.txt`.
