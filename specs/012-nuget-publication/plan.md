# Plan: Publish canonical releases to nuget.org

Source state: `main` at e158507 (PR 14), producer commit 8167d37, producer run 41333282133.

## Deep dive (existing code)

- `.github/workflows/ci.yml`: the `quality` job packs the libraries and uploads `packages-and-consumer-<platform>`; the
  `canonical-consumer` job downloads that artifact and runs `eng/Test-CanonicalPackageHashes.ps1` (producer manifest status `PASS`,
  exactly eight packages, recorded digests), `eng/PackageSmoke.ps1` and `eng/Test-DeliveryConsumer.ps1`. This is the qualification of the
  package bytes; no workflow publishes them. No nuget.org publication path existed (checked: `.github/workflows` holds ci, codemetr-guard,
  dependency-audit and soak).
- The producer manifest is `package-smoke/<run>/manifest.json` inside the artifact; its digest for run 41333282133 is 0085125f….
- Every package depends on `Monstertacle.SharpQrs 0.2.0-alpha.1`, already on nuget.org (owner `bleissem`); the other dependencies are
  public Microsoft packages. The packages are unsigned and carry `repository commit=8167d37…` in their nuspec.
- nuget.org repository signing: a real signed package (Monstertacle.SharpQrs 0.2.0-alpha.1) carries the entries of the unsigned package
  plus `.signature.p7s`; `dotnet nuget verify --all` reports a repository primary signature by `CN=NuGet.org Repository by Microsoft`.

## Decisions

- Publish from the recorded producer run, never from a build in the publish workflow: the bytes the consumers qualified are the bytes
  published. The repository commits a release record so the workflow has an independent statement of what to publish.
- One job (verification, login, push, verification). Splitting into a verification job and a publication job would require passing the
  packages through a second artifact and re-verifying them; the login step runs only after the verification steps of the same job, and
  only pinned official actions run in the job.
- Duplicate handling is content-based: present-and-equivalent is left alone, present-and-different stops, a 409 during the push is
  pending until served and verified. No `--skip-duplicate`.
- PowerShell scripts (repository convention, no Python).

## Constitution check

- Principle IV: the scripts are release tooling with behavior; they get automated tests (`eng/Test-PublicationTooling.ps1`) in CI on
  Linux and Windows. The first version of the scripts was written before its tests (checked by hand against the real artifact, see
  evidence/manual-checks.md); that part is not TDD and is recorded as such. The review fixes (signature requirement, 409 pending, symbol
  and unrecorded-file refusal) were driven by tests that failed first (evidence/red-publication-tooling.txt) and by mutation probes.
- Principle V: least privilege (empty top-level permissions; job contents/actions read and id-token write; `GH_TOKEN` only in the step
  that reads the producer run); no stored key; inputs validated; secrets never printed.
- Principle VI: English throughout.
- No C# change; Codemetr and coverage are unaffected (the quality workflow still runs them).

## Test plan

| Behavior | Test (eng/Test-PublicationTooling.ps1) | Expected Red before the fix |
| --- | --- | --- |
| Unrecorded symbol package refused | input: unrecorded symbol package refused | accepted (only `*.nupkg` counted) |
| Unsigned served package refused | compare: unsigned served package is refused; verify: an unsigned served package is refused | equivalent |
| 409 for an accepted package is pending | push: a 409 for an accepted package is pending and the rest are pushed | push failed |
| Signature by the nuget.org certificate | eng/Test-RepositorySignature.ps1 against a real signed and an unsigned package (manual) | not checked |
