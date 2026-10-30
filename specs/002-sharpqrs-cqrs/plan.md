# Implementation Plan
Created: 2026-10-30T01:21:40Z
Feature: 002-sharpqrs-cqrs
Status: completed
Updated: 2026-10-30T01:55:50Z

.NET 10 / SDK 10.0.403; Linux Ubuntu 24.04 x64 and native Windows CI, existing Windows 11 release requirement unchanged. Existing six libraries and SQLite remain; add Monstertacle.SharpQrs 0.2.0-alpha.1 to Common, Codemetr Cover 0.1.1 as local developer tool.
The six unpublished library packages use 0.2.0-alpha.1 as well; stable package validation rejected the prerelease dependency. This is package metadata only, not publication.
Read analysis.md for source evidence, alternatives and risk assessment.

## Constitution check
Host-neutral async integration; backend/store contracts reused; no schema migration, retry, transaction or business policy in dispatch. Test-first new behavior; existing tests before/after compatibility routing. Pin dependencies, audit transitive packages. Full native product coverage >=87%, static metrics unchanged, Linux/Windows checks required. Historical feature001 evidence is not evidence for new source.

## Structure and sequence
Common/Cqrs: public command/query records, result envelopes, domain handlers, execution-only dispatch composition and legacy translating facades.
RuntimePreparation/RuntimeComponents/SessionRuntime: compose and expose routes.
ContractTests: dispatch contracts and lifecycle integration.
eng + package fixture: transitive dependency source mapping and actual CQRS consumption.
scripts + codemetr-cover.json: full coverage gate, provenance and source hashing.
README + quickstart: integration and measurement instructions.

## Validation
Diff coverage uses the fixed feature base 4494f9a0749c76975418682113f19f7839340f54, not only uncommitted lines. CI fetches history so the gate evaluates the full extension after commits.
Behavioral Red archived before Green; targeted contracts, entire native quality suite, static Codemetr, package smoke, locked restore, dependency audit, native Linux/Windows CI. No cost-incurring live calls, release publication or merge authorized by this feature.

