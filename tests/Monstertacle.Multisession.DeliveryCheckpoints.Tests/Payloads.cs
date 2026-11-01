using System.Text;

namespace Monstertacle.Multisession.DeliveryCheckpoints.Tests;

/// <summary>Opaque canonical test payloads and options. The library never interprets them.</summary>
internal static class Payloads
{
    public static readonly DeliveryChannel Channel = new("consumer-a", "source-a", "route-a");

    public static byte[] Item(long position) => Encoding.UTF8.GetBytes("{\"position\":" + position + ",\"text\":\"Grüße\"}");

    public static DeliveryCheckpointOptions Options(string path, DeliveryChannel? channel = null) =>
        new(path, channel ?? Channel) { BusyTimeout = TimeSpan.FromSeconds(1) };

    public static string Receipt(string label) => "{\"receipt\":\"" + label + "\"}";
}
