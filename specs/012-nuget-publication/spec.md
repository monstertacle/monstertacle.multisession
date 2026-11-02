# Feature: Publish canonical releases to nuget.org through trusted publishing

Status: in_progress
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
  before the push; the key is passed in the step environment only. No long-lived key exists. Pushes do not use `--skip-duplicate`.
- FR-005: After the push the workflow waits until every package is served and compares the served package with the canonical one
  (`eng/Test-PublishedPackage.ps1`): every entry except the repository signature `.signature.p7s` must have the same name and content.
  The archive digest is expected to change when nuget.org adds its repository countersignature; that is not a content change.
  The receipt (served digest, signed flag, compared entries) is written to the run summary.
- FR-006: The account-side policy (owner `monstertacle`, repository `monstertacle.multisession`, workflow `publish.yml`, environment
  `nuget`, package scope covering `Monstertacle.Multisession.*` for new packages and new versions) is documented in
  docs/publishing.md. Another repository's packages (for example Monstertacle.SharpQrs) are not covered.

## Success criteria

- Local checks against the real producer artifact of run 41333282133: the input check passes; a changed package byte, manifest,
  version or commit is refused; the comparison accepts an added signature entry and refuses a changed, added or missing entry; the
  plan classifies an equivalent present package as present and stops on a conflicting one (local test feed).
- Required CI passes on the PR head; the workflow file is reviewed by two opposed roles.
- After merge, one manual run publishes 0.2.0-alpha.7 and its receipt shows all eight packages served and equivalent.

## Out of scope

- Building, versioning or signing packages; the producer run remains the only source of package bytes.
- Publishing other repositories' packages.
