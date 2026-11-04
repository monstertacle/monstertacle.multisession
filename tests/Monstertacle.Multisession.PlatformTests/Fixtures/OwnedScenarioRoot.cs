namespace Monstertacle.Multisession.PlatformTests.Fixtures;

/// <summary>
/// One fresh state root owned by one test (follow-up-native-scenario-root-ownership.md, T021). After the test passed, only this
/// root is deleted; after a failure it is kept as evidence and its path is reported. Nothing else is enumerated or deleted, and
/// the product's own cleanup, which only releases the lease, is not changed.
/// </summary>
internal sealed class OwnedScenarioRoot(string parent, string prefix)
{
    private bool _passed;

    internal string Path { get; } = System.IO.Path.Combine(parent, prefix + Guid.NewGuid().ToString("N"));

    internal static OwnedScenarioRoot UnderScratch(string prefix)
        => new(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "dev", ".agenttemp"), prefix);

    // Called as the last statement of the test body, after every assertion.
    internal void Passed() => _passed = true;

    // Called from the test's finally, after its handles are disposed.
    internal void Finish(Action<string> report)
    {
        if (!_passed)
        {
            report("Native scenario failure evidence kept at " + Path);
            return;
        }
        if (Directory.Exists(Path)) { Directory.Delete(Path, recursive: true); }
    }
}
