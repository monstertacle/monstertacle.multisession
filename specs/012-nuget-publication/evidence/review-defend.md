# Defender review: PR 15 publish workflow (frozen head dc9c67c29800e6897cf3256a5076fe0a29937c39 vs origin/main e15850787e290e1ee9b49226efa4a17ed89138f8)

Role: defender (argue ready-to-merge where the evidence carries it). Read only; nothing edited, pushed, dispatched or logged into.
Frozen evidence digests re-checked: `sha256sum -c ../frozen.sha256` OK for all 5 files.

## Verdict

**Ready to merge and run, no P0/P1.** One P2 (served-package provenance is only "signature entry present", not a verified
nuget.org repository signature) and four P3 text/hardening items. None of them can lead to wrong bytes on nuget.org, a reissue,
a skipped conflict, or a key leak. The P2 is cheap to fix before the run (one `dotnet nuget verify` call). If it is not fixed, the
owner's required provenance evidence has to be produced after the run instead (see "Weaknesses").

## Constraint-by-constraint (measured / derived / open)

| Owner constraint | Evidence | Result |
| --- | --- | --- |
| Manual-only from trusted main | publish.yml:6-12 only `workflow_dispatch`; :22 `github.ref == 'refs/heads/main'` and repo pin; :24 `environment: nuget` (owner setup JSON: branch policy main only); :63 producer commit must be an ancestor of HEAD. Measured: `8167d37` is an ancestor of origin/main. | met |
| Pinned official login action | Measured with `git ls-remote`: NuGet/login `8d196754…` = tags `v1.2.0` and `v1`; checkout `11d5960a…` = `v4.4.0`; setup-dotnet `67a3573c…` = `v4.3.1`. | met |
| Only the publication job gets id-token:write | :14 `permissions: {}`; the single job (:26-29) has `contents: read`, `actions: read`, `id-token: write`. | met at job level (see PD-F5) |
| Exact canonical artifact, commit/run/version/hash/nuspec, no repack | publish.yml:49-64 checks run `head_sha`, `conclusion`, `event`, `head_branch`, `path`, repository. Measured via public API for run 41333282133: `8167d37…`, `success`, `push`, `main`, `.github/workflows/ci.yml`, `monstertacle/monstertacle.multisession` — every predicate holds. Artifact `packages-and-consumer-linux` not expired (expires 2027-01-30T17:47:42Z). Test-ReleaseInput.ps1 checks manifest digest, producer hash list, file count, per-file SHA-256, single nuspec, no `.signature.p7s`, nuspec id/version/repository commit. Reproduced locally on a copy of the real artifact: PASS; appended byte -> refused ("Canonical package bytes differ from the producer"); record commit changed -> refused ("Package metadata differs"); extra nupkg -> refused. Record hashes equal the owner-referenced `packages-alpha7-8167d37/canonical-alpha7.json` (all 8, same artifact name, manifest digest, run, commit). Push re-hashes each file right before pushing (Publish-CanonicalRelease.ps1:61-63). No pack/build step exists. | met |
| No skip-duplicate of a conflict, no reissue | Plan (Publish-CanonicalRelease.ps1:46-55) compares any served version; Test-PublishedPackage.ps1:48 throws on any difference, which stops the Plan step before login. Push (:64) has no `--skip-duplicate`, so a 409 fails. Version comes only from a committed record; no version bump path exists. | met |
| Short-lived auth | Login only when a package is absent (:80), immediately before push (:79-91); key passed in step env only. Official docs: key valid 1 hour. | met |
| Secret confidentiality | NuGet/login src at the pinned SHA calls `core.setSecret` on request token, OIDC token and API key before `setOutput`. `persist-credentials: false`. Version input goes through env and a strict regex (:38), so no script injection and no path traversal into `eng/release-records/`. `NUGET_USER` is a profile name, not a credential. | met |
| Served-package verification honest about repository signing | Downloaded the real nuget.org package monstertacle.sharpqrs 0.2.0-alpha.1 (sha256 `e52114de…`): exactly one added entry `.signature.p7s`, all other entries unchanged in name. CMS inspection: single signer `CN=NuGet.org Repository by Microsoft`, commitment `proofOfReceipt`, service index `https://api.nuget.org/v3/index.json`, owners `bleissem`, no countersignature attribute. Comparator probes on that package: stripped copy vs served -> `EQUIVALENT`, `RepositorySigned: true`, 8 entries compared; changed DLL -> `CONFLICT changed lib/...dll`; added entry -> `CONFLICT added extra.txt`; signed package as canonical -> refused. So a repository signature is not misclassified as a changed assembly. But the signature itself is not verified: PD-F1. | mostly met |
| Spec/docs match workflow | FR-001..FR-006 match the YAML and scripts line by line, except the items in PD-F2, PD-F3, PD-F4. | mostly met |
| Repo rules (English, no Python, neutral metadata) | All added text English; no Python; commit author/committer `Alexander Bleissem <alexander@bleissem.com>`, no trailers, no tool or model names. `Owner: implementation agent (SDK writer)` follows the existing convention (specs 010, 011). | met |
| Feed state | Measured now: flatcontainer `monstertacle.multisession.common` and `monstertacle.multisession` both 404, consistent with local-plan "all 8 absent". | consistent |

Note: the owner authorization supersedes the setup JSON's "no package push until actual release request"; the authorization
explicitly requests the alpha.7 publication, so a merge followed by one manual run is inside the authorized actions.

## Findings

