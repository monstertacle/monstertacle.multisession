# Validation guide

Use SDK10.0.403. No model credentials are needed for deterministic contract validation.

1. Restore locked packages and run UnitTests on the native net10.0 framework (net10.0-windows on Windows). The framework tests exercise agent continuation and evidence renewal without network calls.
2. Run IntegrationTests to exercise the actual durable runtime with deterministic agent sessions, restart and uncertain failures.
3. Run `pwsh ./scripts/Invoke-Quality.ps1` on each native platform. It includes the whole production inventory, the new integration, existing platform scope, audit and87-percent coverage.
4. Run `dotnet tool run codemetr-csharp -- check --format json` from the root.
5. Pack the solution and run `pwsh ./eng/PackageSmoke.ps1`; package-only composition must resolve the real framework dependency and run outside source project references.

Production host configuration supplies the AIAgent factory, workspace/tool permissions, endpoint/model and credentials externally. Do not copy fixtures as a live backend, infer remote cancellation from local token cancellation, or migrate active native sessions by changing package/class names.
