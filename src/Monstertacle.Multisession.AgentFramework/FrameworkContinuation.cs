using System.Text.Json;
namespace Monstertacle.Multisession.AgentFramework;

internal sealed record FrameworkContinuation(string AgentId, Guid SessionId, JsonElement State);
