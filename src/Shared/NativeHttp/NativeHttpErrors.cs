using System.Net;
using System.Text.Json;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.NativeHttp;

internal static class NativeHttpErrors
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
    internal static async ValueTask<SessionOperationException> ResponseAsync(HttpResponseMessage response, int maximumBytes, Func<JsonElement, ErrorCode> classifyBadRequest, CancellationToken cancellationToken)
    {
        if (response.StatusCode != HttpStatusCode.BadRequest) { return Http(response.StatusCode); }
        try
        {
            using JsonDocument document = await NativeHttpBody.ReadAsync(response.Content, maximumBytes, cancellationToken);
            return Create(classifyBadRequest(document.RootElement), ExecutionCertainty.NotStarted);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return Create(ErrorCode.ExecutionFailed, ExecutionCertainty.NotStarted); }
    }
}
