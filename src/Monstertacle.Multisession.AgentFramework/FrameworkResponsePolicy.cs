using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Requests;
namespace Monstertacle.Multisession.AgentFramework;

internal static class FrameworkResponsePolicy
{
    // The pinned 1.27.0 background token is checked only to reject resumable/pending responses.
#pragma warning disable MEAI001
    internal static bool IsTerminal(AgentResponse response)
    {
        ChatMessage? final = response.Messages.LastOrDefault();
        return response.FinishReason == ChatFinishReason.Stop && response.ContinuationToken is null
            && final?.Role == ChatRole.Assistant && final.Contents.Count > 0
            && final.Contents.All(content => content is TextContent) && !string.IsNullOrEmpty(final.Text);
    }

#pragma warning restore MEAI001
    internal static ProviderReply Complete(AgentResponse response, ProviderBinding? binding, int maximumBytes)
    {
        string output = response.Messages[^1].Text;
        ProviderReply reply = new(RequestState.Succeeded, ExecutionCertainty.ConfirmedEnded, output, true, binding, null) { ContinuationAvailable = binding is not null };
        return ProviderReplyPolicy.NormalizeOutput(reply, maximumBytes);
    }

    internal static ProviderReply Failure(ErrorCode code, ExecutionCertainty certainty, ProviderBinding? binding = null)
    {
        RequestState state = code switch
        {
            ErrorCode.Cancelled => RequestState.Cancelled,
            ErrorCode.TimedOut => RequestState.TimedOut,
            _ => RequestState.Failed,
        };
        return new(state, certainty, string.Empty, false, binding, new(code, certainty)) { ContinuationAvailable = false };
    }
}
