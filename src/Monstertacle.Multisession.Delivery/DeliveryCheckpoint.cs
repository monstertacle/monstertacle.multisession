using Microsoft.Data.Sqlite;

namespace Monstertacle.Multisession.Delivery;

/// <summary>
/// Durable custody of one delivery channel: one opaque pending item, its phase and attempt counter, held under an exclusive
/// lease. Every transition is a compare-and-swap on the single stored row. The checkpoint performs no external effect, no
/// retry and no background work; the host calls <see cref="MarkSendingAsync"/> immediately before its own external effect.
/// </summary>
public sealed class DeliveryCheckpoint : IDisposable
{
    private readonly CheckpointFile _file;

    private DeliveryCheckpoint(CheckpointFile file, DeliveryCheckpointOptions options)
    {
        _file = file;
        Options = options;
    }

    internal DeliveryCheckpointOptions Options { get; }

    internal SqliteConnection Connection => _file.Connection;

    /// <summary>
    /// Owner open: creates the checkpoint with the channel identity if it is missing, keeps state owner-only, refuses another
    /// identity and converts a Sending phase left by a previous owner to <see cref="DeliveryPhase.Uncertain"/>.
    /// </summary>
    /// <param name="options">Location, identity and bounds.</param>
    /// <param name="cancellationToken">Cancels before the checkpoint is opened.</param>
    /// <returns>The open checkpoint holding the exclusive lease until disposed.</returns>
    /// <exception cref="DeliveryRefusalException">Identity, format, lease or storage refusal.</exception>
    public static Task<DeliveryCheckpoint> OpenAsync(DeliveryCheckpointOptions options, CancellationToken cancellationToken) =>
        OpenAsync(options, owner: true, cancellationToken);

    /// <summary>
    /// Inspection open for operator work such as grants: refuses a missing file, creates and configures nothing and leaves a
    /// Sending phase unchanged. It holds the same exclusive lease.
    /// </summary>
    /// <param name="options">Location and identity; the initial position is ignored.</param>
    /// <param name="cancellationToken">Cancels before the checkpoint is opened.</param>
    /// <returns>The open checkpoint holding the exclusive lease until disposed.</returns>
    /// <exception cref="DeliveryRefusalException">Missing file, identity, format, lease or storage refusal.</exception>
    public static Task<DeliveryCheckpoint> OpenExistingAsync(DeliveryCheckpointOptions options, CancellationToken cancellationToken) =>
        OpenAsync(options, owner: false, cancellationToken);

