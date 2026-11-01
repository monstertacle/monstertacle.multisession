# Tasks

- [x] T001 Analyse current packaging, license files, consumers and literals (research.md).
- [x] T002 Semantic Red: license-aware eng/Test-PackageLayout.ps1 refuses the alpha.3 packages (evidence/tdd.md).
- [x] T003 LICENSE, Directory.Build.props metadata, version 0.2.0-alpha.4, version from source in package scripts, lock files,
  README; local Linux quality and package smoke.
- [x] T004 Contrary review of the patch; PR with native Linux/Windows CI; canonical alpha.4 bytes in evidence/alpha4-feed.json.
- [x] T005 SharpClaw consumes alpha.4 with locked restore and passes its runtime suites (recorded in SharpClaw).

T004 (2026-11-01): contrary review dispositions in evidence/review.md; GitHub run 41295442582 for a570e6c passed Linux/Windows
quality (97.9511/97.8759 percent), both canonical consumers with actual prior-reader refusal, and codemetr-guard 41295442583.
Canonical alpha.4 bytes bound in evidence/alpha4-feed.json. T005 follows in SharpClaw.

T005 (2026-11-01): SharpClaw PR 51 (merged as 8e6c35b) consumes the canonical alpha.4 bytes through its repo-local feed with
locked restore; consumed assemblies are identical to alpha.3 apart from the copyright attribute and informational version; its
established regression selection passed on the new packages. Recorded in SharpClaw
specs/005-agent-framework-runtime/evidence/alpha4-consumption.json.
