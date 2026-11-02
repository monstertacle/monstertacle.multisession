# Tasks

- [x] T001 Release record eng/release-records/0.2.0-alpha.7.json from the canonical producer run 41333282133 (commit 8167d37).
- [x] T002 eng/Test-ReleaseInput.ps1, eng/Test-PublishedPackage.ps1, eng/Publish-CanonicalRelease.ps1 (Plan, Push, Verify).
- [x] T003 .github/workflows/publish.yml (manual, main, environment nuget, pinned actions, OIDC login only when a push is needed).
- [x] T004 docs/publishing.md: account policy fields and the release procedure.
- [x] T005 Local checks against the real producer artifact, including refusal cases and a local test feed for the plan.
- [ ] T006 Two opposed reviews of the workflow and scripts; dispositions below.
- [ ] T007 Required CI on the PR head; merge.
- [ ] T008 Manual run for 0.2.0-alpha.7; record the run and the served-package receipt.
