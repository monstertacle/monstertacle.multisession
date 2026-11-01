using Monstertacle.Multisession.Delivery.FixtureHarness;

[assembly: System.Runtime.Versioning.SupportedOSPlatform("linux")]

// Fixture-only helper processes for the current Listener: never pointed at live state.
return args switch
{
    ["feed", var events] => await FeedFixture.ServeAsync(events),
    ["rpc", var socket, var mode, var log] => await RpcFixtureServer.RunAsync(socket, mode, log),
    ["snapshot", var database, var output] => CheckpointSnapshot.Write(database, output),
    _ => Usage(),
};

static int Usage()
{
    Console.Error.WriteLine("FixtureHarness feed EVENTS | rpc SOCKET MODE_FILE LOG_FILE | snapshot DATABASE OUTPUT_JSON");
    return 2;
}
