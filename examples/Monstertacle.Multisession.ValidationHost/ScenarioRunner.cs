using System.Collections.Frozen;
using System.Text.Json;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.ValidationHost;

// Public CLI probes are distinct from the bounded internal crash-worker protocols.
internal static class ScenarioRunner
{
    private static readonly FrozenDictionary<string, Func<ValidationRunState, Task>> _flows =
        new Dictionary<string, Func<ValidationRunState, Task>>(StringComparer.Ordinal)
        {
            ["embedded-consumer"] = ConsumerProbeFlow.ExecuteAsync,
            ["deterministic"] = static state => WaveProbeFlow.ExecuteAsync(state, "deterministic"),
            ["routing"] = static state => WaveProbeFlow.ExecuteAsync(state, "routing"),
            ["failures"] = static state => WaveProbeFlow.ExecuteAsync(state, "failures"),
            ["cancellation"] = CancellationProbeFlow.ExecuteAsync,
            ["capacity"] = CapacityProbeFlow.ExecuteAsync,
            ["workspace"] = WorkspaceProbeFlow.ExecuteAsync,
            ["limits"] = LimitsProbeFlow.ExecuteAsync,
            ["shutdown"] = ShutdownProbeFlow.ExecuteAsync,
            ["isolation"] = IsolationProbeFlow.ExecuteAsync,
            ["retention"] = RetentionProbeFlow.ExecuteAsync,
            ["extensions"] = ExtensionsProbeFlow.ExecuteAsync,
        }.ToFrozenDictionary(StringComparer.Ordinal);

    internal static async Task<int> RunAsync(string[] args)
    {
        ValidationReport? report = ValidationArguments.Parse(args, out ValidationArguments? options);
        report ??= await ExecuteAsync(options!);
        await Console.Out.WriteLineAsync(JsonSerializer.Serialize(report));
        return report.ExitCode;
    }

    private static async Task<ValidationReport> ExecuteAsync(ValidationArguments options)
    {
        if (ValidationScenarioCatalog.IsLive(options.Scenario)) { return ValidationReport.NotRun(options.Scenario, "LiveApprovalRequired"); }
        if (!_flows.TryGetValue(options.Scenario, out Func<ValidationRunState, Task>? execute))
        {
            return ValidationReport.NotRun(options.Scenario, "ScenarioPendingImplementation");
        }
        try
        {
            await using ValidationRunState state = await ValidationStateRoot.CreateRunAsync(options.StateRoot, CancellationToken.None);
            await execute(state);
            return ValidationReport.Pass(options.Scenario, state.RunId);
        }
        catch (ValidationProbeException failure) { return ValidationReport.Fail(options.Scenario, failure.Code); }
        catch (SessionOperationException failure) { return ValidationReport.Fail(options.Scenario, failure.Failure.Code.ToString()); }
        catch (Exception) { return ValidationReport.Fail(options.Scenario, "ScenarioExecutionFailed"); }
    }
}
