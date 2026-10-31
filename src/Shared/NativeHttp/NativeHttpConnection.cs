namespace Monstertacle.Multisession.NativeHttp;

internal sealed record NativeHttpConnection(Uri Endpoint, string CredentialReference,
    string CredentialHeader, string? AuthenticationScheme, int MaximumResponseBytes);
