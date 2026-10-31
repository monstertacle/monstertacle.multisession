using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.ValidationHost;

// Serialized diagnostic payload, with no execution or resource-lifetime responsibility.
internal sealed record ValidationFailure(PublicError Error, FailureDetails? Details = null)
{
    internal static ValidationFailure FromException(Exception failure) => failure switch
    {
        SessionOperationException operation => new(operation.Failure, operation.Details),
        AdmissionCancellationException cancellation => new(cancellation.Failure, cancellation.Details),
        ValidationProbeException { Failure: { } primary } => primary,
        _ => new(new PublicError(ErrorCode.ExecutionFailed)),
    };
}
