using System.Text.Json;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Providers.OpenAI;

internal static class OpenAiResponseBody
{
    private const int _bufferSize = 8192;
    internal static async ValueTask<JsonDocument> ReadAsync(HttpContent content, int maximumBytes, CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength > maximumBytes) { throw OpenAiFailure.Create(ErrorCode.OutputLimitExceeded); }
        await using Stream stream = await content.ReadAsStreamAsync(cancellationToken).WaitAsync(cancellationToken);
        using MemoryStream body = new();
        byte[] buffer = new byte[_bufferSize];
        while (true)
        {
            int count = await stream.ReadAsync(buffer, cancellationToken).AsTask().WaitAsync(cancellationToken);
            if (count == 0) { break; }
            if (count > maximumBytes - body.Length) { throw OpenAiFailure.Create(ErrorCode.OutputLimitExceeded); }
            body.Write(buffer.AsSpan(0, count));
        }
        return JsonDocument.Parse(body.ToArray());
    }
}
