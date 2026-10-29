using System.Net;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Providers.OpenAI;

internal static class OpenAiReadRetry
{
    private static readonly TimeSpan _initialDelay = TimeSpan.FromSeconds(1);
    internal static TimeSpan? Delay(HttpMethod method, HttpResponseMessage response, TimeProvider clock)
    {
        if (method != HttpMethod.Get || response.StatusCode is not (HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable)) { return null; }
        TimeSpan delay = response.Headers.RetryAfter?.Delta
            ?? (response.Headers.RetryAfter?.Date is { } date ? date - clock.GetUtcNow() : _initialDelay);
        if (delay < _initialDelay) { return _initialDelay; }
        return delay > RuntimeDefaults.MaximumTimerTimeout ? RuntimeDefaults.MaximumTimerTimeout : delay;
    }
}
