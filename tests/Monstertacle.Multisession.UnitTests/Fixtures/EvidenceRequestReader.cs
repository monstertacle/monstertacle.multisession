using Monstertacle.Multisession.Contracts;
namespace Monstertacle.Multisession.UnitTests.Fixtures;

internal sealed class EvidenceRequestReader : IRequests
{
    internal Dictionary<Guid, RequestSnapshot> Items { get; } = [];
    internal Func<Guid, RequestSnapshot?>? ReadOverride { get; set; }
    internal int Reads { get; private set; }
    internal int Submissions { get; private set; }
    public ValueTask<RequestSnapshot?> GetRequestAsync(Guid requestId, CancellationToken cancellationToken)
    {
        Reads++;
        return ValueTask.FromResult(ReadOverride is null ? Items.GetValueOrDefault(requestId) : ReadOverride(requestId));
    }
    public ValueTask<RequestReceipt> SubmitAsync(SubmitRequest request, CancellationToken cancellationToken)
    {
        Submissions++;
        throw new InvalidOperationException("Workflow must never submit");
    }
    public ValueTask<Page<RequestSnapshot>> ListRequestsAsync(Guid sessionId, int pageSize, string? cursor, CancellationToken cancellationToken) => throw new NotSupportedException();
    public ValueTask CancelRequestAsync(Guid requestId, CancellationToken cancellationToken) => throw new NotSupportedException();
}
