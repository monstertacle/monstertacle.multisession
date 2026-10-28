using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Sessions;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.UnitTests;

/// <summary>Checks confirmed native opening and fail-closed handling of an uncertain open.</summary>
public sealed class SessionOpeningTests
{
    private static SessionCatalog Catalog(InMemorySessionStore store, ControlledProvider provider)
        => new(store,
            [new ProviderRegistration("connection", "controlled", 1, provider)],
            [new AgentProfile("profile", "connection", "opaque-model", ExecutionKind.AgentTask, new("revision", new Dictionary<string, string>()))]);

    /// <summary>Keeps opaque native identity distinct from the local UUID and confirms opening only after storage.</summary>
    [Fact]
    public async Task MarksConfirmedOpeningAsOpen()
    {
        InMemorySessionStore store = new();
        ControlledProvider provider = new() { OpeningBinding = new("controlled", 1, "ses_pinned_native", "confirmed-head") };
        SessionCatalog catalog = Catalog(store, provider);

        SessionSnapshot created = await catalog.CreateSessionAsync("profile", new(Path.GetTempPath(), "workspace"), CancellationToken.None);

        Assert.Equal(SessionLifecycle.Open, created.Lifecycle);
        Assert.Equal(SessionBlockReason.None, created.BlockReason);
        Assert.Equal("ses_pinned_native", created.Binding?.SessionReference);
        Assert.Equal("confirmed-head", created.Binding?.ContinuationState);
        Assert.NotEqual(created.SessionId.ToString(), created.Binding?.SessionReference);
        Assert.Equal(created, await catalog.GetSessionAsync(created.SessionId, CancellationToken.None));
    }

    /// <summary>Does not lose the local identity or expose provider exception text after a possibly accepted open.</summary>
    [Fact]
    public async Task PreservesUncertainOpeningWithoutLeakingProviderText()
    {
        InMemorySessionStore store = new();
        Guid opened = Guid.Empty;
        ControlledProvider provider = new()
        {
            Opening = context =>
            {
                opened = context.SessionId;
                throw new InvalidOperationException("untrusted-provider-secret");
            },
        };
        SessionCatalog catalog = Catalog(store, provider);

        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(
            () => catalog.CreateSessionAsync("profile", new(Path.GetTempPath(), "workspace"), CancellationToken.None).AsTask());

        Assert.Equal(ErrorCode.ProviderUnavailable, failure.Failure.Code);
        Assert.Equal(ExecutionCertainty.Unknown, failure.Failure.Certainty);
        Assert.Equal(opened, failure.Failure.SessionId);
        Assert.DoesNotContain("untrusted-provider-secret", failure.ToString(), StringComparison.Ordinal);
        SessionSnapshot? saved = await catalog.GetSessionAsync(opened, CancellationToken.None);
        Assert.NotNull(saved);
        Assert.Equal(SessionLifecycle.Opening, saved.Lifecycle);
        Assert.Equal(SessionBlockReason.RecoveryRequired, saved.BlockReason);
    }

    /// <summary>Refuses adapter identity or binding formats that differ from the explicit registration.</summary>
    [Theory]
    [InlineData("another-adapter", 1)]
    [InlineData("controlled", 2)]
    public async Task RejectsMismatchedNativeBinding(string adapterId, int formatVersion)
    {
        InMemorySessionStore store = new();
        ControlledProvider provider = new() { OpeningBinding = new(adapterId, formatVersion, "opaque-native", null) };
        SessionCatalog catalog = Catalog(store, provider);

        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(
            () => catalog.CreateSessionAsync("profile", new(Path.GetTempPath(), "workspace"), CancellationToken.None).AsTask());

        Assert.Equal(ErrorCode.ProviderUnavailable, failure.Failure.Code);
        Assert.Equal(ExecutionCertainty.Unknown, failure.Failure.Certainty);
        Assert.NotNull(failure.Failure.SessionId);
        SessionSnapshot? saved = await catalog.GetSessionAsync(failure.Failure.SessionId.Value, CancellationToken.None);
        Assert.Equal(SessionBlockReason.RecoveryRequired, saved?.BlockReason);
        Assert.Null(saved?.Binding);
    }

    /// <summary>Records uncertainty despite caller cancellation instead of mistaking cancellation for a safe rollback.</summary>
    [Fact]
    public async Task PersistsUncertaintyWhenOpeningIsCancelled()
    {
        InMemorySessionStore store = new();
        using CancellationTokenSource cancellation = new();
        ControlledProvider provider = new()
        {
            Opening = _ =>
            {
                cancellation.Cancel();
                throw new OperationCanceledException(cancellation.Token);
            },
        };
        SessionCatalog catalog = Catalog(store, provider);

        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(
            () => catalog.CreateSessionAsync("profile", new(Path.GetTempPath(), "workspace"), cancellation.Token).AsTask());

        Assert.Equal(ErrorCode.Cancelled, failure.Failure.Code);
        Assert.Equal(ExecutionCertainty.Unknown, failure.Failure.Certainty);
        Assert.NotNull(failure.Failure.SessionId);
        SessionSnapshot? saved = await catalog.GetSessionAsync(failure.Failure.SessionId.Value, CancellationToken.None);
        Assert.Equal(SessionBlockReason.RecoveryRequired, saved?.BlockReason);
    }

    /// <summary>Does not publish a successful native binding when its durable commit failed.</summary>
    [Fact]
    public async Task BlocksWhenBindingCannotBeStored()
    {
        InMemorySessionStore store = new() { CommittingBinding = () => throw new IOException("storage-secret") };
        SessionCatalog catalog = Catalog(store, new ControlledProvider());

        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(
            () => catalog.CreateSessionAsync("profile", new(Path.GetTempPath(), "workspace"), CancellationToken.None).AsTask());

        Assert.Equal(ErrorCode.StorageUnavailable, failure.Failure.Code);
        Assert.Equal(ExecutionCertainty.Unknown, failure.Failure.Certainty);
        Assert.DoesNotContain("storage-secret", failure.ToString(), StringComparison.Ordinal);
        Assert.NotNull(failure.Failure.SessionId);
        SessionSnapshot? saved = await catalog.GetSessionAsync(failure.Failure.SessionId.Value, CancellationToken.None);
        Assert.Equal(SessionLifecycle.Opening, saved?.Lifecycle);
        Assert.Equal(SessionBlockReason.RecoveryRequired, saved?.BlockReason);
    }

    /// <summary>Reports a failed recovery write instead of claiming the block was durably recorded.</summary>
    [Fact]
    public async Task ReportsStorageFailureWhenBlockingAlsoFails()
    {
        InMemorySessionStore store = new() { BlockingSession = () => throw new IOException("storage-secret") };
        ControlledProvider provider = new() { Opening = _ => throw new IOException("provider-secret") };
        SessionCatalog catalog = Catalog(store, provider);

        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(
            () => catalog.CreateSessionAsync("profile", new(Path.GetTempPath(), "workspace"), CancellationToken.None).AsTask());

        Assert.Equal(ErrorCode.StorageUnavailable, failure.Failure.Code);
        Assert.Equal(ExecutionCertainty.Unknown, failure.Failure.Certainty);
        Assert.NotNull(failure.Failure.SessionId);
        Assert.DoesNotContain("secret", failure.ToString(), StringComparison.Ordinal);
        SessionSnapshot? saved = await catalog.GetSessionAsync(failure.Failure.SessionId.Value, CancellationToken.None);
        Assert.Equal(SessionLifecycle.Opening, saved?.Lifecycle);
        Assert.Equal(SessionBlockReason.None, saved?.BlockReason);
    }
}
