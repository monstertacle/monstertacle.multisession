using Xunit;

namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>Infrastructure discovery only, not a product behavior or TDD Green test.</summary>
public sealed class InfrastructureDiscoveryTests
{
    /// <summary>Verifies that the runner discovers a test in the compiled assembly.</summary>
    [Fact]
    public void DiscoversInfrastructureTest()
    {
        Assert.False(typeof(InfrastructureDiscoveryTests).Assembly.IsDynamic);
    }
}
