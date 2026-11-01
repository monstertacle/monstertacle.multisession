namespace Monstertacle.Multisession.Delivery.Tests;

/// <summary>A private temporary directory below the configured temporary root, removed after the test.</summary>
internal sealed class TestDirectory : IDisposable
{
    public TestDirectory()
    {
        Root = Path.Combine(Path.GetTempPath(), "delivery-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    public string StatePath => Path.Combine(Root, "state", "checkpoint.db");

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}
