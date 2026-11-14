# Implementation plan

**Created**: 2026-11-14T09:57:24Z (measured with `date -u`)
**Owner**: current implementation session for Alexander Bleissem
**Status**: complete (implementation and runtime acceptance)

1. Add semantic failing regression cases for quality suite/runtime selection.
2. Add matching .NET 11 TFMs to every project and native reference/alias conditions;
   pin SDK 11.0.100, retain both runtime sets and regenerate locked graphs.
3. Add RuntimeMajor to quality/package consumers. Every suite is explicitly selected;
   sensitive suites and native host get the windows suffix only on native Windows.
   Derive validation-host fallback from the executing runtime, retaining old behaviour
   for .NET 10. Keep independent run directories and existing coverage inventory.
4. Validate package assets/dependencies per target. Parameterise native, checkpoint
   and delivery consumers without changing published package identities/versions.
5. Extend CI to native OS × runtime, with distinct artifacts and one designated
   producer for canonical consumers. Retain historical publication records and manual
   publishing authority. Update setup, embedding and package guidance.
6. Run complete native Linux checks sequentially for both runtimes, full Codemetr,
   consumers/published hosts and audit; require actual native Windows CI too.

Constitution 2.0.0 and AGENTS reviewed: SDK concretised here, no amendment needed.
Keep SOLID, public/persisted contracts, provider neutrality and at least 87% native
coverage; no baseline, exclusions or numerical limit changes. New verification
routing gets observed Red before Green. Project/TFM changes use existing meaningful
contract suites; the C# host-selector adjustment is confined to test infrastructure.
No open implementation-relevant architecture question remains.