### PD-F1 (P2) Served-package provenance is presence of `.signature.p7s`, not a verified repository signature
- Where: eng/Test-PublishedPackage.ps1:32 (`$signed = $publishedEntries.ContainsKey($signatureEntry)`), receipt field
  `RepositorySigned` in eng/Publish-CanonicalRelease.ps1:41; publish.yml:93-98.
- Scenario: the receipt says `RepositorySigned: true` for any served archive that carries an entry named `.signature.p7s`, whatever
  its content or signer. The owner authorization asks to "verify ... provenance using official NuGet repository-signing rules".
  The payload check itself is sound (every other entry byte-compared), so no wrong DLL can pass; only the provenance claim is
  stronger than what is checked.
- Fix (small): in Verify, run `dotnet nuget verify --all <served>` (SDK is already installed by setup-dotnet, Linux trust store
  ships with .NET 8+) and optionally require the signer subject `NuGet.org Repository by Microsoft` / `--certificate-fingerprint`;
  or rename the field to `SignatureEntryPresent` and record that cryptographic verification happens in the post-run evidence step.
- Why not P1: no constraint about bytes, auth or reissue is touched; the owner's required evidence list can still be produced
  after the run from the served packages.

### PD-F2 (P3) "countersignature" is the wrong term
- Where: docs/publishing.md:33, specs/012-nuget-publication/spec.md:31, eng/Test-PublishedPackage.ps1:6.
- Scenario: the canonical packages are unsigned, so nuget.org applies a **primary** repository signature. Measured on the real
  served SharpQrs package: one signerInfo, commitment `proofOfReceipt`, no countersignature attribute. A reader verifying
  "countersignature" would look for an author signature that does not exist.
- Fix: "repository signature" throughout.

### PD-F3 (P3) docs place `NUGET_USER` in the environment; the owner set it as a repository secret
- Where: docs/publishing.md:18.
- Scenario: owner-nuget-github-setup-confirmed.json records `repositorySecret: NUGET_USER`. The workflow works either way (job
  sees both), but the doc misdescribes the actual setup.
- Fix: "the repository secret `NUGET_USER`" (or move the secret into the environment, which would be the stricter choice).

### PD-F4 (P3) re-run statement and package scope glob are slightly broader than the mechanism
- docs/publishing.md:36: a re-run recognises packages as present only once the flat container serves them. Re-run during indexing
  lag -> Plan says absent -> push gets 409 -> run fails. Safe (fail-closed), but the sentence should say "after they are served".
- docs/publishing.md:16 / spec.md:34: scope `Monstertacle.Multisession.*`. The owner asked to verify the account policy covers
  the root ID `Monstertacle.Multisession` as well as dotted children. The alpha.7 set has no root package
  (Test-ReleaseInput.ps1:24 even requires at least one dotted segment), so nothing breaks for this release; whether the glob
  matches the bare root is not verifiable without the account. Fix: document the actual configured glob after checking the
  account page, or state that the root ID is out of this release.

### PD-F5 (P3, hardening, optional) OIDC request capability spans all steps of the job
- Where: publish.yml:21-98.
- Scenario: `id-token: write` exposes `ACTIONS_ID_TOKEN_REQUEST_*` to checkout, setup-dotnet, `gh`, and the repo scripts, not only
  NuGet/login. All are SHA-pinned first-party actions or main-reviewed scripts; GitHub cannot scope it per step. The constraint
  says "only the publication job", which is satisfied literally.
- Fix if desired: split into a `verify` job (no id-token, uploads the verified packages) and a `publish` job (id-token, re-hashes
  against the record, which Push already does). Not needed for merge.

## Weaknesses of the defender position

- Account side not verifiable: I cannot see the nuget.org policy (owner, repo, workflow file, environment, glob, policy creator
  vs `NUGET_USER`). The first real OIDC exchange is the only test; owner forbids a dispatch as an auth test. If it refuses, the job
  fails at login with nothing pushed (safe, but the run is "spent").
- Present-equivalent and present-conflict Plan paths were not exercised against a feed by me. I verified only their building blocks:
  the comparator (on a real signed package) and the `[ordered] + [ordered]` merge plus `Where-Object State -eq 'absent'` in pwsh 7
  (returns OrderedDictionary, count 1). The spec's "local test feed" evidence is not in the frozen set; T005 is ticked without a
  committed evidence file.
- The equivalence probe used a canonical made by deleting `.signature.p7s` from the served SharpQrs package, not the true
  pre-push original (none unsigned exists locally; 72 copies all `e52114de…`, signed). It proves the comparator's logic on nuget.org's
  real signing output, not that nuget.org left SharpQrs's other entries byte-identical (that is nuget.org's documented behaviour,
  not measured here).
- PD-F1 is arguably P1 if the owner reads "verify provenance" as a workflow duty rather than a post-run evidence duty. I rate it P2
  because the spec never claims cryptographic verification and the post-run evidence step is separately required.
- The `if:` skip on a non-main ref yields a "skipped" job, which a hasty reader might take for success; the environment branch
  policy is the real gate and is owner-confirmed, not measured by me.
- No CI result on the PR head was examined (out of my read scope); T007 still open.

## Run data

- Start 2026-11-02T01:47:22Z, end 2026-11-02T01:51:03Z (both `date -u`), about 4 minutes.
- Tool calls: 25 (incl. this write).
- Work files: <private staging directory> (login action source, docs copy, signed SharpQrs,
  CMS dump `sig/p7s.txt`, comparator and input-check probe outputs).
