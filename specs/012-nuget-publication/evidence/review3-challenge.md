# Round 3 challenge: fix diff 64a982d..f04582c (SDK PR 15)

Role: challenger (argue against merge and run where evidence supports it).
Object: `f04582c9f4cf232cd727b29f6838eefb37c92825`, diff `64a982d..f04582c`, round-2 disposition table in
`specs/012-nuget-publication/tasks.md`. Repo read only; `git status --short` empty after the runs.
Frozen packet: `sha256sum -c ../frozen.sha256` OK for `challenge.md`, `defend.md`, `fix.diff`. `fix.diff` is byte-identical to
`git diff 64a982d..f04582c` (`diff -q` silent). The repo copy of `review2-defend.md` differs from the frozen copy in one line only
(line 143: the work-dir path replaced by `<private staging directory>`); `review2-challenge.md` is identical.

## Verdict

**No blocking finding (no P0/P1/P2). Merge and the alpha.7 run are acceptable from the challenger side once T007 (required CI on the
PR head, including Windows) is green.** P3 items below; none needs a further fix round.

## Evidence (measured in this round)

| # | What | Result |
| --- | --- | --- |
| E1 | `DOTNET_CLI_UI_LANGUAGE=de-DE pwsh -NoProfile -File eng/Test-PublicationTooling.ps1 -WorkRoot .../challenge-work/tooling` (the one permitted run, deliberately under a German UI language) | exit 0, `"Passed": 19`, `"Failed": []` |
| E2 | `DOTNET_CLI_UI_LANGUAGE=de-DE dotnet nuget push --help` (SDK `dotnet --version` = 10.0.403, matches `global.json` with `rollForward: disable`) | German text ("Überträgt ein Paket mithilfe von Push auf den Server ...") — the variable is effective for NuGet's own strings |
| E3 | `pwsh -NoProfile -File eng/Test-ReleaseInput.ps1 -Release eng/release-records/0.2.0-alpha.7.json -ArtifactDirectory .../producer-download` (the one permitted run) | exit 0, `Status: PASS`, 8 package entries (counted with `grep -c '"Id"'`) |
| E4 | `find .../producer-download/packages -mindepth 1 \| wc -l` and `ls -la` | 8 entries, all top-level `*.0.2.0-alpha.7.nupkg`, no hidden file, no subdirectory |
| E5 | `.github/workflows/publish.yml:23` | `runs-on: ubuntu-24.04`; push step lines 87-92 set only `NUGET_API_KEY`, no UI language |

## Focus questions

1. **Does real `dotnet nuget push` print `Response status code does not indicate success: 409 (` on a 409, without forced English?**
   Yes, as far as it can be shown locally. The positive case "push: a 409 ... is pending and the rest are pushed" runs the real
   SDK 10.0.403 client against the 127.0.0.1 feed and passed under `de-DE` (E1), while E2 shows NuGet's own strings really were German
   in that configuration. The matched sentence is the .NET runtime's `HttpRequestException` text from `EnsureSuccessStatusCode`
   (`... {code} ({reason}).`); .NET (Core) ships no localized runtime exception messages, so the runner locale does not change it.
   With HTTP/2 (no reason phrase on the wire) the runtime substitutes the default phrase, so `409 (Conflict...` still matches; on
   nuget.org the usual form is `409 (Conflict - The feed already contains ...)`. *Inference, not measured against nuget.org.*
2. **Text change by locale or future SDK -> fail closed?** Confirmed by code: `eng/Publish-CanonicalRelease.ps1:77-78` — nonzero exit
   without the match throws `Push failed`. The SDK is pinned (`rollForward: disable`), so a text change needs a deliberate SDK bump.
3. **Does a failing push still stop the remaining pushes, and is that acceptable for the rerun story?** Yes, the `throw` inside the
   `foreach` (line 78) ends the step; `push-result.json` is not written and Verify does not run. Acceptable: a rerun re-plans, packages
   already served become `present`, accepted-but-unserved ones get 409 -> `pending`, the rest are pushed. Nothing is lost, only time.
