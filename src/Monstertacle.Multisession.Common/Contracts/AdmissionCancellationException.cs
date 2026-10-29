namespace Monstertacle.Multisession.Contracts;

/// <summary>Caller cancellation with explicit acceptance certainty and a safe lookup key.</summary>
public sealed class AdmissionCancellationException : OperationCanceledException
{
    /// <summary>Creates a sanitized cancellation without provider or database exception text.</summary>
    public AdmissionCancellationException(PublicError failure, CancellationToken cancellationToken)
        : base(failure.Code.ToString(), null, cancellationToken) => Failure = failure;
    /// <summary>Gets local acceptance status and candidate identity, when already generated.</summary>
    public PublicError Failure { get; }
}
