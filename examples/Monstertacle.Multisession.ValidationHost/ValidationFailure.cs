using System.Text.Json.Serialization;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.ValidationHost;

// Serialized diagnostic payload, with no execution or resource-lifetime responsibility.
internal sealed record ValidationFailure(PublicError Error, FailureDetails? Details = null)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ExceptionType { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Site { get; init; }

    internal static ValidationFailure FromException(Exception failure) => failure switch
    {
        SessionOperationException operation => new(operation.Failure, operation.Details),
        AdmissionCancellationException cancellation => new(cancellation.Failure, cancellation.Details),
        ValidationProbeException { Failure: { } primary } => primary,
        // An untyped failure keeps the generic public code and names its category and throw site for attribution.
        _ => new(new PublicError(ErrorCode.ExecutionFailed)) { ExceptionType = failure.GetType().Name, Site = ThrowSite.Of(failure) },
    };
}
