using Monstertacle.Multisession.NativeHttp;

namespace Monstertacle.Multisession.Providers.Gemini;

/// <summary>Host-owned connection to the stable Interactions REST contract.</summary>
public sealed record GeminiOptions
{
    /// <summary>Gets the approved API base URI.</summary>
    public Uri Endpoint { get; init; } = new("https://generativelanguage.googleapis.com/v1/");
    /// <summary>Gets the host resolver reference; credentials are never session state.</summary>
    public required string CredentialReference { get; init; }
    /// <summary>Gets whether loopback-only plaintext fixtures are explicitly allowed.</summary>
    public bool AllowLocalHttp { get; init; }
    /// <summary>Gets the finite limit for native JSON response bodies.</summary>
    public int MaximumResponseBytes { get; init; } = NativeHttpDefaults.MaximumResponseBytes;
}
