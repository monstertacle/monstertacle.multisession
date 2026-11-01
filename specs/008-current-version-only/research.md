# Research (source main 5d4aa2f)

Inventory by a read-only exploration agent, checked by the maintainer for every item acted on, and two contrary architecture reviews
held in the SharpClaw repository (specs/007-current-version-only/evidence/review.md) on the joint inventory.

Removed: eng/Build-PriorPackages.ps1, eng/Test-PriorReaderRefusal.ps1, eng/PriorReaderConsumer.cs.txt and their wiring in
Test-FrameworkConsumer.ps1 and PackageSmoke.ps1 (they built 0.2.0-alpha.2 from commit 6127f2e to show the old reader refusing format2);
docs/agent-framework-migration.md. Changed: StoredUtcTimestampConverter tolerated non-UTC offsets written before format2 (unreachable for
valid stores). Kept as current: SqliteStorePreflight/SqliteSchema exact format checks, MigrationVersion column (part of format2, checked
exactly), reopen capability split, CQRS ports, ContinuationAvailable tri-state (current adapters produce null). Gap closed: no test
showed current code refusing a format1 store once the probe was gone.

Process note: one empty `python3` invocation (no script, no input) was issued by mistake during this work; it executed nothing and
changed no file. No Python was used for this feature otherwise.
