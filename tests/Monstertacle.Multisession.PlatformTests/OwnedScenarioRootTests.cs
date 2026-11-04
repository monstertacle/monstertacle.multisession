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
