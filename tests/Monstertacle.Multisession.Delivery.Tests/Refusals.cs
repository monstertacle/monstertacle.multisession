using Xunit;

namespace Monstertacle.Multisession.Delivery.Tests;

/// <summary>Asserts a typed refusal.</summary>
internal static class Refusals
{
    public static async Task Expect(DeliveryRefusal reason, Func<Task> action)
    {
        var refusal = await Assert.ThrowsAsync<DeliveryRefusalException>(action);
        Assert.Equal(reason, refusal.Reason);
    }
}
