namespace Monstertacle.Multisession.Contracts;

/// <summary>Explicit local deletion under the store's immutable retention policy.</summary>
public interface ISessionRetentionStore
{
    /// <summary>Deletes one eligible closed local session and its contents atomically, without provider I/O.</summary>
    ValueTask DeleteClosedSessionAsync(Guid sessionId, CancellationToken cancellationToken);
}
