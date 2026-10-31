using Monstertacle.Multisession.NativeHttp;

namespace Monstertacle.Multisession.Providers.Interactions;

/// <summary>Host-owned connection to the stable Interactions REST contract.</summary>
public sealed record InteractionOptions
{
    /// <summary>Gets the approved API base URI.</summary>
    public required Uri Endpoint { get; init; }
    /// <summary>Gets the host resolver reference; credentials are never session state.</summary>
    public required string CredentialReference { get; init; }
    /// <summary>Gets whether loopback-only plaintext fixtures are explicitly allowed.</summary>
    public bool AllowLocalHttp { get; init; }
    /// <summary>Gets the finite limit for native JSON response bodies.</summary>
    public int MaximumResponseBytes { get; init; } = NativeHttpDefaults.MaximumResponseBytes;
}
