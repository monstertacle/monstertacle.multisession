# Challenge review: PR 15 (monstertacle/monstertacle.multisession), head dc9c67c29800e6897cf3256a5076fe0a29937c39

Role: challenger (argue against merge and run). Base: origin/main e15850787e290e1ee9b49226efa4a17ed89138f8.
Evidence: frozen set verified against ../frozen.sha256 (candidate.diff 52d45b2d..., all OK). Read-only; no edits, dispatches or logins.

## Verdict: fix-first

No P0 found. The credential path and the byte-binding design hold up (see "What I could not break").
Merge should still wait: the repo constitution requires change evidence and TDD that this PR does not
have. Two owner-required properties are also claimed but not implemented (repository-signature provenance,
rerun after a partial push), and `dotnet nuget push` would publish unrecorded `.snupkg` bytes whenever a
producer artifact contains them. On the PR head, the required checks `quality (ubuntu-24.04)` and
`quality (windows-latest)` were still `in_progress` (mergeable_state `blocked`) at review time.

## Findings

### PC-F1 (P1) No change evidence or tests, against the constitution (specs/012-nuget-publication/, eng/*.ps1)
- Rule: `.specify/memory/constitution.md:28-46` (II) requires documented code deep dive and architecture evidence
  (source hash, file/symbol evidence, planned tests) *before* implementation. `:146-160` (IV) requires TDD with an observed
  domain Red and Green in the change evidence. `AGENTS.md` restates both. The owner authorization asks for the work to be
  "qualified ... under existing SDK SpecKit conventions".
- Fact: `specs/012-nuget-publication/` holds only `spec.md` and `tasks.md`. It has no `plan.md`, `research.md` or `evidence/`,
  unlike 007 and 009. None of the three new scripts is called by any test or CI workflow. grep finds them only in
  `publish.yml` and in one internal call.
  T005 is ticked ("refusal cases and a local test feed"), but the PR contains no evidence of it. The frozen evidence shows only
  the PASS input check and an all-absent plan. Neither a refusal run nor a CONFLICT/EQUIVALENT classification is shown.
- Scenario: the first time the comparator (`Test-PublishedPackage.ps1`) and the conflict path ever run against a present package
  is in production, on an immutable public version.
- Fix: add `evidence/` (source state, analysis, Red/Green commands and outputs) and committed Pester or script tests:
  - an altered entry, an added entry and a missing entry each give CONFLICT;
  - an added `.signature.p7s` gives EQUIVALENT;
  - a byte, manifest, version or commit mismatch is refused;
  - Plan on a local flat-container fixture gives absent, present and conflict.
  Wire them into CI.

### PC-F2 (P2) "Verify" does not verify the repository signature (eng/Test-PublishedPackage.ps1:270-286; Publish-CanonicalRelease.ps1:221-233)
- `RepositorySigned` only reports whether an entry named `.signature.p7s` exists. Nothing parses or validates that entry, checks
  the signer, or requires the flag to be `true`. An unsigned served package passes as EQUIVALENT, and so does a package carrying
  an arbitrary `.signature.p7s` blob.
- The owner requires: "verify payload and provenance using official NuGet repository-signing rules" and "server-returned signed
  package ... verification". The receipt's `Status: PASS` will be read as that verification.
- Fix:
  - Require `RepositorySigned -eq $true` in Verify.
  - Run `dotnet nuget verify --all` on each served file and check the repository signer. The real signed SharpQrs shows
    `CN=NuGet.org Repository by Microsoft`, a commitment-type attribute and the v3 service index URL
    `https://api.nuget.org/v3/index.json`.
  - Put the signer certificate fingerprint in the receipt.

### PC-F3 (P2) A rerun after a partial push fails until nuget.org serves the package, contradicting the docs (docs/publishing.md:146; Publish-CanonicalRelease.ps1:175-187, 208-219)
- Plan treats "not in flat-container `index.json`" as absent. A pushed package that is still in nuget.org validation or
  indexing, or whose validation failed, is absent to Plan but reserved on the server.
