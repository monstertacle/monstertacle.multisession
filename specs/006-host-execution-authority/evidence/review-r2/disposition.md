# Contrary patch review r2: disposition

Frozen patch sha256 d3b51d85bcc44e46c26262d0d6cee172216102112d34319046b9e28c3ff7fdf3 (base 0597d9e). Two independent read-only roles with identical evidence: change advocate about 4.5 minutes and 30 tool calls; status-quo defender about 3 minutes and 22 tool calls (both self-reported). Root verified each accepted claim at the cited source before changing it.

Accepted and fixed:
- Change P1-1: typed NotStarted after acknowledged operation/native session became not-invoked. Semantic Red (NotStartedAfterExternalEvidenceIsUnknown, two cases) then fix: such facts become Unknown.
- Change P1-2: the canonical consumer CI job used a shallow checkout and could not archive the prior source commit. fetch-depth 0 added to that job and the soak job.
- Change P2-2: prior packages are now packed inside the extracted prior tree (its own global.json); wording no longer claims published bytes.
- Change P2-3: one OperationAccepted per framework invocation and a 512-character identity bound (Red in OperationIdentityIsSingleAndBounded). Store-level last-writer semantics stay for the other adapters, unchanged.
- Change P2-1 (partly): the probe text no longer claims provider construction; store open is the first composition step, so refusal there precedes any runtime/provider use. Store manifest comparison is now recursive.
- Status-quo P3-2: cancellation projection fails closed instead of falling back to the unprojected snapshot.
- Status-quo P3-4: observation-bound validation message corrected.
- Status-quo P2-2/P2-3 and change P3 store contract: documented in contracts/public.md.

Retained with reason:
- Change P2-4 / distinct block reason: valid but adds public enum surface that breaks exhaustive host switches; recorded as T009 with acceptance criteria.
- Change P3 typed Unknown with output rejected wholesale: consistent with the existing Unknown invariant (no output); category loss accepted.
- Change P3 termination race erasing output: existing "termination wins" contract; ADR wording limited to the ordinary completion path.
- Change P3 real ChatClientAgent transmission of AdditionalProperties: recorded in T009 as a consumer test; FrameworkInvocation is never serialized by this SDK.
- Status-quo P2-1 (operation-aware reopen without explicit unavailability): branch unchanged for null/true continuation; existing Interactions recovery contract tests in the full suite cover it.
- Status-quo P3-3 (one type combines API and gate): accepted as a single responsibility, the invocation's observation custody.
