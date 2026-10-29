using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.NativeHttp;

internal static class NativeHttpEndpoint
{
    internal static Uri Normalize(Uri? endpoint, string credentialReference, int maximumResponseBytes, bool allowLocalHttp)
    {
        if (endpoint is null || !endpoint.IsAbsoluteUri || maximumResponseBytes < 1 || string.IsNullOrWhiteSpace(credentialReference)
            || !string.IsNullOrEmpty(endpoint.UserInfo) || !string.IsNullOrEmpty(endpoint.Query) || !string.IsNullOrEmpty(endpoint.Fragment)
            || (endpoint.Scheme != Uri.UriSchemeHttps && !(allowLocalHttp && endpoint.Scheme == Uri.UriSchemeHttp && endpoint.IsLoopback)))
        {
            throw NativeHttpErrors.Create(ErrorCode.InvalidConfiguration, ExecutionCertainty.NotStarted);
        }
        return new(endpoint.AbsoluteUri.TrimEnd('/') + "/", UriKind.Absolute);
    }
}
