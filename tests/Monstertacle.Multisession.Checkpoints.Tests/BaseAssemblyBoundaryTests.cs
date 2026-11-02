using System.Reflection;
using Xunit;

namespace Monstertacle.Multisession.Checkpoints.Tests;

/// <summary>
/// The base package stays a storage-independent contract: it references only the runtime and exports only the checkpoint
/// contract, so no delivery, SQLite, session or host type can enter it unnoticed.
/// </summary>
public sealed class BaseAssemblyBoundaryTests
{
    private static readonly Assembly _base = typeof(ICheckpoint<>).Assembly;

    /// <summary>The base assembly references only runtime assemblies.</summary>
    [Fact]
    public void ReferencesOnlyRuntimeAssemblies()
    {
        string[] references = [.. _base.GetReferencedAssemblies().Select(reference => reference.Name!)];

        Assert.NotEmpty(references);
        Assert.All(references, name => Assert.True(name == "System.Runtime" || name.StartsWith("System.", StringComparison.Ordinal), name));
    }

    /// <summary>The base assembly exports exactly the checkpoint contract and nothing delivery-specific.</summary>
    [Fact]
    public void ExportsOnlyTheCheckpointContract()
    {
        Type[] exported = _base.GetExportedTypes();

        Assert.Equal([typeof(ICheckpoint<>)], exported);
        Assert.True(typeof(ICheckpoint<>).IsInterface);
        Assert.Equal("Monstertacle.Multisession.Checkpoints", typeof(ICheckpoint<>).Namespace);
        Assert.Equal([typeof(IDisposable)], typeof(ICheckpoint<>).GetInterfaces());
    }
}
