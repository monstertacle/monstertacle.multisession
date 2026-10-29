using System.Net;
using System.Text.Json;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Providers.OpenAI;

internal static class OpenAiFailure
{
    internal static SessionOperationException Create(ErrorCode code, ExecutionCertainty certainty = ExecutionCertainty.Unknown)
        => new(new PublicError(code, certainty));
    internal static SessionOperationException Http(HttpStatusCode status)
        => status switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => Create(ErrorCode.AuthenticationFailed, ExecutionCertainty.NotStarted),
            HttpStatusCode.TooManyRequests => Create(ErrorCode.ProviderRateLimited, ExecutionCertainty.NotStarted),
            HttpStatusCode.NotFound => Create(ErrorCode.ProviderSessionExpired, ExecutionCertainty.NotStarted),
            _ => Create(ErrorCode.ProviderUnavailable),
        };
    internal static async ValueTask<SessionOperationException> ResponseAsync(HttpResponseMessage response, int maximumBytes, CancellationToken cancellationToken)
    {
        if (response.StatusCode != HttpStatusCode.BadRequest) { return Http(response.StatusCode); }
        try
        {
            using JsonDocument document = await OpenAiResponseBody.ReadAsync(response.Content, maximumBytes, cancellationToken);
            return Create(OpenAiResponse.NativeError(document.RootElement), ExecutionCertainty.NotStarted);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return Create(ErrorCode.ExecutionFailed, ExecutionCertainty.NotStarted); }
    }
}
