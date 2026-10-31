using Microsoft.Agents.AI;
namespace Monstertacle.Multisession.UnitTests.Fixtures;

internal sealed class FrameworkTestSession : AgentSession
{
    internal int Turns { get; set; }
}
