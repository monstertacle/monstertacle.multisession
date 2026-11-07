using Monstertacle.Multisession.PlatformTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.PlatformTests;

/// <summary>
/// A test-owned scenario root is removed after a pass and kept and reported after a failure, the original failure always stays
/// first, and nothing else is touched. Each case uses the same catch and finally shape as the native ownership test.
/// </summary>
public sealed class OwnedScenarioRootTests
{
    /// <summary>After a pass only the test's own fresh root is deleted; a foreign sibling stays.</summary>
    [Fact]
    public void DeletesItsOwnRootAfterAPass()
    {
        string parent = Parent();
        try
        {
            OwnedScenarioRoot owned = new(parent, "case-");
            Directory.CreateDirectory(Path.Combine(owned.Path, "run-1"));
            File.WriteAllText(Path.Combine(owned.Path, "run-1", "state"), "state");
            string sentinel = Directory.CreateDirectory(Path.Combine(parent, "foreign-sentinel")).FullName;
            List<string> reported = [];
            Run(owned, () => { }, reported.Add);
            Assert.False(Directory.Exists(owned.Path));
            Assert.True(Directory.Exists(sentinel));
            Assert.Empty(reported);
        }
        finally { Remove(parent); }
    }

    /// <summary>After a failure the root is kept, its path is reported once and the same failure is rethrown.</summary>
    [Fact]
    public void KeepsAndReportsItsRootAfterAFailure()
    {
        string parent = Parent();
        try
        {
            OwnedScenarioRoot owned = new(parent, "case-");
            Directory.CreateDirectory(owned.Path);
            List<string> reported = [];
            InvalidOperationException primary = new("original assertion");
            Exception? thrown = Record.Exception(() => Run(owned, () => throw primary, reported.Add));
            Assert.Same(primary, thrown);
            Assert.True(Directory.Exists(owned.Path));
            Assert.Contains(owned.Path, Assert.Single(reported), StringComparison.Ordinal);
        }
        finally { Remove(parent); }
    }

    /// <summary>A passing test whose root was never created deletes nothing and leaves siblings alone.</summary>
    [Fact]
    public void DeletesNothingWhenThePassingRootWasNeverCreated()
    {
        string parent = Parent();
        try
        {
            OwnedScenarioRoot owned = new(parent, "case-");
            string sentinel = Directory.CreateDirectory(Path.Combine(parent, "foreign-sentinel")).FullName;
            Run(owned, () => { }, _ => throw new InvalidOperationException("a pass reports nothing"));
            Assert.True(Directory.Exists(sentinel));
            Assert.Single(Directory.GetDirectories(parent));
        }
        finally { Remove(parent); }
    }

    /// <summary>A failing reporter after a failed body never replaces the original failure, which comes first.</summary>
    [Fact]
    public void KeepsTheOriginalFailureFirstWhenTheReportFails()
    {
        string parent = Parent();
        try
        {
            OwnedScenarioRoot owned = new(parent, "case-");
            Directory.CreateDirectory(owned.Path);
            string sentinel = Directory.CreateDirectory(Path.Combine(parent, "foreign-sentinel")).FullName;
            InvalidOperationException primary = new("original assertion");
            IOException sink = new("output unavailable");
            Exception? thrown = Record.Exception(() => Run(owned, () => throw primary, _ => throw sink));
            AggregateException both = Assert.IsType<AggregateException>(thrown);
            Assert.Same(primary, both.InnerExceptions[0]);
            Assert.Same(sink, both.InnerExceptions[1]);
            Assert.Contains(owned.Path, both.Message, StringComparison.Ordinal);
            Assert.True(Directory.Exists(owned.Path));
            Assert.True(Directory.Exists(sentinel));
        }
        finally { Remove(parent); }
    }

    /// <summary>
    /// SDK-PR41-WIN-SHUTDOWN-LOCK-001 (P-SHUTDOWN-1): after a pass, a store handle that outlived the scenario is its own failure naming
    /// the store file, and the root is kept instead of being deleted around the open handle.
    /// </summary>
    [Fact]
    public void ReportsAStoreHandleThatOutlivedAPassAndKeepsTheRoot()
    {
        string parent = Parent();
        try
        {
            OwnedScenarioRoot owned = new(parent, "case-");
            string store = Path.Combine(Directory.CreateDirectory(Path.Combine(owned.Path, "run-1")).FullName, "multisession.db");
            File.WriteAllText(store, "store");
            Exception? thrown;
            using (FileStream held = new(store, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
            {
                thrown = Record.Exception(() => Run(owned, () => { }, _ => throw new InvalidOperationException("a pass reports nothing")));
            }
            IOException failure = Assert.IsType<IOException>(thrown);
            Assert.Contains(store, failure.Message, StringComparison.Ordinal);
            Assert.Contains("root kept at " + owned.Path, failure.Message, StringComparison.Ordinal);
            Assert.True(File.Exists(store));
        }
        finally { Remove(parent); }
    }

    /// <summary>
    /// P-SHUTDOWN-1: a cleanup failure after a pass is reported as a cleanup failure naming the kept root, never swallowed and never
    /// confused with a scenario failure; nothing is retried.
    /// </summary>
    [Fact]
    public void ReportsACleanupFailureAfterAPassAsItsOwnFailure()
    {
        string parent = Parent();
        string locked = "";
        try
        {
            OwnedScenarioRoot owned = new(parent, "case-");
            locked = Directory.CreateDirectory(Path.Combine(owned.Path, "run-1")).FullName;
            File.WriteAllText(Path.Combine(locked, "state"), "state");
            // The deletion is refused by the platform: a read-only directory on Unix, an exclusively open file on Windows.
            FileStream? exclusive = null;
            if (OperatingSystem.IsWindows()) { exclusive = new(Path.Combine(locked, "state"), FileMode.Open, FileAccess.Read, FileShare.None); }
            else { File.SetUnixFileMode(locked, UnixFileMode.UserRead | UnixFileMode.UserExecute); }
            Exception? thrown;
            using (exclusive) { thrown = Record.Exception(() => Run(owned, () => { }, _ => throw new InvalidOperationException("a pass reports nothing"))); }
            IOException failure = Assert.IsType<IOException>(thrown);
            Assert.StartsWith("Native scenario cleanup failed after a pass", failure.Message, StringComparison.Ordinal);
            Assert.Contains("root kept at " + owned.Path, failure.Message, StringComparison.Ordinal);
            Assert.NotNull(failure.InnerException);
            Assert.True(File.Exists(Path.Combine(locked, "state")));
        }
        finally
        {
            if (!OperatingSystem.IsWindows() && locked.Length > 0 && Directory.Exists(locked))
            {
                File.SetUnixFileMode(locked, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
            Remove(parent);
        }
    }

    // The lifecycle shape of NativeScenarioOwnershipTests.ReleasesNativeStateAfterConcreteScenario.
    private static void Run(OwnedScenarioRoot owned, Action body, Action<string> report)
    {
        try
        {
            body();
            owned.Passed();
        }
        catch (Exception primary)
        {
            owned.Report(primary, report);
            throw;
        }
        finally { owned.Finish(); }
    }

    // A private parent for one test case, under the same scratch area the native test uses.
    private static string Parent()
        => Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "dev", ".agenttemp",
            "multisession-owned-root-test-" + Guid.NewGuid().ToString("N"))).FullName;

    // Removes the case's own parent; a parent already removed by a faulty deletion fails the case through its assertions.
    private static void Remove(string parent)
    {
        if (Directory.Exists(parent)) { Directory.Delete(parent, recursive: true); }
    }
}
