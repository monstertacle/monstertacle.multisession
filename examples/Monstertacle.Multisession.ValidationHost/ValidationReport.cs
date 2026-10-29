namespace Monstertacle.Multisession.ValidationHost;

internal sealed record ValidationReport(string Scenario, string Status, string Reason)
{
    public int ProcessId { get; } = Environment.ProcessId;
    public int ExitCode => Status switch { "PASS" => 0, "FAIL" => 2, _ => 3 };
    internal static ValidationReport Fail(string scenario, string reason) => new(scenario, "FAIL", reason);
    internal static ValidationReport NotRun(string scenario, string reason) => new(scenario, "NOT RUN", reason);
}
