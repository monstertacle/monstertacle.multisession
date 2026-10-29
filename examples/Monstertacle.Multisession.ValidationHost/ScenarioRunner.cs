using System.Text.Json;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.ValidationHost;

// Public CLI probes are distinct from the bounded internal crash-worker protocols.
internal static class ScenarioRunner
{
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
        if (options.Scenario is not ("embedded-consumer" or "deterministic" or "routing" or "failures" or "cancellation"))
        {
            return ValidationReport.NotRun(options.Scenario, "ScenarioPendingImplementation");
        }
        try
        {
            await using ValidationRunState state = await ValidationStateRoot.CreateRunAsync(options.StateRoot, CancellationToken.None);
            if (options.Scenario == "embedded-consumer") { await ConsumerProbeFlow.ExecuteAsync(state); }
            else if (options.Scenario == "cancellation") { await CancellationProbeFlow.ExecuteAsync(state); }
            else { await WaveProbeFlow.ExecuteAsync(state, options.Scenario); }
            return ValidationReport.Pass(options.Scenario, state.RunId);
        }
        catch (ValidationProbeException failure) { return ValidationReport.Fail(options.Scenario, failure.Code); }
        catch (SessionOperationException failure) { return ValidationReport.Fail(options.Scenario, failure.Failure.Code.ToString()); }
        catch (Exception) { return ValidationReport.Fail(options.Scenario, "ScenarioExecutionFailed"); }
    }
}
