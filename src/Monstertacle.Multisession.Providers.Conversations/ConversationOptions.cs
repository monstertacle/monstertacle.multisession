using Monstertacle.Multisession.NativeHttp;
namespace Monstertacle.Multisession.Providers.Conversations;

/// <summary>Explicit host connection for the Conversations and Responses REST contract.</summary>
public sealed record ConversationOptions
{
    /// <summary>Gets the approved API base URI.</summary>
    public required Uri Endpoint { get; init; }
    /// <summary>Gets a host resolver reference, never a persisted credential.</summary>
    public required string CredentialReference { get; init; }
    /// <summary>Gets whether HTTP is explicitly allowed for loopback fixtures.</summary>
    public bool AllowLocalHttp { get; init; }
    /// <summary>Gets the finite byte limit for native JSON response bodies, including metadata.</summary>
    public int MaximumResponseBytes { get; init; } = NativeHttpDefaults.MaximumResponseBytes;
}