4. **Recursive input check vs. the real artifact:** it refuses nothing legitimate (E3, E4).
5. **Write-Host / secret:** output goes to the information stream (visible in the Actions log, not mixed into the JSON on stdout).
   The argument array holding the key is never printed; the NuGet client sends the key as `X-NuGet-ApiKey` header and does not echo it;
   in Actions the login action's output is masked as well. *Inference from client behavior; the test suite suppresses the output
   (`Invoke-Case ... *> $null`), so I did not see the text of a run.*
6. **Genuineness of new tests:** the 403 case is genuine — red2 evidence shows the old loose match turned it green-as-success, which
   is only possible if the real client prints the reason phrase containing `409`. The Verify-success case asserts PASS and one
   `EQUIVALENT`+`RepositorySigned` receipt per id. Limits listed under PC3-F2/F3.

## Findings

| Id | Sev | Location | Scenario | Fix |
| --- | --- | --- | --- | --- |
| PC3-F1 | P3 | `eng/Publish-CanonicalRelease.ps1:77` | nuget.org also answers 409 for a version that was deleted and cannot be reused. That is marked `pending`, and Verify then waits the full 60 minutes before failing. Fail closed, only slow and with a misleading state name. | Optional: log the matched line in the result, or accept. |
| PC3-F2 | P3 | `eng/Test-PublicationTooling.ps1:194-197` (403 case) and `Invoke-Case` line 67 | Refusal cases pass on *any* throw; the 403 case does not assert that the failure is the 403 push (`Push failed`) rather than, e.g., a stale plan or missing key. Red2 shows it currently discriminates, but a later unrelated breakage would keep it green. | Assert the exception message contains `Push failed` (pattern exists in the other cases' style). |
| PC3-F3 | P3 | `eng/Test-PublicationTooling.ps1:98-100`, `eng/Test-ReleaseInput.ps1:23` | One combined case (`nested/.extra.snupkg`). Removing the `DirectoryName` check would stay green (the unrecorded-name rule still refuses); a top-level hidden file is not tested on its own. Behavior still refuses both, so this is test granularity, not a hole. FR-007 wording is covered. | Optional: a nested file with a recorded name (count then also catches it) and a top-level dot file. |
| PC3-F4 | P3 | `eng/Test-RepositorySignature.ps1:9` | Sets `$env:DOTNET_CLI_UI_LANGUAGE` process-wide when invoked with `&`; leaks into later `dotnet` calls in the same pwsh process. Harmless today (Verify is the last step; tests skip the script). | Scope it (save and restore) or accept. |
| PC3-F5 | P3 | `specs/012-nuget-publication/evidence/review2-defend.md:143` | Repo copy differs from the reviewed frozen copy (path redaction). Content-neutral, but the evidence file is not byte-identical to what was reviewed. | Note the redaction in the file or accept. |
| PC3-F6 | P3 | `tasks.md` round-2 table, row "Windows not shown" | Disposition is a promise on CI that has not run yet; it is correctly gated by T007 (unchecked). | None beyond T007. |

## Weaknesses of my position

- I argued against merging and found nothing blocking; the strongest residual (PC3-F1) is fail-closed and only slow.
- The nuget.org 409 text is inferred from the .NET runtime message and the local feed, not observed against nuget.org itself.
- Locale independence rests on one de-DE run of the suite plus the `--help` probe; I did not see the German push transcript because
  the suite discards push output.
- "Key not echoed" is inferred from client behavior, not from a captured transcript of a real push.
- I could run the suite only once and chose de-DE; an English run of the same head is the author's green evidence, not mine.

## Duration and tool calls

Start `2026-11-02T02:07:09Z`, end `2026-11-02T02:08:46Z` (both `date -u`; the end stamp was taken before writing this file).
Tool calls: 10 (9 Bash, 1 Write).
