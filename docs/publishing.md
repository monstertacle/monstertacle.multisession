# Publishing to nuget.org

Releases are published by the manual workflow `.github/workflows/publish.yml` through nuget.org trusted publishing: the job
exchanges a short-lived GitHub OIDC token for an API key that is valid for one hour. No long-lived API key is stored.

## Account side (once)

On nuget.org, under the publishing account's **Trusted Publishing** page, add a policy:

| Field | Value |
| --- | --- |
| Repository owner | `monstertacle` |
| Repository | `monstertacle.multisession` |
| Workflow file | `publish.yml` |
| Environment | `nuget` |
| Package scope | `Monstertacle.Multisession.*`, allowed to push new packages and new versions (a root package `Monstertacle.Multisession` would need its own pattern; the current releases have none) |

On GitHub, the environment `nuget` is restricted to the `main` branch. The repository secret `NUGET_USER` holds the nuget.org profile
name (not an e-mail address). Requiring a reviewer on the environment is an optional additional control.

The policy covers this repository's packages only. Other repositories, for example the one producing `Monstertacle.SharpQrs`, need
their own policy and workflow.

## Release procedure

1. The packages come from one successful `push` run of `.github/workflows/ci.yml` on `main`. Record that run in
   `eng/release-records/<version>.json`: producer commit and run, artifact name, producer manifest digest and each package's id, file and
   SHA-256. Merge the record through a normal pull request.
2. Start **publish** from `main` with the version. The workflow verifies the run and the artifact against the record (only the recorded
   packages may be in it; no symbol packages), then classifies each package on nuget.org. Packages that are already served with the same
   content are left alone; a served package with other content stops the run. A version is never reissued with other bytes.
3. Only if a package is absent, the job logs in and pushes it without symbols. It then waits until every package is served and compares
   the served package with the canonical one. The canonical packages are unsigned; nuget.org adds its repository primary signature as
   the entry `.signature.p7s`, which changes the archive digest but no other entry. The workflow requires that entry, verifies it with
   `dotnet nuget verify --all` and requires the nuget.org repository certificate; any other difference fails the run. The run summary
   holds the receipt with the served digest, the signer and its certificate fingerprint.

A failed run can be started again. Packages that nuget.org already serves are recognised as present. A package that nuget.org has
accepted but not yet served answers the push with 409; it is recorded as pending, not skipped, and the run still requires it to be
served with the canonical content and signature.

The scripts are tested by `eng/Test-PublicationTooling.ps1` (synthetic packages and a local test feed) in the quality workflow.
