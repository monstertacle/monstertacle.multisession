# Feature: Publish canonical releases to nuget.org through trusted publishing

Status: done
Created: 2026-11-02T01:45:49Z
Owner: implementation agent (SDK writer)
Origin: owner authorization to publish the canonical 0.2.0-alpha.7 package set to nuget.org (recorded 2026-11-01T22:24:22Z); repository
side of the trusted publishing setup (account `bleissem`, environment `nuget` restricted to main, secret `NUGET_USER`).

## Problem

The packages exist only as CI artifacts and local feeds. Consumers cannot restore them from nuget.org, and there is no reviewed path
from a qualified producer run to a public feed. A long-lived API key would be a standing credential.

## Requirements

- FR-001: A manual-only workflow `.github/workflows/publish.yml` publishes one release named by its version. It runs only for
  `refs/heads/main` of this repository, in the `nuget` environment. Top-level permissions are empty; the single job has
  `contents: read`, `actions: read` and `id-token: write`. Actions are pinned to commit SHAs.
- FR-002: The release is defined by a committed record `eng/release-records/<version>.json`: producer repository, commit, run, workflow,
  artifact name, producer manifest digest and, per package, id, file and SHA-256. The workflow verifies that the producer run is a
  successful `push` run of `.github/workflows/ci.yml` on `main` at that commit, that the commit is an ancestor of the published head,
  and that the downloaded artifact has the recorded manifest digest, file set and bytes, and that each package's nuspec carries the
  recorded id, version and source commit and no signature (`eng/Test-ReleaseInput.ps1`). Nothing is built or repacked.
- FR-003: Before any login the workflow classifies each package on nuget.org: absent, or present with content equivalent to the
  canonical package. A present package with other content stops the run; it is never skipped as a duplicate and never replaced by a
  new identity (`eng/Publish-CanonicalRelease.ps1 -Mode Plan`).
- FR-004: Only when a package is absent, `NuGet/login` exchanges the job's OIDC token for an API key (valid one hour) immediately
  before the push; the key is passed in the step environment only. No long-lived key exists. Pushes use `--no-symbols` and do not use
  `--skip-duplicate`. A 409 answer (the version is already held, for example accepted by an earlier run but not yet served) marks the
  package pending; it is not treated as success: FR-005 still requires it to be served with the canonical content.
- FR-005: After the push the workflow waits until every package is served and compares the served package with the canonical one
  (`eng/Test-PublishedPackage.ps1`): the served package must carry the repository signature entry `.signature.p7s`, and every other
  entry must have the same name and content. The canonical packages are unsigned; nuget.org adds its repository primary signature,
  which changes the archive digest without changing any other entry. The signature is verified with `dotnet nuget verify --all` and
  must be a repository primary signature with a trusted chain whose certificate subject is the nuget.org repository; the fingerprint is recorded, not pinned, because the certificate rotates (`eng/Test-RepositorySignature.ps1`; English CLI output is forced for parsing). The receipt
  (served digest, compared entries, signer, certificate fingerprint) is written to the run summary.
- FR-007: The artifact's package directory holds exactly the recorded packages; a symbol package or any other file, including hidden files and files in subdirectories, is refused. Only the push client's own 409 status message marks a package pending; any other push failure stops the run, and the push output is always logged.
- FR-008: `eng/Test-PublicationTooling.ps1` tests the scripts with synthetic packages and a local test feed on 127.0.0.1 (no network,
  no credentials) and runs in the quality workflow on Linux and Windows.
- FR-006: The account-side policy (owner `monstertacle`, repository `monstertacle.multisession`, workflow `publish.yml`, environment
  `nuget`, package scope covering `Monstertacle.Multisession.*` for new packages and new versions) is documented in
  docs/publishing.md. Another repository's packages (for example Monstertacle.SharpQrs) are not covered.

## Success criteria

- The real producer artifact of run 41333282133 passes the input check.
- `eng/Test-PublicationTooling.ps1` passes (19 cases); mutation probes that remove the pending rule, the signature requirement and the
  unrecorded-file rules turn it red.
- `eng/Test-RepositorySignature.ps1` accepts a real nuget.org-signed package and refuses an unsigned canonical one.
- Required CI passes on the PR head; the workflow and scripts are reviewed by two opposed roles.
- After merge, one manual run publishes 0.2.0-alpha.7 and its receipt shows all eight packages served, equivalent and signed by the
  nuget.org repository certificate subject.

## Out of scope

- Building, versioning or signing packages; the producer run remains the only source of package bytes.
- Publishing other repositories' packages.