- Scenario: Verify hits its 60-minute deadline, or the job is cancelled after push 3 of 8. The operator reruns as the docs
  suggest. Plan marks package 3 absent, push gets HTTP 409, `$LASTEXITCODE != 0` throws, and the loop stops before packages
  4-8. Every rerun fails until package 3 is served.
  This is safe: there is no wrong-bytes push and no skip-duplicate. But the docs promise "published packages are recognised as
  present and only the remaining ones are pushed", which is false in exactly the case where reruns are needed.
- Fix: on a 409 from push, record `pending`, continue with the remaining packages, and let Verify decide. The package must
  become served and compare EQUIVALENT, otherwise the run fails. This keeps "never skip a conflict", because the conflict
  decision moves to the content comparison instead of the status code. Alternatively, correct the docs.

### PC-F4 (P2) `dotnet nuget push` also pushes an adjacent `.snupkg` that is not in the record (Publish-CanonicalRelease.ps1:216; Test-ReleaseInput.ps1:315)
- When the source is nuget.org, `dotnet nuget push X.nupkg` automatically pushes `X.snupkg` from the same directory.
  Test-ReleaseInput counts only `*.nupkg`, so a symbols file in `packages/` is neither recorded, nor hashed, nor rejected.
  Verify never looks at symbols either.
- For alpha.7 this does not occur: the downloaded artifact has 0 `.snupkg`, and ci.yml and the props files have no
  `IncludeSymbols`. The workflow is generic per version, though, and FR-002 and the docs say only recorded bytes are published.
- Fix: pass `--no-symbols` to the push, and make Test-ReleaseInput reject any non-`.nupkg` file in `packages/`.

### PC-F5 (P3) The OIDC request capability covers every step, not only the login step (publish.yml:26-38, 84)
- `id-token: write` is set at job level, so `ACTIONS_ID_TOKEN_REQUEST_*` is available to every step. That includes checkout,
  setup-dotnet (which downloads and runs the SDK installer), the repo's pwsh scripts and the dotnet CLI. It is available even
  when `push == 'false'`. The comment "The only step that uses the OIDC token" describes intent, not enforcement.
  `GH_TOKEN` (contents/actions read on a public repo) is also job-wide and visible to `NuGet/login`.
- The owner constraint "only publication job gets id-token:write" is met literally.
- Optional fix: split into a `plan` job (no environment, no id-token) and a `publish` job (environment nuget, id-token)
  conditioned on `needs.plan.outputs.push`. The publish job must re-download and re-hash the packages, which the Push step
  already does. Also scope `GH_TOKEN` to the one bash step.

### PC-F6 (P3) Docs and spec misstate facts (docs/publishing.md:128, 143; spec.md FR-005)
- `NUGET_USER` is a **repository** secret. `gh api .../environments/nuget/secrets` lists nothing, while `actions/secrets` lists
  `NUGET_USER`. The docs say the environment holds it.
  `secrets.NUGET_USER` resolves either way, so the workflow is fine; only the docs are wrong.
- "Repository countersignature": for an unsigned package, nuget.org adds a primary *repository signature*, not a
  countersignature. The real SharpQrs 0.2.0-alpha.1 `.signature.p7s` has signer `NuGet.org Repository by Microsoft` and OID
  1.3.6.1.4.1.311.84.2.1.1.1 with value `https://api.nuget.org/v3/index.json`.
- The package scope `Monstertacle.Multisession.*` does not match the root ID `Monstertacle.Multisession`. The owner explicitly
  asked to verify the policy covers "root Monstertacle.Multisession as well as dotted children". The alpha.7 set has no root
  package, so this is not a blocker for this run, but the documented scope contradicts the owner's stated expectation.
- The "private repository ... temporary" sentence is moot: the repo is public. Per the official docs the policy is temporarily
  active for 7 days, not "until first publication".

### PC-F7 (P3) Small robustness gaps (Publish-CanonicalRelease.ps1:178-181, 216; publish.yml:99-104)
- Under `Set-StrictMode -Version Latest`, a non-HTTP failure (DNS or TLS) makes `$_.Exception.Response` itself throw
  "property 'Response' cannot be found" (observed in pwsh 7.6.9). The run still fails, but with a misleading message.
