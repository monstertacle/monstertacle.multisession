using System.Net;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.NativeHttp;

internal static class NativeHttpReadRetry
{
    internal static TimeSpan? Delay(HttpMethod method, HttpResponseMessage response, TimeProvider clock)
    {
        if (method != HttpMethod.Get || response.StatusCode is not (HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable)) { return null; }
        TimeSpan delay = response.Headers.RetryAfter?.Delta
            ?? (response.Headers.RetryAfter?.Date is { } date ? date - clock.GetUtcNow() : NativeHttpDefaults.PollInterval);
        if (delay < NativeHttpDefaults.PollInterval) { return NativeHttpDefaults.PollInterval; }
        return delay > RuntimeDefaults.MaximumTimerTimeout ? RuntimeDefaults.MaximumTimerTimeout : delay;
    }
}
