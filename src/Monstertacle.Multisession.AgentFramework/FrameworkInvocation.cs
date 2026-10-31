using Microsoft.Agents.AI;
using Monstertacle.Multisession.Contracts;
namespace Monstertacle.Multisession.AgentFramework;

/// <summary>Transient typed invocation context passed in run options; its awaited callbacks persist evidence only.</summary>
/// <remarks>Callbacks are accepted only while the invocation is running, are bounded in number and by the dispatch deadline,
/// and a failed or late callback makes the turn uncertain. Observations never authorize resumption or replay.</remarks>
public sealed class FrameworkInvocation
{
    /// <summary>Inclusive length of an opaque operation or native session identity; identities are references, not content.</summary>
    public const int MaximumReferenceCharacters = 512;
    private const int _externalMarker = 1;
    private const int _operationMarker = 2;
    private static readonly string _key = typeof(FrameworkInvocation).FullName!;
    private readonly Func<ProviderObservation, CancellationToken, ValueTask> _observe;
    private readonly CancellationToken _execution;
    private readonly int _maximum;
    private int _count;
    private int _pending;
    private int _closed;
    private int _failed;
    private int _evidence;

    internal FrameworkInvocation(ProviderCall call, Func<ProviderObservation, CancellationToken, ValueTask> observe, int maximum, CancellationToken execution)
    {
        (SessionId, RequestId, DispatchId) = (call.Session.SessionId, call.RequestId, call.DispatchId);
        (_observe, _maximum, _execution) = (observe, maximum, execution);
    }

    /// <summary>Gets the canonical SDK session identity.</summary>
    public Guid SessionId { get; }
    /// <summary>Gets the canonical SDK request identity.</summary>
    public Guid RequestId { get; }
    /// <summary>Gets the reserved dispatch identity.</summary>
    public Guid DispatchId { get; }

    internal bool Trustworthy => Volatile.Read(ref _failed) == 0 && Volatile.Read(ref _pending) == 0;
    /// <summary>Gets whether an external operation or native session was acknowledged, which contradicts any not-started fact.</summary>
    internal bool HasExternalEvidence => Volatile.Read(ref _evidence) != 0;

    /// <summary>Finds the invocation context supplied by the adapter, requiring the exact CLR type.</summary>
    public static bool TryGet(AgentRunOptions? options, out FrameworkInvocation? invocation)
    {
        invocation = options?.AdditionalProperties is { } properties && properties.TryGetValue(_key, out object? value) ? value as FrameworkInvocation : null;
        return invocation is not null;
    }

    /// <summary>Durably records that the external operation was actually accepted, before the agent continues.</summary>
    public ValueTask ObserveOperationAcceptedAsync(string operationId, CancellationToken cancellationToken)
        => RecordAsync(ProviderObservationKind.OperationAccepted, operationId, cancellationToken);
    /// <summary>Durably records the request-linked native session identity, before the agent continues.</summary>
    public ValueTask ObserveNativeSessionAsync(string nativeSessionId, CancellationToken cancellationToken)
        => RecordAsync(ProviderObservationKind.NativeSessionObserved, nativeSessionId, cancellationToken);
    /// <summary>Durably records content-free progress.</summary>
    public ValueTask ReportProgressAsync(CancellationToken cancellationToken) => RecordAsync(ProviderObservationKind.Progress, null, cancellationToken);

    internal AgentRunOptions CreateOptions() => new() { AdditionalProperties = new() { [_key] = this } };

    internal void Close() => Interlocked.Exchange(ref _closed, 1);

    private async ValueTask RecordAsync(ProviderObservationKind kind, string? reference, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _pending);
        try
        {
            if (kind != ProviderObservationKind.Progress)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(reference);
                if (reference.Length > MaximumReferenceCharacters) { throw new ArgumentException("The identity exceeds its bounded length.", nameof(reference)); }
            }
            // One external operation per invocation: a second acceptance would make the recorded identity ambiguous.
            int marker = kind == ProviderObservationKind.OperationAccepted ? _operationMarker : 0;
            if (Volatile.Read(ref _closed) != 0 || Interlocked.Increment(ref _count) > _maximum || (Interlocked.Or(ref _evidence, marker) & marker) != 0)
            {
                throw new InvalidOperationException("The invocation no longer accepts this observation.");
            }
            using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(_execution, cancellationToken);
            await _observe(new(kind, reference), linked.Token).AsTask().WaitAsync(linked.Token);
            if (Volatile.Read(ref _closed) != 0) { throw new InvalidOperationException("The invocation ended before the observation was acknowledged."); }
            if (kind != ProviderObservationKind.Progress) { Interlocked.Or(ref _evidence, _externalMarker); }
        }
        catch (Exception)
        {
            Interlocked.Exchange(ref _failed, 1);
            throw;
        }
        finally
        {
            Interlocked.Decrement(ref _pending);
        }
    }
}
