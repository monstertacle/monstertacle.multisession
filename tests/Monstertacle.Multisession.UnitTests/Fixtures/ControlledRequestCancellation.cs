using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Requests;

namespace Monstertacle.Multisession.UnitTests.Fixtures;

// Caller holds the common state gate; no native termination is inferred here.
internal static class ControlledRequestCancellation
{
    internal static RequestSnapshot Apply(ControlledStoreState state, Guid requestId)
    {
        RequestSnapshot before = state.Requests.GetValueOrDefault(requestId)
            ?? throw new SessionOperationException(new(ErrorCode.NotFound, RequestId: requestId));
        RequestSnapshot updated = RequestCancellationPolicy.Apply(before, state.Clock.GetUtcNow());
        if (updated == before) { return before; }
        state.Requests[requestId] = updated;
        if (before.State == RequestState.Queued)
        {
            ExecutionReservation local = new(Guid.NewGuid(), requestId, before.SessionId, null, updated.EndedUtc!.Value);
            state.Dispatch.RecordCompletion(new(local, state.Sessions[before.SessionId], state.Submissions[requestId], before.Sequence,
                state.Sessions[before.SessionId].BindingRevision), updated.Result!);
        }
        else { state.Dispatch.RequestCancellation(requestId); }
        return updated;
    }
}
