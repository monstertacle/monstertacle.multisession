using Monstertacle.Multisession.Contracts;
using Xunit;

namespace Monstertacle.Multisession.UnitTests;

/// <summary>Behavior tests for deterministic runtime option validation, without filesystem access.</summary>
public sealed class RuntimeOptionsTests
{
    /// <summary>Provides a fully specified valid baseline without relying on defaults.</summary>
    internal static RuntimeOptions ValidOptions() => new()
    {
        StateDirectory = Path.Combine(Path.GetTempPath(), "multisession-validation"),
        MaxConcurrentRequests = 4,
        MaxQueuedRequests = 100,
        MaxInputBytes = 1_048_576,
        MaxOutputBytes = 4_194_304,
        DbWorkQueueCapacity = 256,
        AdmissionTimeout = TimeSpan.FromSeconds(5),
        RequestTimeout = TimeSpan.FromSeconds(600),
        ShutdownTimeout = TimeSpan.FromSeconds(10),
        DbBusyTimeout = TimeSpan.FromSeconds(1),
        Retention = new RetentionPolicy(true, null),
    };

    /// <summary>Supplies each documented capacity default.</summary>
    [Theory]
    [InlineData(nameof(RuntimeOptions.MaxConcurrentRequests), 4)]
    [InlineData(nameof(RuntimeOptions.MaxQueuedRequests), 100)]
    [InlineData(nameof(RuntimeOptions.MaxInputBytes), 1_048_576)]
    [InlineData(nameof(RuntimeOptions.MaxOutputBytes), 4_194_304)]
    [InlineData(nameof(RuntimeOptions.DbWorkQueueCapacity), 256)]
    public void SuppliesCapacityDefault(string propertyName, int expected)
    {
        RuntimeOptions options = new() { StateDirectory = Path.GetTempPath() };

        object? actual = typeof(RuntimeOptions).GetProperty(propertyName)!.GetValue(options);

        Assert.Equal(expected, Assert.IsType<int>(actual));
    }

    /// <summary>Supplies each documented deadline default.</summary>
    [Theory]
    [InlineData(nameof(RuntimeOptions.AdmissionTimeout), 5)]
    [InlineData(nameof(RuntimeOptions.RequestTimeout), 600)]
    [InlineData(nameof(RuntimeOptions.ShutdownTimeout), 10)]
    [InlineData(nameof(RuntimeOptions.DbBusyTimeout), 1)]
    public void SuppliesDeadlineDefault(string propertyName, int seconds)
    {
        RuntimeOptions options = new() { StateDirectory = Path.GetTempPath() };

        object? actual = typeof(RuntimeOptions).GetProperty(propertyName)!.GetValue(options);

        Assert.Equal(TimeSpan.FromSeconds(seconds), Assert.IsType<TimeSpan>(actual));
    }

    /// <summary>Does not silently authorize deletion through a default retention policy.</summary>
    [Fact]
    public void LeavesRetentionExplicit()
    {
        RuntimeOptions options = new() { StateDirectory = Path.GetTempPath() };

        Assert.Null(options.Retention);
    }

    /// <summary>Rejects absent, relative and malformed state paths before runtime start.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("relative/state")]
    [InlineData("bad\0path")]
    public void RejectsInvalidStateDirectory(string? stateDirectory)
    {
        RuntimeOptions options = ValidOptions() with { StateDirectory = stateDirectory! };

        PublicError? failure = options.Validate();

        Assert.NotNull(failure);
        Assert.Equal(ErrorCode.InvalidConfiguration, failure.Code);
        Assert.Equal(ExecutionCertainty.NotStarted, failure.Certainty);
    }

    /// <summary>Rejects nonpositive capacities without relying on another invalid option.</summary>
    [Theory]
    [InlineData(nameof(RuntimeOptions.MaxConcurrentRequests), 0)]
    [InlineData(nameof(RuntimeOptions.MaxConcurrentRequests), -1)]
    [InlineData(nameof(RuntimeOptions.MaxQueuedRequests), 0)]
    [InlineData(nameof(RuntimeOptions.MaxQueuedRequests), -1)]
    [InlineData(nameof(RuntimeOptions.MaxInputBytes), 0)]
    [InlineData(nameof(RuntimeOptions.MaxInputBytes), -1)]
    [InlineData(nameof(RuntimeOptions.MaxOutputBytes), 0)]
    [InlineData(nameof(RuntimeOptions.MaxOutputBytes), -1)]
    [InlineData(nameof(RuntimeOptions.DbWorkQueueCapacity), 0)]
    [InlineData(nameof(RuntimeOptions.DbWorkQueueCapacity), -1)]
    public void RejectsNonpositiveCapacity(string propertyName, int value)
    {
        RuntimeOptions options = ValidOptions();
        typeof(RuntimeOptions).GetProperty(propertyName)!.SetValue(options, value);

        Assert.Equal(ErrorCode.InvalidConfiguration, options.Validate()?.Code);
    }

