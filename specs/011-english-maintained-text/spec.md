# Feature: English for all maintained project content

Status: in_progress
Created: 2026-11-01T21:00:25Z
Owner: implementation agent (SDK writer)
Origin: owner request "Make English mandatory everywhere in the Monstertacle constitution, including documentation"
(recorded 2026-11-01T20:43:46Z).

## Problem

The constitution, the agent instructions, the repository README, four maintained documents and the core documents of feature 001
are German, while the rest of the repository is English. The owner requires English for all maintained project content, including
the constitution itself, and no grandfather clause for existing documents.

## Requirements

- FR-001: The constitution is English, keeps every other binding rule unchanged in force, and adds principle VI: all maintained
  project content is English; literal test data, verbatim quotations, proper names and identifiers fixed by external contracts are
  data, not documentation; archived hashed evidence, release artifacts and commit history are not rewritten but get an English
  explanation; enforcement is review, automated probes only flag candidates. Version 2.1.0 with an amendment record.
- FR-002: Every maintained non-English file is listed in `inventory.md` with a disposition: translated in this feature, kept as
  data or quotation (with the reason), or archived evidence with an English note beside the archive.
- FR-003: Translations are faithful: no requirement, threshold, scope, identifier, number or link target changes meaning. Normative
  force is kept (MUSS → MUST, darf nicht → MUST NOT).
- FR-004: No package bytes change: the packed package README is `docs/package-readme.md`, which is already English; no version
  advance and no new package identity.

## Out of scope

Other repositories that consume Monstertacle (no cross-repo writer); sibling Monstertacle repositories (reported for a coordinated
task); rewriting history or hashed evidence.

## Success criteria

- `inventory.md` accounts for every tracked file that the candidate probe flags; each flagged file has a disposition.
- Required CI (quality Linux/Windows, Codemetr, canonical consumers) passes on the PR head; no package content changes.
- A narrow opposed review of the constitution translation finds no change of normative meaning.
