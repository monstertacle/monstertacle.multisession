namespace Monstertacle.Multisession.Delivery.Tests;

/// <summary>Drives a new checkpoint into a named phase through the public transitions.</summary>
internal static class CheckpointScenario
{
    public static async Task<DeliveryCheckpoint> OpenAsync(string path, int maximumRetainedGrants = DeliveryLimits.RetainedGrants)
    {
        var options = new DeliveryCheckpointOptions(path, Payloads.Channel) { BusyTimeout = TimeSpan.FromSeconds(1), MaximumRetainedGrants = maximumRetainedGrants };
        return await DeliveryCheckpoint.OpenAsync(options, CancellationToken.None);
    }

    public static async Task<CheckpointExpectation> PendingAsync(DeliveryCheckpoint checkpoint)
    {
        await checkpoint.StageAsync(1, Payloads.Item(1), CancellationToken.None);
        return (await checkpoint.ReadAsync(CancellationToken.None)).ToExpectation();
    }

    public static async Task<CheckpointExpectation> UncertainAsync(DeliveryCheckpoint checkpoint)
    {
        await checkpoint.StageAsync(1, Payloads.Item(1), CancellationToken.None);
        await checkpoint.MarkSendingAsync(CancellationToken.None);
        await checkpoint.MarkUncertainAsync(CancellationToken.None);
        return (await checkpoint.ReadAsync(CancellationToken.None)).ToExpectation();
    }

    public static void FillJournal(string path, string table, int rows)
    {
        using var connection = RawDatabase.Open(path);
        for (var row = 0; row < rows; row++)
        {
            using var command = connection.CreateCommand();
            command.CommandText = table switch
            {
                "delivery_replays" => "INSERT INTO delivery_replays VALUES($id,$item,'{}')",
                "delivery_rebinds" => "INSERT INTO delivery_rebinds VALUES($id,'a','b',0,$item,'{}')",
                _ => "INSERT INTO preparation_rearms VALUES($id,'c','r',0,$item,'d',0,'{}')",
            };
            command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
            command.Parameters.AddWithValue("$item", Guid.NewGuid().ToString());
            command.ExecuteNonQuery();
        }
    }
}
