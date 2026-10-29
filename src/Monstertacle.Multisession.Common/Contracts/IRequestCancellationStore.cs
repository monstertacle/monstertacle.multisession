namespace Monstertacle.Multisession.Contracts;

/// <summary>Atomic cancellation intent, independent of provider execution and termination evidence.</summary>
public interface IRequestCancellationStore
{
    /// <summary>Cancels queued input or records the first running cancellation intent; a final result is unchanged.</summary>
    ValueTask<RequestSnapshot> RequestCancellationAsync(Guid requestId, CancellationToken cancellationToken);
}
