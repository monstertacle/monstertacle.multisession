namespace Monstertacle.Multisession.Contracts;

/// <summary>Atomic local closure, independent of external provider deletion.</summary>
public interface ISessionClosureStore
{
    /// <summary>Closes acceptance, cancels queued input and records active cancellation intent atomically.</summary>
    ValueTask<SessionSnapshot> BeginSessionCloseAsync(Guid sessionId, CancellationToken cancellationToken);
    /// <summary>Closes only when no pending or uncertain work or recovery case remains.</summary>
    ValueTask<SessionSnapshot> RefreshSessionCloseAsync(Guid sessionId, CancellationToken cancellationToken);
}
