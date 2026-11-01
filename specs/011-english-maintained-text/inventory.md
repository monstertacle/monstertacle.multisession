# Inventory of non-English maintained content (feature 011)

Candidate probe (flags candidates only, proves nothing; constitution principle VI): every tracked file containing a German
umlaut or eszett, or one of the words und, nicht, oder, wird, werden, MUSS, MÜSSEN:
`git ls-files -z | xargs -0 grep -l -I -E '[äöüÄÖÜß]|\b(und|nicht|oder|wird|werden|MUSS|MÜSSEN)\b'`.
Measured on origin/main 8167d37 the probe flagged 144 files; after it, the files below. Every flagged file has a disposition.

## Translated in this feature

- `.specify/memory/constitution.md`
- `AGENTS.md`
- `README.md`
- `docs/cqrs.md`
- `docs/embedding.md`
- `docs/operations.md`
- `docs/providers.md`
- `specs/001-agent-multisession/checklists/multisession.md`
- `specs/001-agent-multisession/checklists/requirements.md`
- `specs/001-agent-multisession/contracts/orchestration-details.md`
- `specs/001-agent-multisession/contracts/provider-contract.md`
- `specs/001-agent-multisession/contracts/public-api.md`
- `specs/001-agent-multisession/data-model.md`
- `specs/001-agent-multisession/plan.md`
- `specs/001-agent-multisession/quickstart.md`
- `specs/001-agent-multisession/research.md`
- `specs/001-agent-multisession/spec.md`
- `specs/001-agent-multisession/tasks.md`
- `specs/002-sharpqrs-cqrs/evidence.md`

## Kept as data or quotation

- `.opencode/skills/codemetr-guard/SKILL.md`: the skill description contains German trigger keywords (Qualitätsprüfung,
  Grenzwerte prüfen, Refactoring absichern) that match German user requests; they are matching data, the skill text is English.
- `examples/Monstertacle.Multisession.ValidationHost/LimitsProbeFlow.cs`: `ä🙂` exercises the UTF-8 input/output limits.
- `tests/Monstertacle.Multisession.DeliveryCheckpoints.Tests/Payloads.cs`: `Grüße` exercises non-ASCII payload bytes.
- `specs/001-agent-multisession/checklists/multisession.md`: verbatim owner approval with its English rendering.
- `specs/008-current-version-only/spec.md`: verbatim owner decision quoted in the origin line.
- `specs/011-english-maintained-text/translation-notes.md`: quotes the German originals of ambiguous passages.
- `specs/011-english-maintained-text/inventory.md` and `spec.md`: name the probe words and the German normative terms (MUSS,
  darf nicht) as quoted examples.

## Archived evidence, preserved byte for byte

English notes: `specs/001-agent-multisession/evidence-language.md`, `specs/003-native-reliability/evidence-language.md`;
`specs/007-mit-package-license/evidence/local-linux-quality.json` is captured tool output of the feature 007 quality run.

