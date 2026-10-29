using Xunit;

namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>Native volumes and complete durable host workloads run without competing test collections.</summary>
[CollectionDefinition("Exclusive storage volume", DisableParallelization = true)]
public sealed class ExclusiveStorageVolumeGroup;
