# Implementation evidence: feature 013

Base: `main` at `a44cb4f9cb00476d1abd51a6a43a34bde10913d8`. SDK 10.0.403, PowerShell 7, native Linux. Heavy runs used a
memory-capped scope. Contract decision recorded 2026-11-02T20:51:58Z (spec Clarifications). The commands below ran from the
repository root.

## Pinning tests (T001)

`feature/015-continuity-pinning` at `6df17074056b033fff36166c8a4bdcd809fe8cbe` was merged normally; its two commits keep their
history. `PartialEvidencePinningTests` runs in ContractTests (257 passed in the quality run).

## Move (T002)

- Before the move: `dotnet test tests/Monstertacle.Multisession.DeliveryCheckpoints.Tests -c Release`, 94 passed.
- After the move to `Monstertacle.Multisession.Checkpoints.Delivery` (git renames, 54 files): 94 passed.
- The staged diff, excluding `namespace`/`using` lines, contains only the solution paths, `InternalsVisibleTo`, the test
  `ProjectReference` and the lock-file project key.
- No assertion and no fixture changed.

## Red → Green

| Unit | Test | Red observed | Green |
| --- | --- | --- | --- |
| Suite inventory (T004/T005) | `pwsh -File tests/quality/Test-QualitySuites.Tests.ps1` | With a stub returning the previous literal list: `Unexpected suites: UnitTests,ContractTests,IntegrationTests,PlatformTests,DeliveryCheckpoints.Tests` | `Get-QualitySuites` derives the suites from the solution; five cases pass, three of them refusals |
| Delivery port (T010/T011) | `dotnet test tests/Monstertacle.Multisession.Checkpoints.Delivery.Tests` | 5 of 99 failed: `DeliveryCheckpoint does not implement IDeliveryCheckpoint.` / `Assert.True() Failure` | `DeliveryCheckpoint : IDeliveryCheckpoint`: 99 passed |
| Package layout of a dependency-free package | `eng/PackageSmoke.ps1` | `The property 'dependency' cannot be found on this object` on the contract package | Dependency elements enumerated by name; a direction rule was added; all nine packages pass |
| Coverage instrumentation | `scripts/Invoke-Quality.ps1 -Platform linux` | Gate refused: `Missing product assembly: Monstertacle.Multisession.Checkpoints` (runsettings listed only the old assembly) | Both assemblies included; gate passed |

The base contract is a behaviourless interface, so it has no behavioural Red. Its tests show contract usability: a typed
double, cancellation and single release. The boundary tests prove their sensitivity through the mutations below.

## Named mutations (T014): each was observed red, then restored

| Mutation | Detected by | Observation |
| --- | --- | --- |
| M1: base gains a public type using `Microsoft.Data.Sqlite` | BaseAssemblyBoundaryTests | 2 of 5 failed (references, exports) |
| M2: base gains `DeliveryPhase` | BaseAssemblyBoundaryTests | 1 of 5 failed (exports) |
| M3: `DeliveryCheckpoint` drops `IDeliveryCheckpoint` | delivery tests compile | CS1503 ×3, CS0411 ×2 |
| M4: the host-shaped loop omits `AcceptAsync` | DeliveryPortTests | 1 of 99 failed (accepted cycle) |
| M5: the delivery package is missing from the package set | `eng/Test-PackageLayout.ps1` | `Expected exactly 9 library packages.` |
| M6: the delivery consumer imports the old namespace | `eng/Test-DeliveryConsumer.ps1` | CS0234 `DeliveryCheckpoints` does not exist |
| Earlier, unintended: an instrumented assembly not in the runsettings | coverage gate | `Missing product assembly` (above) |

During instrumentation, the base assembly references `netstandard`. The boundary test allows that BCL facade explicitly; M1
remains detected.

## Local gates (final working state before commit)

- `scripts/Invoke-Quality.ps1 -Platform linux`: exit 0.
  - Suites: UnitTests 442, ContractTests 257, IntegrationTests 432, PlatformTests 68, Checkpoints.Tests 5,
    Checkpoints.Delivery.Tests 99.
  - Product coverage 97.71 % (3888/3979). Both checkpoint assemblies are in the module list.
  - Codemetr Cover is complete and passed, with 0 violations.
  - Cover printed a generic warning, "1 file(s) have no coverage data", while its `unreported` list was empty and its
    summary showed `unreported: 0`. It is recorded here, not resolved.
- `dotnet tool run codemetr-csharp -- check --format json`: exit 0; 591 files, 615 types, 0 violations, 0 skipped.
- Pack, then `eng/PackageSmoke.ps1`: nine packages pass layout; the framework and package consumers pass.
- `eng/Test-DeliveryConsumer.ps1`: PASS. The base package is bound transitively with its exact content hash.
- `eng/Test-CheckpointsConsumer.ps1`: PASS. The lock resolves only the contract package.

## Not executed locally

- Native Windows quality and consumers: they run in hosted CI on the pull request.
- Publication, the release record and SharpClaw consumption: out of scope for this change.