- `specs/001-agent-multisession/evidence/README.md`
- `specs/001-agent-multisession/evidence/acceptance-audit.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-28-analysis-remediation.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-28-coverage-87.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-28-fifo-reservations.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-28-linux-quality-file-discovery.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-28-provider-output-boundary.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-28-reserved-dispatch.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-28-session-selection.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-28-sharpclaw-refresh-request-admission.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-admission-certainty.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-before-send-cancellation.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-bootstrap-options.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-cancel-end-evidence.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-canonical-package-consumption.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-case-storage-consistency.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-continuation-availability.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-controlled-stop.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-controlled-timers.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-crash-journal-harness.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-diagnostics.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-dispatch-storage-consistency.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-dispatcher.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-embedded-consumer.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-gemini-adapter.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-host-boundaries.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-host-composition.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-host-scenarios.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-linux-explicit-unlock.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-linux-owner-hardlink-probes.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-linux-ownership.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-linux-process-recovery.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-local-retention.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-native-abi-constants.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-native-context-host.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-native-io-test-isolation.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-native-journal-security.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-native-model-availability.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-native-probe-isolation.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-openai-adapter.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-operation-recovery.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-package-ci.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-package-consumer.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-paging.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-process-diagnostics.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-provider-configuration.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-provider-failure-isolation.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-public-provider-integration.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-publish-host.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-recovery-contract.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-recovery-host.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-recovery-probe-isolation.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-recovery-stop-ownership.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-request-cancellation.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-request-state.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-request-storage-consistency.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-result-storage-consistency.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-routing-process-budget.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-running-cancel.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-runtime-composition.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-sc009-process.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-scenario-dispatch.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-serialization-culture.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-session-close.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-session-metadata.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-session-storage-consistency.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-shared-native-http.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-soak-host.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-soak-receipt-evidence.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-sqlite-admission.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-sqlite-dispatch.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-sqlite-foundation.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-sqlite-pages.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-sqlite-recovery.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-start-dispose-race.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-startup-availability.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-startup-cancellation.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-startup-native-reopen.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-stop-callback-deadline.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-storage-native-faults.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-submission-storage-consistency.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-uncertain-outcomes.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-utf8-integrity.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-validation-host-cli.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-windows-negative-probes.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-windows-ownership.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-windows-process-recovery.md`
- `specs/001-agent-multisession/evidence/architecture/2026-10-29-windows-recovery-diagnosis.md`
- `specs/001-agent-multisession/evidence/bootstrap.md`
- `specs/001-agent-multisession/evidence/build-audit.md`
- `specs/001-agent-multisession/evidence/ci.md`
- `specs/001-agent-multisession/evidence/codemetr-approval.md`
- `specs/001-agent-multisession/evidence/codemetr-results.md`
- `specs/001-agent-multisession/evidence/coverage.md`
- `specs/001-agent-multisession/evidence/delivery-candidate.md`
- `specs/001-agent-multisession/evidence/dependencies.md`
- `specs/001-agent-multisession/evidence/implementation-progress.md`
- `specs/001-agent-multisession/evidence/integrator.md`
- `specs/001-agent-multisession/evidence/linux-mount-proof.md`
- `specs/001-agent-multisession/evidence/live-providers.md`
- `specs/001-agent-multisession/evidence/package-consumer.md`
- `specs/001-agent-multisession/evidence/packages.md`
- `specs/001-agent-multisession/evidence/platforms.md`
- `specs/001-agent-multisession/evidence/recovery.md`
- `specs/001-agent-multisession/evidence/refactoring.md`
- `specs/001-agent-multisession/evidence/release.md`
- `specs/001-agent-multisession/evidence/remaining-acceptance.md`
- `specs/001-agent-multisession/evidence/repository-bootstrap.md`
- `specs/001-agent-multisession/evidence/review.md`
- `specs/001-agent-multisession/evidence/soak.md`
- `specs/001-agent-multisession/evidence/tdd-audit.md`
- `specs/001-agent-multisession/evidence/tdd-foundation.md`
- `specs/001-agent-multisession/evidence/tdd-us1.md`
- `specs/001-agent-multisession/evidence/tdd-us2.md`
- `specs/001-agent-multisession/evidence/tdd-us3.md`
- `specs/001-agent-multisession/evidence/tdd-us4.md`
- `specs/001-agent-multisession/evidence/tdd-us5.md`
- `specs/003-native-reliability/evidence/reviews/change-case.md`
- `specs/003-native-reliability/evidence/reviews/change-delta.md`
- `specs/003-native-reliability/evidence/reviews/status-quo-delta.md`
- `specs/003-native-reliability/evidence/reviews/status-quo.md`

## Completion

No maintained file without a data, quotation or archive disposition remains flagged. Sibling Monstertacle package
repositories and consumers are out of scope (no cross-repository writer); they are reported for a coordinated task.
