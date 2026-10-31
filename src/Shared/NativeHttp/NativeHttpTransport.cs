using System.Text;
using System.Text.Json;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.NativeHttp;

internal sealed class NativeHttpTransport : IDisposable
{
    private readonly HttpClient _client;
    private readonly NativeHttpConnection _options;
    private readonly ICredentialResolver _credentials;
    private readonly TimeProvider _clock;
    private readonly Func<JsonElement, ErrorCode> _classifyBadRequest;

    internal NativeHttpTransport(NativeHttpConnection options, ICredentialResolver credentials, HttpMessageHandler? handler, TimeProvider clock, Func<JsonElement, ErrorCode> classifyBadRequest)
    {
        Uri endpoint = options.Endpoint;
        _options = options;
        _credentials = credentials;
        _clock = clock;
        _classifyBadRequest = classifyBadRequest;
        _client = handler is null
            ? new(new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false })
            : new(handler, disposeHandler: false);
        _client.BaseAddress = endpoint;
        _client.Timeout = Timeout.InfiniteTimeSpan;
    }

    internal async ValueTask<JsonDocument> SendAsync(HttpMethod method, string path, string? body, CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                TimeSpan? retryDelay;
                using (HttpRequestMessage request = new(method, path))
                {
                    if (body is not null) { request.Content = new StringContent(body, Encoding.UTF8, "application/json"); }
                    using HttpResponseMessage response = await HeadersAsync(request, cancellationToken);
                    retryDelay = NativeHttpReadRetry.Delay(method, response, _clock);
                    if (retryDelay is null)
                    {
                        if (!response.IsSuccessStatusCode) { throw await NativeHttpErrors.ResponseAsync(response, _options.MaximumResponseBytes, _classifyBadRequest, cancellationToken); }
                        return await NativeHttpBody.ReadAsync(response.Content, _options.MaximumResponseBytes, cancellationToken);
                    }
                }
                await Task.Delay(retryDelay.Value, _clock, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (SessionOperationException failure) { throw new SessionOperationException(failure.Failure); }
        catch (Exception) { throw NativeHttpErrors.Create(ErrorCode.ProviderUnavailable); }
    }
    private async ValueTask<HttpResponseMessage> HeadersAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using CancellationTokenSource deadline = new(NativeHttpDefaults.HeaderTimeout, _clock);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        string credential = await _credentials.ResolveAsync(_options.CredentialReference, linked.Token).AsTask().WaitAsync(linked.Token);
        request.Headers.Add(_options.CredentialHeader, _options.AuthenticationScheme is null ? credential : $"{_options.AuthenticationScheme} {credential}");
        Task<HttpResponseMessage> pending = _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linked.Token);
        try { return await pending.WaitAsync(linked.Token); }
        catch
        {
            _ = DisposeLateAsync(pending);
            throw;
        }
    }
    private static async Task DisposeLateAsync(Task<HttpResponseMessage> pending)
    {
        try { (await pending).Dispose(); }
        catch (Exception) { /* Observe a late transport fault without exposing its details. */ }
    }
    public void Dispose() => _client.Dispose();
}
