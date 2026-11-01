# TDD

Red (2026-11-01, before any metadata change): the new eng/Test-PackageLayout.ps1 run against a copy of artifacts/packages-alpha3
(Directory.Build.props still at 0.2.0-alpha.3, so identity matched) failed with
`Missing MIT license expression: Monstertacle.Multisession.Common`. The check fails for the missing license, not for a version
mismatch.

Mutation probe (after review P2-b): a copy of the alpha.4 Common package whose LICENSE was replaced by only the title and
copyright line passed the first check (prefix and copyright) by construction and now fails with
`Packaged LICENSE differs from the repository MIT notice`. The check compares the full text with the repository LICENSE after
normalising CRLF to LF.
