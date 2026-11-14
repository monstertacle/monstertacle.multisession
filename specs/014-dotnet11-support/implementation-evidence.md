# Additional runtime implementation evidence

**Recorded**: 2026-11-14T10:31:06Z (measured with `date -u`)
**Owner**: current implementation session for Alexander Bleissem
**Status**: local and native hosted acceptance passed; final documentation CI and merge pending.

## Actual local checks

Dedicated SDK installation selected SDK 11.0.100; both 10.0.403 and 11.0.100 SDKs and
runtime sets were available. Capped native Linux runs executed the whole repository
quality entry point separately with RuntimeMajor 10 and 11. All six suites ran on
each target: 1362 tests passed, none failed or skipped. Native product scope was
3894/3984 lines, 97.7410%, against the unchanged 87% floor on each target.
Complete coverage guards passed independently; full Codemetr static guard passed
620 source files/661 types, with zero violations/parse errors/skipped source. The
required changed-file guard also passed. Locks, analyzers, formatting and audit passed.

Exact local quality report directories:

- net10: artifacts/quality/linux/8cee30c25a004e91a55617d1e0ffc2a3/
- net11: artifacts/quality/linux/c857d6f8ea5f440fac5a92ab9068cfa7/

The manifests bind measured source hashes and selected RuntimeMajor. They are
working-tree evidence; final CI must qualify the committed revision. Later consumer
SDK-selection/doc changes do not reuse those manifests as identical-source CI proof.

All nine packages passed actual dual-target asset/group, identity/version, dependency,
MIT licence and source/private-file checks. For RuntimeMajor 10 and 11 separately,
package-only native/session/framework consumers, isolated checkpoint consumer and
isolated delivery consumer passed, including hash-bound locked graphs and reopening.
Both actual published native hosts passed every standard validation scenario. Reports:
artifacts/package-smoke/, artifacts/checkpoints-consumer/, artifacts/delivery-consumer/,
and artifacts/validation-scenarios/{3c181eb728614b73ad8bd517237e9e7c,2e8467ff9dae4dec8167493fe051985f}/.

The initial checkpoint-only consumer exposed SDK11's need for a newer net10 reference
pack from a feed intentionally restricted to the contract. Its own private workspace
now selects installed SDK 10.0.403 for net10 or 11.0.100 for net11, rather than relying
on the caller directory's SDK. The contract still resolves no other application
package. This is an SDK selection correction, not a relaxed dependency boundary.
The final complete consumer run includes it.

## Regression and review

The runtime routing regression observed Red before implementation and now passes
all native suite/runtime combinations. Actual .NET11 callback-entry failures and
subsequent controlled probe/review are bound in evidence/cancellation-fixture-review.md.
Both complete target suites now pass the preserved callback/deadline/lease and
cleanup/primary-failure assertions. RT11-CALLBACK-LIFETIME-001 is resolved for this
runtime qualification by retained registration and those checks; historical Windows
causality and older normal-run acceptance remain explicitly owned by feature008 T006.
No product cancellation implementation or timeout changed.

Public contracts, persistence, native ownership, backend selection and scope were
reviewed. Product source is unchanged; only the validation-host target selector and
two contract-test fixture awaits changed in C#. No baseline, threshold or source
exclusion was introduced. Existing SOLID/dependency boundaries remain; advisory
metrics are not represented as extra numerical gates. Existing published dependencies
remain intentional compatible inputs; the additional target creates no unpublished
package dependency. Package publication and a full one-hour soak were not performed.
The optional soak workflow is configured for both target runtimes, preserving its
manual/source-bound invocation contract. Windows execution requires actual CI.

## Requirement disposition

FR-001/005: all matching targets built; existing public/state/runtime contracts retained.
FR-002: 1362 tests per target and independent native product/complete cover checks passed.
FR-003: nine packages and both sets of actual external consumers/published scenarios passed.
FR-004: locked restore, formatting/analyzers, audit, full/diff metrics passed; native
Windows/Linux matrix and canonical consumers passed hosted qualification on the implementation head.
All local findings were either corrected and checked or explicitly retained with the
existing source-bound task assignment above. No unresolved local acceptance blocker remains.

## Hosted child-process composition correction

The first hosted net11 Linux quality attempt ran the suite itself but failed 64
validation-host child cases, including missing System.Runtime 11.0.0.0. Investigation
of the next CI build showed only net10 host outputs. The local example-host csproj
already had both new targets, but it had inadvertently been omitted from the staged
commit. Consequently clean CI referenced a net10 executable while loading net11 test
dependencies. This was an implementation staging error, not a runtime or SDK defect.
The example-host target and native-reference conditions are now committed. The
unnecessary manual metadata-staging workaround was removed. Existing project-reference
target selection is retained; final exact-head native CI must qualify the correction.

## Hosted acceptance

Updated: 2026-11-14T11:04:46Z (measured with `date -u`).

The complete [PR quality run](https://github.com/monstertacle/monstertacle.multisession/actions/runs/43587241962)
passed on implementation commit `24c0d29c998f3087902c3bd669f0808a1e2789f6`.
All four native Linux/Windows × .NET 10/11 quality jobs passed, including locked
restore, build, formatting, test suites, audit, coverage, package layout, external
consumers and published host scenarios. All four canonical-consumer jobs passed
against the same designated Linux/.NET10 producer package artifact and manifest.
Both native [metrics guards](https://github.com/monstertacle/monstertacle.multisession/actions/runs/43587241939)
also passed. Thus Windows evidence is actual execution, not cross-compilation.
The example host lock graph is committed with both new framework groups.

All tasks T001–T007 are accepted. Final documentation changes receive their own
current-head CI before merge. The explicitly retained historical feature008 T006
assignment remains there; this runtime task does not claim its historical cause
or older repeated-normal-run acceptance has been established. No new unassigned
finding or runtime-acceptance blocker remains.
