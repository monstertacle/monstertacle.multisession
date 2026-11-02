# Manual checks (2026-11-02)

Run locally against the real producer artifact of run 41333282133 (downloaded with `gh run download`), before the automated tests
existed; recorded as manual evidence, not as TDD.

- `eng/Test-ReleaseInput.ps1` on the artifact: PASS, manifest digest 0085125f61a1e49ac283578f555fb01a5bee3732d70898222a07b6180176852f,
  eight packages. Refused: one appended byte in a package, an appended line in the manifest, a record with another version, a record
  with another commit.
- `eng/Publish-CanonicalRelease.ps1 -Mode Plan` against nuget.org: all eight packages absent.
- `eng/Test-PublishedPackage.ps1` with the real Common package: an added `.signature.p7s` entry is equivalent; a changed, added or
  missing entry is a conflict; a canonical package with a signature is refused.
- Plan against a local feed: a served equivalent package is present; a served conflicting package stops the run before a plan is
  written.
- `eng/Test-RepositorySignature.ps1`: the nuget.org-signed Monstertacle.SharpQrs 0.2.0-alpha.1 passes (signer
  `CN=NuGet.org Repository by Microsoft`, certificate SHA-256 1f4b311d9acc115c8dc8018b5a49e00fce6da8e2855f9f014ca6f34570bc482d); the
  unsigned canonical Common package is refused.

Red evidence note: in evidence/red-publication-tooling.txt the two Verify cases "passed" only because the test parameter
`-SkipSignatureVerification` did not exist yet, and the push case failed because the local feed is plain HTTP (refused before the 409
logic). These are not domain Red results. The domain behavior of these cases is shown by the mutation probes
(evidence/mutation-publication-tooling.txt): removing the 409 rule turns the push case red; removing the signature requirement turns
both unsigned cases red; removing both unrecorded-file guards turns the symbol case red (removing only one leaves it green, because the
second guard also refuses it).

Process note: during this feature one empty `python3` invocation (no program, no effect) was started by mistake in a shell command.
It is recorded here because the project rules forbid Python for any project work.

Language check (2026-11-02): `DOTNET_CLI_UI_LANGUAGE=de-DE pwsh -NoProfile -File eng/Test-RepositorySignature.ps1 -Package <signed
Monstertacle.SharpQrs 0.2.0-alpha.1>` returned the repository signature by `CN=NuGet.org Repository by Microsoft` (the script forces
English CLI output for parsing). The round-3 challenger ran the whole tooling suite under the same setting: 19 passed.
