using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.ValidationHost;

internal sealed record ValidationReport(string Scenario, string Status, string Reason)
{
    public int ProcessId { get; } = Environment.ProcessId;
    public Guid? RunId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ValidationFailure? Failure { get; init; }
    public IReadOnlyList<ValidationFailure> CleanupFailures { get; init; } = [];
    public string? OperatingSystem { get; init; }
    public string? RuntimeVersion { get; init; }
    public string? SqliteVersion { get; init; }
    public int ExitCode => Status switch { "PASS" => 0, "FAIL" => 2, _ => 3 };
    internal static ValidationReport Fail(string scenario, string reason) => new(scenario, "FAIL", reason);
    internal static ValidationReport FromException(string scenario, Exception failure)
        => failure is ValidationProbeException probe
            ? Fail(scenario, probe.Code) with { Failure = probe.Failure, CleanupFailures = probe.CleanupFailures }
            : Fail(scenario, failure is SessionOperationException operation ? operation.Failure.Code.ToString() : "ScenarioExecutionFailed")
                with
            { Failure = ValidationFailure.FromException(failure) };

    internal ValidationReport WithCleanup(Exception cleanup) => this with
    {
        Status = "FAIL",
        Reason = Status == "PASS" ? "ScenarioCleanupFailed" : Reason,
        CleanupFailures = [.. CleanupFailures,
            .. cleanup is ValidationProbeException { Failure: null, CleanupFailures.Count: > 0 } probe
                ? probe.CleanupFailures : [ValidationFailure.FromException(cleanup), .. (cleanup as ValidationProbeException)?.CleanupFailures ?? []]],
    };
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
