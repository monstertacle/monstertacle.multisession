using System.Text;
using System.Text.Json;
using Microsoft.Agents.AI;
using Monstertacle.Multisession.Contracts;
namespace Monstertacle.Multisession.AgentFramework;

internal sealed class FrameworkBindingCodec(FrameworkAgentOptions options)
{
    internal const int FormatVersion = 1;

    internal async ValueTask<ProviderBinding> EncodeAsync(AIAgent agent, AgentSession session, ProviderSessionContext context, CancellationToken cancellationToken)
    {
        using CancellationTokenSource deadline = new(options.SessionOperationTimeout);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        JsonElement state = await agent.SerializeSessionAsync(session, cancellationToken: linked.Token).AsTask().WaitAsync(linked.Token);
        string json = JsonSerializer.Serialize(new FrameworkContinuation(agent.Id, context.SessionId, state));
        if (Encoding.UTF8.GetByteCount(json) > options.MaximumStateBytes) { throw InvalidState(); }
        return new(options.AdapterId, FormatVersion, context.SessionId.ToString("N"), json);
    }

    internal async ValueTask<AgentSession> RestoreAsync(AIAgent agent, ProviderSessionContext context, ProviderBinding binding, CancellationToken cancellationToken)
    {
        if (binding.AdapterId != options.AdapterId || binding.FormatVersion != FormatVersion
            || binding.SessionReference != context.SessionId.ToString("N") || string.IsNullOrWhiteSpace(binding.ContinuationState)
            || Encoding.UTF8.GetByteCount(binding.ContinuationState) > options.MaximumStateBytes)
        {
            throw InvalidState();
        }
        FrameworkContinuation state;
        try { state = JsonSerializer.Deserialize<FrameworkContinuation>(binding.ContinuationState) ?? throw InvalidState(); }
        catch (JsonException) { throw InvalidState(); }
        if (state.AgentId != agent.Id || state.SessionId != context.SessionId || state.State.ValueKind is JsonValueKind.Undefined) { throw InvalidState(); }
        using CancellationTokenSource deadline = new(options.SessionOperationTimeout);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        return await agent.DeserializeSessionAsync(state.State, cancellationToken: linked.Token).AsTask().WaitAsync(linked.Token);
    }

    private static SessionOperationException InvalidState() => new(new PublicError(ErrorCode.ProviderSessionExpired));
}
