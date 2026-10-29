using System.Globalization;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.IntegrationTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.IntegrationTests;

/// <summary>Roundtrips opaque case-sensitive text and UTC state in actual persisted SQLite rows.</summary>
public sealed class SerializationCultureTests
{
    /// <summary>Host culture changes cannot reinterpret stored settings, IDs, timestamps or timeout.</summary>
    [Theory]
    [InlineData("de-DE", "tr-TR")]
    [InlineData("en-US", "de-DE")]
    [InlineData("tr-TR", "en-US")]
    public async Task PreservesFrozenValuesAndUtcAcrossCultureChange(string writeCulture, string readCulture)
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(writeCulture);
            await using AdmissionStoreFixture fixture = await AdmissionStoreFixture.CreateAsync();
            SessionSnapshot session = await fixture.CreateSessionAsync();
            RequestAcceptance acceptance = new(Guid.NewGuid(), new(session.SessionId, "  I i İ ı\n🙂\r\n", TimeSpan.FromMilliseconds(1234), "host-workspace", true));
            RequestReceipt receipt = await fixture.Store.CommitAcceptanceAsync(acceptance, 100, CancellationToken.None);
            RequestSnapshot initial = (await fixture.Store.GetRequestAsync(receipt.RequestId, CancellationToken.None))!;
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(readCulture);
            await fixture.ReopenAsync();
            SessionSnapshot saved = (await fixture.Store.GetSessionAsync(session.SessionId, CancellationToken.None))!;
            RequestSnapshot request = (await fixture.Store.GetRequestAsync(receipt.RequestId, CancellationToken.None))!;
            Assert.Equal("connection-I", saved.ProviderId);
            Assert.Equal("profile-İ", saved.AgentProfileId);
            Assert.Equal("model-I-İ", saved.Context.ModelId);
            Assert.Equal("revision-İ", saved.SettingsRevision);
            Assert.Equal("frozen-İ\n🙂", saved.Context.Settings["I"]);
            Assert.Equal("distinct", saved.Context.Settings["i"]);
            Assert.Equal(session.Binding, saved.Binding);
            Assert.Equal(7, saved.Binding?.FormatVersion);
            Assert.Equal(session.CreatedUtc, saved.CreatedUtc);
            Assert.Equal(initial.AcceptedUtc, saved.UpdatedUtc);
            Assert.Equal(initial.AcceptedUtc, request.AcceptedUtc);
            Assert.Equal(TimeSpan.Zero, saved.CreatedUtc.Offset);
            Assert.Equal(TimeSpan.Zero, request.AcceptedUtc.Offset);
            using System.Text.Json.JsonDocument json = System.Text.Json.JsonDocument.Parse((string)StoreProbe.Scalar(fixture.Directory.DatabasePath, "SELECT SubmissionJson FROM Requests")!);
            Assert.Equal(acceptance.Submission.Input, json.RootElement.GetProperty("Input").GetString());
            Assert.Equal("00:00:01.2340000", json.RootElement.GetProperty("Timeout").GetString());
        }
        finally { CultureInfo.CurrentCulture = original; }
    }
}