- The API key is passed as an argv value (`--api-key $env:NUGET_API_KEY`), visible in the process table of the runner for
  the push duration. The runner is ephemeral and the value is masked in logs.
- If Push fails mid-loop, Verify does not run, so the summary has no receipt of what was already pushed. Consider an
  `if: always() && steps.plan.outcome == 'success'` Verify in report-only mode.
- The environment `nuget` has only a branch rule and no required reviewer. Either admin (`bleissem`, `catWithATemper`) can
  dispatch an irreversible publication.

## What I could not break (weaknesses of my position)
- **Injection:** none. `inputs.version` reaches scripts only via env, is regex-checked in bash, and contains no `/`.
  `persist-credentials: false` is set. Top-level `permissions: {}`.
- **Action pins:** all three are real tag commits. checkout v4.4.0 = 11d5960a, setup-dotnet v4.3.1 = 67a3573c,
  NuGet/login v1.2.0 = 8d196754.
- **NuGet/login:** at the pinned commit, `src/index.ts` masks the request token, the OIDC token and the API key. It exchanges
  with `https://www.nuget.org/api/v2/token`, audience `https://www.nuget.org`. I did not diff `dist/index.js` against `src`.
- **Non-main ref, fork or PR:** `workflow_dispatch` only, plus `if: github.ref == 'refs/heads/main'`. The environment branch
  policy is `main` (type branch, verified via API). A branch copy of the workflow is blocked by the environment rule, assuming
  the nuget.org policy sets the environment, which the docs say it does.
- **Producer run:** run 41333282133 matches every jq predicate: path `.github/workflows/ci.yml`, event push, branch main,
  head_sha 8167d37, success, attempt 1. Artifact `packages-and-consumer-linux` expires 2027-01-30, so it is not a time risk.
- **Byte binding:**
  - The record pins the manifest digest and each package SHA-256.
  - Push re-hashes immediately before each push. The remaining TOCTOU is between hash and dotnet reading the file, which
    would need hostile code already in the job.
  - Artifact-name collisions or another run's artifact cannot change bytes without failing the hash check.
- **Comparator soundness for nuget.org signing:**
  - The served SharpQrs 0.2.0-alpha.1 (sha256 e52114de...) has exactly the producer entry layout plus `.signature.p7s`.
  - Its `[Content_Types].xml` has no `p7s` type, so nuget.org did not change it.
  - I found no unsigned original of SharpQrs locally, so a per-entry before/after diff was not possible. The claim that only
    `.signature.p7s` changes is consistent with the evidence but not proven by a pair.
- **Dependency SharpQrs:** the producer's `packages.lock.json` pins contentHash `PuXg49yL...`, identical to the nuget.org
  download in `~/.nuget/packages` (`.nupkg.metadata` source api.nuget.org). The canonical consumer used the same signed
  package nuget.org serves. Not a finding.
- **IDs on nuget.org:** all 8 IDs and the root ID give 404 now. Owner `bleissem` holds only ReflectionMapper and SharpQrs*.
  No visible collision.
- **PowerShell:** `[ordered] + [ordered]` works in pwsh 7.6.9. A 404 surfaces as `HttpResponseException` with status 404, so
  the absent path is correct. `"$env:RELEASE_VERSION.json"` expands correctly. I did not check that the pwsh version on
  ubuntu-24.04 has the same `[ordered]` addition behavior.
- **Severity:** PC-F1 is a process and constitution gate. A defender can argue that build and CI scripts fall under
  "suitable validation" rather than behavioral TDD. PC-F3 and PC-F4 cannot publish wrong bytes. PC-F2 can be closed by a
  post-run manual check outside the workflow.

## Duration and effort
- Start 2026-11-02T01:47:13Z, end 2026-11-02T01:51:28Z (both `date -u`). About 4 minutes of wall-clock time for tool work.
- 21 tool calls, including this write.
- Work dir: <private staging directory> (tp.html, dl/sq.nupkg, dl/sig.p7s).
