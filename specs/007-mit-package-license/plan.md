# Plan: MIT license for the packages

Status: in_progress
Owner: integration maintainer

## Technical context

Metadata only. Directory.Build.props sets Version, Copyright, PackageLicenseExpression and packs LICENSE beside README.md for every
packable project. eng/Test-PackageLayout.ps1 and eng/PackageSmoke.ps1 read the version from Directory.Build.props instead of
literals, so the next identity change cannot pass against stale strings. packages.lock.json files change only the own project
version range.

## Constitution gate

Analysis in research.md (source dbf629da4a2c76d0b4f77cf23a1fbaf006a72465). No C# change, so codemetr findings cannot change; the
full quality gate (restore locked, format, tests, audit, 87 percent native coverage) still runs on both platforms. No Python.

## Milestones

1. Semantic Red: the license-aware layout check refuses the alpha.3 packages.
2. LICENSE, metadata, version alpha.4, lock files; local Linux quality and package smoke.
3. Contrary review, PR with native CI, canonical alpha.4 feed binding in evidence/alpha4-feed.json, merge.
4. SharpClaw consumes alpha.4 (its own PR) and the prepared cutover bundle is renewed.
