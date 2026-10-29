using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.ContractTests.Fixtures;

internal sealed class FixtureCredentials : ICredentialResolver
{
    internal int Calls { get; private set; }
    public ValueTask<string> ResolveAsync(string credentialReference, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls++;
        return ValueTask.FromResult($"fixture-key-{Calls}");
    }
}
