# Feature: MIT license for the Monstertacle.Multisession packages

Status: in_progress
Owner: integration maintainer

## Decision

The owner chose MIT for the Monstertacle packages (owner decision recorded 2026-11-01T09:42:50Z). Until then the packages carried no
license metadata and README.md stated that the license was still undecided.

## User scenarios

US1 (P1): a consumer restoring any of the seven library packages sees the SPDX expression `MIT` and a copyright notice in the
nuspec, and finds the MIT license text with that notice inside the package, as the license requires for every copy.

US2 (P1): already produced 0.2.0-alpha.3 packages stay byte-identical and are never reissued. Packages carrying the license have a
new identity, 0.2.0-alpha.4, built from a clean producer commit by the native CI, and consumers bind the exact new bytes.

## Requirements

- FR-001: Repository root LICENSE with the MIT text and `Copyright (c) 2026 Alexander Bleissem`.
- FR-002: Every library package declares `<license type="expression">MIT</license>`, `<copyright>` and contains LICENSE at its root.
- FR-003: The package layout check derives the expected version from Directory.Build.props and refuses a package without the MIT
  expression, copyright or packaged notice.
- FR-004: Third-party components stay NuGet references with their own licenses; no third-party code is packaged. Files under
  .opencode/skills/codemetr-guard keep their own LICENSE.
- FR-005: No code, store format, dependency, tool pin, coverage scope or threshold changes. Store format stays 2; the prior-reader
  refusal still targets 0.2.0-alpha.2 (format 1).

## Success criteria

- SC-001: The layout check fails on the alpha.3 packages for the missing license (observed Red) and passes on alpha.4.
- SC-002: Full Linux and Windows quality, both canonical consumers and package smoke pass in GitHub CI for the producer commit.
- SC-003: SharpClaw consumes the canonical alpha.4 bytes with locked restore and passes its runtime suites.