    /// <summary>Reads the stored state; the pending payload is returned exactly as stored.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The current state.</returns>
    public Task<CheckpointState> ReadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return StorageFailures.RunAsync(() => CheckpointRow.ReadAsync(Connection, null, cancellationToken));
    }

    /// <summary>Stages the item at the next position when nothing is pending; attempts restart at zero.</summary>
    /// <param name="position">Source position of the item; must be the stored position plus one and at least 1.</param>
    /// <param name="payload">Canonical UTF-8 bytes produced by the host; never interpreted.</param>
    /// <param name="cancellationToken">Cancels before the change.</param>
    /// <returns>A task completing once the item is durably staged.</returns>
    /// <exception cref="DeliveryRefusalException"><see cref="DeliveryRefusal.TransitionConflict"/> or <see cref="DeliveryRefusal.InvalidInput"/>.</exception>
    public Task StageAsync(long position, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        if (position < 1)
        {
            throw new DeliveryRefusalException(DeliveryRefusal.InvalidInput);
        }
        var text = CheckpointInputs.Payload(payload, Options.MaximumPayloadBytes);
        return ChangeAsync("UPDATE checkpoint SET pending=$payload,phase='pending',attempts=0 WHERE id=1 AND pending IS NULL AND cursor=$prior", command =>
        {
            command.Parameters.AddWithValue("$payload", text);
            command.Parameters.AddWithValue("$prior", position - 1);
        }, cancellationToken);
    }

    /// <summary>Marks the pending item Sending and counts the attempt; call immediately before the external effect.</summary>
    /// <param name="cancellationToken">Cancels before the change.</param>
    /// <returns>A task completing once Sending is durable.</returns>
    /// <exception cref="DeliveryRefusalException"><see cref="DeliveryRefusal.TransitionConflict"/> unless Pending.</exception>
    public Task MarkSendingAsync(CancellationToken cancellationToken) =>
        ChangeAsync("UPDATE checkpoint SET phase='sending',attempts=attempts+1 WHERE id=1 AND phase='pending' AND attempts<2147483647", static _ => { }, cancellationToken);

    /// <summary>Counts a failed preparation that caused no external effect, only while Pending.</summary>
    /// <param name="cancellationToken">Cancels before the change.</param>
    /// <returns>True when the counter changed.</returns>
    public Task<bool> RecordPreparationFailureAsync(CancellationToken cancellationToken) =>
        TryChangeAsync("UPDATE checkpoint SET attempts=attempts+1 WHERE id=1 AND phase='pending' AND attempts<2147483647", static _ => { }, cancellationToken);

    /// <summary>Records that the outcome of the current send is unknown, only from Sending.</summary>
    /// <param name="cancellationToken">Cancels before the change.</param>
    /// <returns>True when the phase changed.</returns>
    public Task<bool> MarkUncertainAsync(CancellationToken cancellationToken) =>
        TryChangeAsync("UPDATE checkpoint SET phase='uncertain' WHERE id=1 AND phase='sending'", static _ => { }, cancellationToken);

    /// <summary>
    /// Records a positive acknowledgement for exactly the stored bytes: the position advances, the item is cleared and the
    /// acknowledgement is kept. Accepted means only that the host's adapter reported success.
    /// </summary>
    /// <param name="expectedPayload">The bytes that were sent; must equal the stored bytes.</param>
    /// <param name="acknowledgement">Non-blank acknowledgement of at most 1024 characters.</param>
    /// <param name="cancellationToken">Cancels before the change.</param>
    /// <returns>A task completing once Accepted is durable.</returns>
    /// <exception cref="DeliveryRefusalException"><see cref="DeliveryRefusal.TransitionConflict"/> or <see cref="DeliveryRefusal.InvalidInput"/>.</exception>
    public Task AcceptAsync(ReadOnlyMemory<byte> expectedPayload, string acknowledgement, CancellationToken cancellationToken)
    {
        CheckpointInputs.Acknowledgement(acknowledgement);
        var text = CheckpointInputs.Payload(expectedPayload, Options.MaximumPayloadBytes);
        return ChangeAsync("UPDATE checkpoint SET cursor=cursor+1,pending=NULL,phase='accepted',attempts=0,receipt=$acknowledgement WHERE id=1 AND phase='sending' AND pending=$payload", command =>
        {
            command.Parameters.AddWithValue("$acknowledgement", acknowledgement);
            command.Parameters.AddWithValue("$payload", text);
        }, cancellationToken);
    }

    /// <summary>Releases the connection and the exclusive lease.</summary>
    public void Dispose() => _file.Dispose();

    private static async Task<DeliveryCheckpoint> OpenAsync(DeliveryCheckpointOptions options, bool owner, CancellationToken cancellationToken)
    {
        CheckpointInputs.Validate(options);
        cancellationToken.ThrowIfCancellationRequested();
        var file = CheckpointFile.Open(options, owner);
        try
        {
            await StorageFailures.RunAsync(() => CheckpointOpening.PrepareAsync(file.Connection, options, owner, cancellationToken));
            return new DeliveryCheckpoint(file, options);
        }
        catch
        {
            file.Dispose();
            throw;
        }
    }

    private async Task ChangeAsync(string sql, Action<SqliteCommand> bind, CancellationToken cancellationToken)
    {
        if (!await TryChangeAsync(sql, bind, cancellationToken))
        {
            throw new DeliveryRefusalException(DeliveryRefusal.TransitionConflict);
        }
    }

    private Task<bool> TryChangeAsync(string sql, Action<SqliteCommand> bind, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return StorageFailures.RunAsync(async () =>
        {
            using var command = Connection.CreateCommand();
            command.CommandText = sql;
            bind(command);
            return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
        });
    }
}
