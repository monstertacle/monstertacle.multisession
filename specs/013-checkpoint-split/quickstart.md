# Quickstart: validating the checkpoint split

Prerequisites: .NET SDK 10.0.403, PowerShell 7, network access to nuget.org; no Python.

1. `dotnet restore Monstertacle.Multisession.slnx --locked-mode`, then build Release.
2. Run both checkpoint test suites (`tests/Monstertacle.Multisession.Checkpoints.Tests`,
   `tests/Monstertacle.Multisession.Checkpoints.Delivery.Tests`). Expected result: all moved delivery tests and the new
   contract tests pass.
3. Run `pwsh -NoProfile -File scripts/Invoke-Quality.ps1 -Platform linux` natively. On Windows, use `-Platform windows`.
   Expected result: product coverage is at least 87 %, both assemblies are in scope, and every solution test project is a
   quality suite.
4. Pack, then run `eng/Test-PackageLayout.ps1`. Expected result: nine packages.
5. Run `eng/Test-CheckpointsConsumer.ps1`. Expected result: a base-only consumer passes and its lock file holds no delivery,
   SQLite or session package.
6. Run `eng/Test-DeliveryConsumer.ps1`. Expected result: the nine conformance cases pass on the new delivery package.
7. Format check: the `CurrentListener` fixtures and the current-format tests pass unchanged against the new assembly.

The contract shapes are those finally decided; see contracts/.
