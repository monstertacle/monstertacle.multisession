using Xunit;

// Independent real-I/O fixtures share finite runner resources; concurrency is asserted inside each scenario.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
