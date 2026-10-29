using Xunit;

namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>Native volume creation and physical disk exhaustion run without concurrent test workloads.</summary>
[CollectionDefinition("Exclusive storage volume", DisableParallelization = true)]
public sealed class ExclusiveStorageVolumeGroup;
