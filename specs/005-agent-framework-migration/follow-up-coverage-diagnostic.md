# Coverage diagnostic reconciliation

Status: ready
Owner: primary integration agent
Origin: full native Codemetr Cover1.1 reports complete/passed while warning that one file has no coverage data; summary unreported count is0. Product and host denominators remain above87 percent. No gate was weakened.

Bounded scope: determine whether this is an expected foreign-platform/generated-code exclusion diagnostic or an actionable missed native source file. Do not undertake a general coverage rewrite or lower/narrow coverage policy.

Next steps:
1. Inspect the final native report's warnings, exclusions, unreported entries and per-member reported flags against the source inventory and collector reports.
2. Reproduce the diagnostic with the same pinned tool and source inventory, recording the exact file responsible.
3. If native own code is missing, add a meaningful behavior test and rerun the complete gate. If the warning is emitted before approved foreign-platform filtering, document a minimal tool-diagnostic reproduction; preserve the current policy and report the exact boundary.

Dependencies: final native Linux/Windows quality artifacts and pinned Codemetr Cover0.1.1; do not substitute old reports as new measurements.

Acceptance: every warning in this class is associated with an exact file and approved scope or a passing new behavioral test; complete remains true; no silent exclusion or threshold change. This nonblocking diagnostic does not replace any unmet migration acceptance. Existing soak/native-capacity work remains in its original concrete plans.
