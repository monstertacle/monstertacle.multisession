using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Requests;
using Xunit;

namespace Monstertacle.Multisession.UnitTests;

/// <summary>Exact input identity for recovery comparison without resubmission.</summary>
public sealed class RequestSubmissionFingerprintTests
{
    private static SubmitRequest Input() => new(Guid.Parse("73ea0104-fd03-465b-a163-6fe2df346919"), "original input", TimeSpan.FromSeconds(5), null, false);

    /// <summary>A value-preserving copy remains identifiable with a full SHA256 representation.</summary>
    [Fact]
    public void IdenticalValueHasStableIdentity()
    {
        SubmitRequest request = Input();
        string fingerprint = RequestSubmissionFingerprint.Compute(request);
        Assert.Equal(64, fingerprint.Length);
        Assert.Equal(fingerprint, RequestSubmissionFingerprint.Compute(request with { }));
    }

    /// <summary>Every immutable field participates, including nullable text and exact Unicode.</summary>
    [Theory]
    [InlineData("session")]
    [InlineData("input")]
    [InlineData("timeout")]
    [InlineData("workspace")]
    [InlineData("empty-workspace")]
    [InlineData("write")]
    public void ChangedFieldCannotMatchOriginal(string field)
    {
        SubmitRequest original = Input();
        SubmitRequest changed = field switch
        {
            "session" => original with { SessionId = Guid.NewGuid() },
            "input" => original with { Input = "changed input" },
            "timeout" => original with { Timeout = original.Timeout + TimeSpan.FromTicks(1) },
            "workspace" => original with { WorkspaceKey = "workspace" },
            "empty-workspace" => original with { WorkspaceKey = string.Empty },
            "write" => original with { WriteIntent = true },
            _ => throw new InvalidOperationException(),
        };
        Assert.NotEqual(RequestSubmissionFingerprint.Compute(original), RequestSubmissionFingerprint.Compute(changed));
    }

    /// <summary>No text normalization silently equates different accepted byte representations.</summary>
    [Fact]
    public void DifferentUnicodeRepresentationsRemainDifferent()
    {
        SubmitRequest request = Input();
        Assert.NotEqual(RequestSubmissionFingerprint.Compute(request with { Input = "\u00e9" }),
            RequestSubmissionFingerprint.Compute(request with { Input = "e\u0301" }));
    }
}
