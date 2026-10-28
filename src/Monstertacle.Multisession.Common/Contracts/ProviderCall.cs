namespace Monstertacle.Multisession.Contracts;

/// <summary>A single dispatch with only its new input, never reconstructed history.</summary>
/// <param name="Session">Immutable session context.</param>
/// <param name="RequestId">Local accepted request identity.</param>
/// <param name="DispatchId">Durably reserved dispatch identity.</param>
/// <param name="Input">Unmodified new text.</param>
/// <param name="Timeout">Finite execution deadline measured from dispatch.</param>
/// <param name="MaxOutputBytes">Inclusive UTF-8 output limit.</param>
public sealed record ProviderCall(
    ProviderSessionContext Session,
    Guid RequestId,
    Guid DispatchId,
    string Input,
    TimeSpan Timeout,
    int MaxOutputBytes);
