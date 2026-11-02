namespace Monstertacle.Multisession.Checkpoints.Delivery.Tests;

/// <summary>
/// One delivery cycle shaped like the current host listener: it depends only on <see cref="IDeliveryCheckpoint"/>, stages the
/// next source item, marks Sending immediately before the external effect, and records Accepted or Uncertain. It never resends
/// an Uncertain item automatically.
/// </summary>
/// <param name="checkpoint">The delivery port; the composition root keeps the concrete store.</param>
/// <param name="source">Canonical bytes of the item at a position.</param>
/// <param name="prepare">The host's preparation before the effect; false means nothing was sent.</param>
/// <param name="send">The external effect: an acknowledgement, null or an exception when the outcome is unknown.</param>
internal sealed class HostShapedLoop(IDeliveryCheckpoint checkpoint, Func<long, byte[]> source, Func<bool> prepare,
    Func<ReadOnlyMemory<byte>, Task<string?>> send)
{
    public const string Accepted = "accepted", Uncertain = "uncertain", UncertainBlocked = "uncertain-blocked", PreparationFailed = "preparation-failed";

    public async Task<string> RunOnceAsync(CancellationToken cancellationToken)
    {
        var state = await checkpoint.ReadAsync(cancellationToken);
        if (state.Phase == DeliveryPhase.Uncertain)
        {
            return UncertainBlocked;
        }
        if (state.Pending is null)
        {
            await checkpoint.StageAsync(state.Position + 1, source(state.Position + 1), cancellationToken);
            state = await checkpoint.ReadAsync(cancellationToken);
        }
        var payload = state.Pending!.Payload;
        if (!prepare())
        {
            await checkpoint.RecordPreparationFailureAsync(cancellationToken);
            return PreparationFailed;
        }
        await checkpoint.MarkSendingAsync(cancellationToken);
        string? acknowledgement;
        try
        {
            acknowledgement = await send(payload);
        }
        catch (IOException)
        {
            acknowledgement = null;
        }
        if (acknowledgement is null)
        {
            await checkpoint.MarkUncertainAsync(cancellationToken);
            return Uncertain;
        }
        await checkpoint.AcceptAsync(payload, acknowledgement, cancellationToken);
        return Accepted;
    }
}
