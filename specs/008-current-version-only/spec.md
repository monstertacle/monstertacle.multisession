# Feature: Current version only

Status: in_progress
Created: 2026-11-01T11:51:52Z
Owner: integration maintainer
Origin: owner decision 2026-11-01T11:12:53Z "Altversionsfälle -> keine Migration nötig. Get rid of all legacy" (breaking changes
authorized). Historical specifications, ADRs and evidence stay; contradicting statements are amended, not rewritten.

## Requirements

- FR-001: The packages read only the current store format. Stores of any other format, schema or binding encoding are refused
  fail-closed without mutation; nothing is converted. Format 1 is refused like every other unsupported format.
- FR-002: Persisted timestamps are UTC; a stored timestamp with any other offset is refused (already enforced by the snapshot consistency checks; the converter itself is unchanged).
- FR-003: No build, script or CI step produces or exercises an older package or reader (the prior-package probe is removed).
- FR-004: Documentation describes only current use; the alpha.1-to-alpha.2 migration guide is removed.
- FR-005: Wording that labels current behavior as legacy is corrected (continuation availability, reopen capability, CQRS ports).
- FR-006: Package content changes, so the version advances to 0.2.0-alpha.5; alpha.3 and alpha.4 bytes stay unchanged.

## Success criteria

- SC-001: StoreFormatTests refuse user_version 1 and SchemaVersion 1 without mutation; RequestStorageConsistencyTests refuse a non-UTC
  stored timestamp.
- SC-002: Full native Linux/Windows quality, both canonical consumers and codemetr-guard pass for the producer commit.
- SC-003: SharpClaw consumes the canonical alpha.5 bytes.
