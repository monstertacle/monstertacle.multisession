# Research: current packaging (source dbf629da4a2c76d0b4f77cf23a1fbaf006a72465)

- Directory.Build.props: Version 0.2.0-alpha.3, Authors, Description, Repository*, PackageReadmeFile; no license, no copyright.
  README.md packed to the package root for all packable projects.
- Packable projects: the seven src libraries; tests and ValidationHost are not packed. Their own runtime references are
  Microsoft.Agents.AI(.Workflows) 1.27.0 (MIT), Microsoft.Data.Sqlite and Microsoft.Extensions.Logging.Abstractions 10.0.14 (MIT),
  Monstertacle.SharpQrs 0.2.0-alpha.1 (MIT, specs/002 analysis). All are NuGet references; no third-party source in src (no
  foreign copyright or license headers found by `git grep -i "copyright|licensed under|SPDX" -- src`).
- The only license file in the repository is .opencode/skills/codemetr-guard/LICENSE (agent skill, not packaged); it is kept.
- Alpha.3 nuspec (artifacts/packages-alpha3, Common): no license, no copyright, README.md only. Canonical alpha.3 bytes are bound in
  specs/006-host-execution-authority/evidence/alpha3-feed.json and must stay unchanged; a licensed package therefore needs a new
  version.
- Version literals: eng/Test-PackageLayout.ps1 (4), eng/PackageSmoke.ps1 (6); Build-PriorPackages/Test-PriorReaderRefusal refer to
  alpha.2 (format 1) and stay valid because the store format does not change.
- Consumers: SharpClaw (Directory.Packages.props and locked restore through its local feed) is the only consumer.

Decision: SPDX expression rather than a license file reference (NuGet shows it directly), with LICENSE packed as an additional file
so every copy carries the notice. Open questions: none.

Transitive packages keep their own licenses (for example SQLitePCLRaw, Apache-2.0); the README makes no all-MIT claim, it states
that dependencies are NuGet references with their own licenses. Planned tests: license-aware layout check (Red on alpha.3), full
native quality, package smoke with prior-reader refusal and both canonical consumers in CI, SharpClaw locked consumption.
