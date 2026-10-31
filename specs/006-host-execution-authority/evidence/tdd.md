# Observed behavior tests

Baseline: `6127f2ec6bfcb279c6204c1de6e5e81b4a99bb3e` plus behaviorless signatures. Compile/analyzer fixes preceded the semantic Red and are not counted as behavior evidence.

Command prefix: `bash /home/user1/dev/scripts/run-capped.sh -m 6G -s 1G -- dotnet test tests/Monstertacle.Multisession.IntegrationTests -c Release -f net10.0`.

| Filter | Recorded semantic Red | Implementation |
| --- | --- | --- |
| `FullyQualifiedName~HostAuthorityTests` | Four failures: supplied session ID replaced, mode change accepted, queued input invoked automatically, explicit target not dispatched. | Canonical IDs; persisted mode; mode-dependent lifecycle; expected head checked inside reservation transaction. |
| `FullyQualifiedName~RequestIdentityIsExact\|FullyQualifiedName~EmptySessionIdentity` | Supplied request ID replaced; empty session ID opened an agent. | Request ID carried through commands/admission; empty ID refused before open. |
| `FullyQualifiedName~UsesCallerIdsAndRejectsDuplicateBeforeAnotherOpen` | Duplicate session reported StorageUnavailable instead of InvalidInput. | Transactional duplicate refusal before insert/open. |

The earlier complete integration run reached 417 passing tests and one failure because the historical future-format fixture still wrote version2, which is now current. That fixture now writes version3. It was a test expectation adjustment, not a relaxed format guard. Final results belong to the full quality run and must be recorded only after its completion.

Green: the final full Linux quality run passed all1140tests, including all six HostAuthorityTests. See qualification.json and linux-run.json for exact source binding and limits. The dispatcher cancellation read was moved to the existing cancellation observer after green; the full run was repeated successfully afterward, with the complete static guard passing and without policy changes.
