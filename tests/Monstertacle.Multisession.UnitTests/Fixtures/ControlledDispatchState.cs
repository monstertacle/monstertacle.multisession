using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.UnitTests.Fixtures;

// Controlled observations only; runtime access is serialized by the owning store's gate.
internal sealed class ControlledDispatchState
{
    private readonly HashSet<Guid> _cancellationRequests = [];
    private readonly List<HistoryEntry> _history = [];

    public Dictionary<Guid, ReservedDispatch> Snapshots { get; } = [];
    public IReadOnlyList<HistoryEntry> History => _history;

    public bool IsCancellationRequested(Guid requestId) => _cancellationRequests.Contains(requestId);

    // Setup hook used synchronously between store operations, not a production cancellation API.
    public void RequestCancellation(Guid requestId) => _cancellationRequests.Add(requestId);

    public void RecordCompletion(ReservedDispatch snapshot, ProviderReply reply)
        => _history.Add(new HistoryEntry(snapshot.Reservation.SessionId, snapshot.Reservation.RequestId,
            snapshot.Sequence, snapshot.Submission.Input, reply));
}
