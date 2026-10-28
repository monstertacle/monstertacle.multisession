namespace Monstertacle.Multisession.Contracts;

/// <summary>Exposes a stable typed failure without raw external exception or response text.</summary>
public sealed class SessionOperationException : InvalidOperationException
{
    /// <summary>Creates a failure whose public message is only its stable error category.</summary>
    public SessionOperationException(PublicError failure)
        : base(failure?.Code.ToString() ?? throw new ArgumentNullException(nameof(failure)))
    {
        Failure = failure;
    }

    /// <summary>Gets the category, local identities and separately recorded execution evidence.</summary>
    public PublicError Failure { get; }
}
