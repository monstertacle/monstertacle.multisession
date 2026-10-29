using System.Text.Json;

namespace Monstertacle.Multisession.ValidationHost;

// Public CLI probes are distinct from the bounded internal crash-worker protocols.
internal static class ScenarioRunner
{
    internal static async Task<int> RunAsync(string[] args)
    {
        ValidationReport? report = ValidationArguments.Parse(args, out ValidationArguments? options);
        report ??= ValidationScenarioCatalog.IsLive(options!.Scenario)
            ? ValidationReport.NotRun(options.Scenario, "LiveApprovalRequired")
            : ValidationReport.NotRun(options.Scenario, "ScenarioPendingImplementation");
        await Console.Out.WriteLineAsync(JsonSerializer.Serialize(report));
        return report.ExitCode;
    }
}
