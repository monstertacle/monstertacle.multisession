using Xunit;

namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>Controls that read the process-wide allocated-bytes counter run without competing test collections.</summary>
[CollectionDefinition("Process-wide allocation measurement", DisableParallelization = true)]
public sealed class AllocationMeasurementGroup;