    /// <summary>Rejects zero, infinite, negative and unschedulable execution deadlines.</summary>
    [Theory]
    [InlineData(nameof(RuntimeOptions.AdmissionTimeout), 0L)]
    [InlineData(nameof(RuntimeOptions.AdmissionTimeout), -10_000L)]
    [InlineData(nameof(RuntimeOptions.AdmissionTimeout), -20_000L)]
    [InlineData(nameof(RuntimeOptions.AdmissionTimeout), long.MaxValue)]
    [InlineData(nameof(RuntimeOptions.RequestTimeout), 0L)]
    [InlineData(nameof(RuntimeOptions.RequestTimeout), -10_000L)]
    [InlineData(nameof(RuntimeOptions.RequestTimeout), -20_000L)]
    [InlineData(nameof(RuntimeOptions.RequestTimeout), long.MaxValue)]
    [InlineData(nameof(RuntimeOptions.ShutdownTimeout), 0L)]
    [InlineData(nameof(RuntimeOptions.ShutdownTimeout), -10_000L)]
    [InlineData(nameof(RuntimeOptions.ShutdownTimeout), -20_000L)]
    [InlineData(nameof(RuntimeOptions.ShutdownTimeout), long.MaxValue)]
    [InlineData(nameof(RuntimeOptions.DbBusyTimeout), 0L)]
    [InlineData(nameof(RuntimeOptions.DbBusyTimeout), -10_000L)]
    [InlineData(nameof(RuntimeOptions.DbBusyTimeout), -20_000L)]
    [InlineData(nameof(RuntimeOptions.DbBusyTimeout), long.MaxValue)]
    public void RejectsInvalidDeadline(string propertyName, long ticks)
    {
        RuntimeOptions options = ValidOptions();
        typeof(RuntimeOptions).GetProperty(propertyName)!.SetValue(options, TimeSpan.FromTicks(ticks));

        Assert.Equal(ErrorCode.InvalidConfiguration, options.Validate()?.Code);
    }

    /// <summary>Keeps database blocking strictly below the controlled stop deadline.</summary>
    [Theory]
    [InlineData(10)]
    [InlineData(11)]
    public void RejectsBusyTimeoutAtOrAboveShutdown(int seconds)
    {
        RuntimeOptions options = ValidOptions() with { DbBusyTimeout = TimeSpan.FromSeconds(seconds) };

        Assert.Equal(ErrorCode.InvalidConfiguration, options.Validate()?.Code);
    }

    /// <summary>Requires one explicit, consistent retention choice without implicit content deletion.</summary>
    [Theory]
    [InlineData(null, null)]
    [InlineData(true, 1L)]
    [InlineData(false, null)]
    [InlineData(false, 0L)]
    [InlineData(false, -1L)]
    [InlineData(false, long.MaxValue)]
    public void RejectsInvalidRetention(bool? explicitDeletion, long? ticks)
    {
        RetentionPolicy? retention = explicitDeletion is null
            ? null
            : new RetentionPolicy(explicitDeletion.Value, ticks is null ? null : TimeSpan.FromTicks(ticks.Value));
        RuntimeOptions options = ValidOptions() with { Retention = retention };

        Assert.Equal(ErrorCode.InvalidConfiguration, options.Validate()?.Code);
    }

    /// <summary>Rejects syntactically identifiable network paths without claiming native path validation.</summary>
    [Theory]
    [InlineData(@"\\server\share\state")]
    [InlineData("//server/share/state")]
    public void RejectsNetworkStateDirectory(string path)
    {
        RuntimeOptions options = ValidOptions() with { StateDirectory = path };

        Assert.Equal(ErrorCode.InvalidConfiguration, options.Validate()?.Code);
    }

    /// <summary>Accepts positive inclusive boundary values and either explicit retention mode.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AcceptsValidBoundaryOptions(bool explicitDeletion)
    {
        RuntimeOptions options = ValidOptions() with
        {
            MaxConcurrentRequests = 1,
            MaxQueuedRequests = 1,
            MaxInputBytes = 1,
            MaxOutputBytes = 1,
            DbWorkQueueCapacity = 1,
            AdmissionTimeout = TimeSpan.FromTicks(1),
            RequestTimeout = TimeSpan.FromTicks(1),
            ShutdownTimeout = TimeSpan.FromTicks(2),
            DbBusyTimeout = TimeSpan.FromTicks(1),
            Retention = new RetentionPolicy(explicitDeletion, explicitDeletion ? null : TimeSpan.FromTicks(1)),
        };

        Assert.Null(options.Validate());
    }
}
