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
| Package scope | `Monstertacle.Multisession.*`, allowed to push new packages and new versions |

On GitHub, the environment `nuget` is restricted to the `main` branch and holds the secret `NUGET_USER` with the nuget.org profile
name (not an e-mail address). A policy for a private repository stays temporary until the first successful publication.

The policy covers this repository's packages only. Other repositories, for example the one producing `Monstertacle.SharpQrs`, need
their own policy and workflow.

## Release procedure

1. The packages come from one successful `push` run of `.github/workflows/ci.yml` on `main`. Record that run in
   `eng/release-records/<version>.json`: producer commit and run, artifact name, producer manifest digest and each package's id, file and
   SHA-256. Merge the record through a normal pull request.
2. Start **publish** from `main` with the version. The workflow verifies the run and the artifact bytes against the record, then
   classifies each package on nuget.org. Packages that are already served with the same content are left alone; a served package with
   other content stops the run. A version is never reissued with other bytes.
3. Only if a package is absent, the job logs in and pushes it. It then waits until every package is served and compares the served
   package with the canonical one. nuget.org adds a repository countersignature (`.signature.p7s`), which changes the archive digest
   but no other entry; any other difference fails the run. The run summary holds the receipt.

A failed run can be started again: published packages are recognised as present and only the remaining ones are pushed.
