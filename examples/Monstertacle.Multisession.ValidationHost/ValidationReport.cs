using System.Runtime.InteropServices;
using Microsoft.Data.Sqlite;

namespace Monstertacle.Multisession.ValidationHost;

internal sealed record ValidationReport(string Scenario, string Status, string Reason)
{
    public int ProcessId { get; } = Environment.ProcessId;
    public Guid? RunId { get; init; }
    public string? OperatingSystem { get; init; }
    public string? RuntimeVersion { get; init; }
    public string? SqliteVersion { get; init; }
    public int ExitCode => Status switch { "PASS" => 0, "FAIL" => 2, _ => 3 };
    internal static ValidationReport Fail(string scenario, string reason) => new(scenario, "FAIL", reason);
    internal static ValidationReport NotRun(string scenario, string reason) => new(scenario, "NOT RUN", reason);
    internal static ValidationReport Pass(string scenario, Guid runId) => new(scenario, "PASS", "Completed")
    {
        RunId = runId,
        OperatingSystem = RuntimeInformation.OSDescription,
        RuntimeVersion = Environment.Version.ToString(),
        SqliteVersion = ReadSqliteVersion(),
    };
    private static string ReadSqliteVersion()
    {
        using SqliteConnection connection = new("Data Source=:memory:");
        connection.Open();
        return connection.ServerVersion;
    }
}
