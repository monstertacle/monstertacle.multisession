using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.UnitTests.Fixtures;

// Controlled observations only; runtime access is serialized by the owning store's gate.
internal sealed class ControlledDispatchState
{
    private readonly HashSet<Guid> _cancellationRequests = [];
    private readonly List<HistoryEntry> _history = [];
    private readonly Dictionary<Guid, long> _historyOrdinals = [];
    private readonly List<(Guid DispatchId, ProviderObservation Observation)> _observations = [];

    public Func<ProviderObservation, CancellationToken, ValueTask>? Observing { get; init; }
    public IReadOnlyList<(Guid DispatchId, ProviderObservation Observation)> Observations => _observations;
    public Action? Completed { get; init; }
    public Func<CancellationToken, ValueTask>? MarkingUncertain { get; init; }
    public Func<CancellationToken, ValueTask>? Reading { get; init; }
    public Func<CancellationToken, ValueTask>? Committing { get; init; }
    public Dictionary<Guid, ReservedDispatch> Snapshots { get; } = [];
    public IReadOnlyList<HistoryEntry> History => _history;

    public bool IsCancellationRequested(Guid requestId) => _cancellationRequests.Contains(requestId);

    // Setup hook used synchronously between store operations, not a production cancellation API.
    public void RequestCancellation(Guid requestId) => _cancellationRequests.Add(requestId);

    public string? OperationReference(Guid dispatchId)
        => _observations.LastOrDefault(item => item.DispatchId == dispatchId && item.Observation.Kind == ProviderObservationKind.OperationAccepted).Observation?.Reference;

    public void RecordObservation(Guid dispatchId, ProviderObservation observation)
        => _observations.Add((dispatchId, observation));

    public void RecordCompletion(ReservedDispatch snapshot, ProviderReply reply)
    {
        long ordinal = checked(_historyOrdinals.GetValueOrDefault(snapshot.Reservation.SessionId) + 1);
        _historyOrdinals[snapshot.Reservation.SessionId] = ordinal;
        _history.Add(new HistoryEntry(snapshot.Reservation.SessionId, snapshot.Reservation.RequestId,
            snapshot.Sequence, snapshot.Submission.Input, reply)
        { HistoryOrdinal = ordinal, HistoryId = Guid.NewGuid() });
    }
}
