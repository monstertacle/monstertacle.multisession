# Contrary review of 6ac791d (patch dbf629d..6ac791d, sha256 942a6bce0fe703ab27f2218ad291fa779e9577a5bd8afd69cc0cceb0f5aeb1f5)

Two independent read-only reviewers with the same frozen patch: one advocating further change, one defending the status quo.
Both: no P1. Durations 90 s / 9 tool calls (change) and 67 s / 6 tool calls (status quo).

| Finding | Raised by | Disposition |
| --- | --- | --- |
| P2 packaged README states SharpQrs 0.2.0-alpha.2; actual pin and nuspec are 0.2.0-alpha.1. Would be frozen into alpha.4. | both (status quo as P3) | Fixed: README.md line 41. |
| P2 LICENSE check only tested prefix and copyright; a notice without permission text or disclaimer passed. | change; status quo as P3 | Fixed: full-text comparison with the repository LICENSE, CRLF normalised; mutation probe red (tdd.md). |
| P3 copyright literal in the guard instead of reading Directory.Build.props | change | Kept deliberately as an independent second source (status quo argued the same). |
| P3 no guard against reissuing alpha.3 | change | Rejected: alpha.3 bytes are bound by hash in specs/006 evidence; a new mechanism is out of scope (change reviewer agreed it is scope creep). |
| P3 version read from props removes the literal second key | status quo | Kept: lock files and canonical hash binding remain second keys; both reviewers judged it sound. |
| P3 research gaps (transitive licenses, explicit test plan line) | both | research.md amended. |
| P3 recordedUtc 09:50:00Z looks rounded | both | It is the measured `date -u` output of that step (commit at 09:50:03Z); unchanged. |
| P2 process: merge only after native CI and canonical binding | status quo | Followed: canonical bytes come from the CI run of the fixed commit (alpha4-feed.json). |

The CI run 41294827535 on 6ac791d passed, but its package bytes are superseded by the review fixes and are not bound.
