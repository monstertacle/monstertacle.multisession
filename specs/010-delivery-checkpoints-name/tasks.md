# Tasks

- [x] T001 Naming decision by two opposed roles (both recommend DeliveryCheckpoints, no type renames, drop the outbox tag); decision
  record in docs/adr/0008-delivery-checkpoints-package-name.md.
- [x] T002 Rename project, assembly, namespace, test project and all source references; package tags; version 0.2.0-alpha.7; locks.
- [x] T003 Consumer package README docs/package-readme.md packed as README.md; layout guard binds it (mutation: repository README packed
  -> guard red).
- [ ] T004 Required CI on the PR head; merge; canonical alpha.7 package set and provenance from the merged main head.
- [ ] T005 Host consumption of the alpha.7 set in SharpClaw (lock, provenance, usings) under its gates.

Local evidence (2026-11-01, Linux): Release build 0 errors; pack of 8 alpha.7 packages; eng/Test-PackageLayout.ps1 PASS for all 8;
DeliveryCheckpoints.Tests 94 passed; eng/Test-DeliveryConsumer.ps1 PASS against the alpha.7 package; mutant packing the repository
README fails the layout guard ("Packaged README is not docs/package-readme.md").
