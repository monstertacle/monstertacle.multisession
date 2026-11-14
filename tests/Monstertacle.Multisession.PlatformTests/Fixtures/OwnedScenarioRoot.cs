namespace Monstertacle.Multisession.PlatformTests.Fixtures;

/// <summary>
/// One fresh state root owned by one test (follow-up-native-scenario-root-ownership.md, T021). After the test passed, only this
/// root is deleted; after a failure it is kept as evidence and its path is reported. The original failure always stays first:
/// the caller rethrows it, and a failing report is thrown together with it. Nothing else is enumerated or deleted, and the
/// product's own cleanup, which only releases the lease, is not changed.
/// </summary>
internal sealed class OwnedScenarioRoot(string parent, string prefix)
{
    private bool _passed;

    internal string Path { get; } = System.IO.Path.Combine(parent, prefix + Guid.NewGuid().ToString("N"));

    internal static OwnedScenarioRoot UnderScratch(string prefix)
        => new(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "dev", ".agenttemp"), prefix);

    // Called as the last statement of the test body, after every assertion.
    internal void Passed() => _passed = true;

    // Called from the test's catch with the body failure; the caller then rethrows that failure unchanged with `throw;`.
    internal void Report(Exception primary, Action<string> report)
    {
        string kept = "Native scenario failure evidence kept at " + Path;
        try { report(kept); }
        catch (Exception output)
        {
            throw new AggregateException(kept + "; reporting it failed after the test failed.", primary, output);
        }
    }

    // Called from the test's finally, after its handles are disposed. Only a passed test deletes its own root. Before deleting, each
    // owned store file is opened exclusively once (SDK-PR41-WIN-SHUTDOWN-LOCK-001, P-SHUTDOWN-1): a store handle that outlived the
    // scenario is reported as its own failure naming that file, and the root is kept. A deletion failure is reported as a cleanup
    // failure naming the kept root. Nothing is retried, cleared or swallowed; a failed test never reaches either step.
    internal void Finish()
    {
        if (!_passed || !Directory.Exists(Path)) { return; }
        foreach (string store in Directory.EnumerateFiles(Path, "multisession.db", SearchOption.AllDirectories))
        {
            try
            {
                using FileStream probe = new(store, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException held)
            {
                throw new IOException("Native scenario store still open after the scenario passed: " + store + "; root kept at " + Path, held);
            }
        }
        try { Directory.Delete(Path, recursive: true); }
        catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException)
        {
            throw new IOException("Native scenario cleanup failed after a pass; root kept at " + Path, cleanup);
        }
    }
}
