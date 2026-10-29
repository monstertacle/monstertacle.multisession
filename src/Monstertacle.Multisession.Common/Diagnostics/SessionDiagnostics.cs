using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Diagnostics;

/// <summary>Host-owned, content-free diagnostics dedicated to one runtime. Dispose after that runtime has stopped.</summary>
public sealed class SessionDiagnostics : IDisposable
{
    private readonly ActivitySource _activities = new(SourceName);
    private readonly SessionMeasurements _measurements;
    private readonly ILogger? _logger;
    private long _failures;
    private int _disposed;
    /// <summary>Gets the stable name shared by diagnostic activity sources.</summary>
    public const string SourceName = "Monstertacle.Multisession";
    /// <summary>Gets the local diagnostic instance identity.</summary>
    public Guid InstanceId { get; } = Guid.NewGuid();
    /// <summary>Creates an isolated diagnostic source; the logger remains host-owned.</summary>
    public SessionDiagnostics(ILogger? logger = null)
    {
        _logger = logger;
        _measurements = new(InstanceId, () => Interlocked.Increment(ref _failures));
    }
    /// <summary>Gets the number of isolated diagnostic callback failures.</summary>
    public long FailureCount => Interlocked.Read(ref _failures);
    /// <summary>Publishes an authoritative local snapshot; null withdraws unavailable statistics.</summary>
    public void UpdateRuntimeStatistics(RuntimeStatistics? statistics)
    {
        if (Volatile.Read(ref _disposed) == 0) { _measurements.Update(statistics); }
    }
    internal void StatisticsUnavailable()
    {
        UpdateRuntimeStatistics(null);
        Interlocked.Increment(ref _failures);
    }

    /// <summary>Reports confirmed durable acceptance without input contents.</summary>
    public void RecordAccepted(Guid sessionId, RequestReceipt receipt, TimeSpan elapsed)
    {
        if (Volatile.Read(ref _disposed) != 0) { return; }
        Write("request.accepted", new(sessionId, receipt.RequestId, null), RequestState.Queued, ExecutionCertainty.NotStarted, null, elapsed);
        _measurements.Acceptance(elapsed);
    }
    /// <summary>Reports a confirmed stored result, excluding provider output and binding data.</summary>
    public void RecordCompleted(ExecutionReservation reservation, ProviderReply reply, TimeSpan elapsed)
    {
        if (Volatile.Read(ref _disposed) != 0) { return; }
        Write("request.completed", new(reservation.SessionId, reservation.RequestId, reservation.DispatchId),
            reply.Outcome, reply.Certainty, reply.Error?.Code, elapsed);
        _measurements.Completion(reply, elapsed);
    }
    /// <summary>Releases the diagnostic source. The supplied logger remains host-owned.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _activities.Dispose();
            _measurements.Dispose();
        }
    }

    private void Write(string transition, Correlation ids, RequestState state,
        ExecutionCertainty certainty, ErrorCode? code, TimeSpan elapsed)
    {
        if (Volatile.Read(ref _disposed) != 0) { return; }
        KeyValuePair<string, object?>[] values =
        [
            new("instance.id", InstanceId), new("session.id", ids.Session), new("request.id", ids.Request), new("dispatch.id", ids.Dispatch),
            new("transition", transition), new("request.state", Name(state)), new("execution.certainty", Name(certainty)),
            new("error.code", code is null ? null : Name(code.Value)), new("duration.seconds", Math.Max(0, elapsed.TotalSeconds))
        ];
        try
        {
            using Activity? activity = _activities.StartActivity(transition, ActivityKind.Internal, default(ActivityContext), values);
        }
        catch (Exception) { Interlocked.Increment(ref _failures); }
        try
        {
            if (_logger?.IsEnabled(LogLevel.Information) == true)
            {
                _logger.Log(LogLevel.Information, new EventId(transition == "request.accepted" ? 1 : 2, transition),
                    values, null, static (_, _) => "Multisession state transition");
            }
        }
        catch (Exception) { Interlocked.Increment(ref _failures); }
    }

    internal static string Name<T>(T value) where T : struct, Enum => Enum.IsDefined(value) ? value.ToString() : "Unknown";
    private sealed record Correlation(Guid Session, Guid Request, Guid? Dispatch);
}
