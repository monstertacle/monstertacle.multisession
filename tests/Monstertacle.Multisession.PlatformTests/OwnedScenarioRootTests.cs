using Monstertacle.Multisession.PlatformTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.PlatformTests;

/// <summary>A test-owned scenario root is removed after a pass, kept and reported after a failure, and nothing else is touched.</summary>
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
            owned.Passed();
            owned.Finish(reported.Add);
            Assert.False(Directory.Exists(owned.Path));
            Assert.True(Directory.Exists(sentinel));
            Assert.Empty(reported);
        }
        finally { Directory.Delete(parent, recursive: true); }
    }

    /// <summary>After a failure the root is kept as evidence and its path is reported once.</summary>
    [Fact]
    public void KeepsAndReportsItsRootAfterAFailure()
    {
        string parent = Parent();
        try
        {
            OwnedScenarioRoot owned = new(parent, "case-");
            Directory.CreateDirectory(owned.Path);
            List<string> reported = [];
            owned.Finish(reported.Add);
            Assert.True(Directory.Exists(owned.Path));
            Assert.Contains(owned.Path, Assert.Single(reported), StringComparison.Ordinal);
        }
        finally { Directory.Delete(parent, recursive: true); }
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
            owned.Passed();
            owned.Finish(_ => throw new InvalidOperationException("a pass reports nothing"));
            Assert.True(Directory.Exists(sentinel));
            Assert.Single(Directory.GetDirectories(parent));
        }
        finally { Directory.Delete(parent, recursive: true); }
    }

    // A private parent for one test case, under the same scratch area the native test uses.
    private static string Parent()
        => Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "dev", ".agenttemp",
            "multisession-owned-root-test-" + Guid.NewGuid().ToString("N"))).FullName;
}
