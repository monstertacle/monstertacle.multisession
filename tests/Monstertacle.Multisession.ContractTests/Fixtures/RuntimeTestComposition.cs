using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.UnitTests.Fixtures;

namespace Monstertacle.Multisession.ContractTests.Fixtures;

internal static class RuntimeTestComposition
{
    internal static RuntimeOptions Options() => new() { StateDirectory = Path.GetTempPath(), Retention = new(true, null), MaxConcurrentRequests = 2 };
    internal static SessionRuntime Runtime(InMemorySessionStore store, ControlledProvider provider, TestStateDirectoryLease lease)
        => new(store, lease, [new("registered-adapter", "test-adapter", 1, provider)], []);
}
