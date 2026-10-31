using System.Diagnostics;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Diagnostics;
using Xunit;

namespace Monstertacle.Multisession.UnitTests;

/// <summary>Diagnostic sinks receive only explicit, content-free correlation data.</summary>
public sealed class DiagnosticsPrivacyTests
{
    /// <summary>Stored outcome reporting excludes all provider contents.</summary>
    [Fact]
    public void ConfirmedEventsExposeOnlyCorrelationAndFiniteOutcomeData()
    {
        CaptureLogger logger = new();
        using SessionDiagnostics diagnostics = new(logger);
        Guid sessionId = Guid.NewGuid();
        Guid requestId = Guid.NewGuid();
        Guid dispatchId = Guid.NewGuid();
        diagnostics.RecordAccepted(sessionId, new(requestId, 42), TimeSpan.FromMilliseconds(25));
        diagnostics.RecordCompleted(new(dispatchId, requestId, sessionId, "SECRET-WORKSPACE", DateTimeOffset.UtcNow),
            new(RequestState.Failed, ExecutionCertainty.Unknown, "SECRET-PROMPT-OUTPUT", false,
                new("SECRET-ADAPTER", 1, "SECRET-TOKEN", "SECRET-HEAD"), new(ErrorCode.AuthenticationFailed)), TimeSpan.FromSeconds(2));
        Assert.Equal(2, logger.Events.Count);
        Dictionary<string, object?> accepted = logger.Events[0];
        Dictionary<string, object?> completed = logger.Events[1];
        Assert.Equal(diagnostics.InstanceId, accepted["instance.id"]);
        Assert.Equal(sessionId, completed["session.id"]);
        Assert.Equal(requestId, completed["request.id"]);
        Assert.Equal(dispatchId, completed["dispatch.id"]);
        Assert.Equal("Failed", completed["request.state"]);
        Assert.Equal("Unknown", completed["execution.certainty"]);
        Assert.Equal("AuthenticationFailed", completed["error.code"]);
        Assert.Equal(2d, completed["duration.seconds"]);
        Assert.DoesNotContain("SECRET", string.Join('|', logger.Events.SelectMany(item => item.Values)));
        Assert.All(logger.Exceptions, Assert.Null);
    }

    /// <summary>Tracing identifies local work without native identifiers.</summary>
    [Fact]
    public void ActivitiesCarryLocalCorrelationWithoutNativeContents()
    {
        List<Activity> activities = [];
        using SessionDiagnostics diagnostics = new();
        using ActivityListener listener = new()
        {
            ShouldListenTo = source => source.Name == SessionDiagnostics.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => options.Tags!.Any(item => item.Key == "instance.id" && Equals(item.Value, diagnostics.InstanceId))
                ? ActivitySamplingResult.AllData : ActivitySamplingResult.None,
            ActivityStopped = activities.Add
        };
        ActivitySource.AddActivityListener(listener);
        Guid session = Guid.NewGuid();
        Guid request = Guid.NewGuid();
        diagnostics.RecordAccepted(session, new(request, 1), TimeSpan.Zero);
        Activity activity = Assert.Single(activities, item => Equals(item.GetTagItem("instance.id"), diagnostics.InstanceId));
        Assert.Equal("request.accepted", activity.OperationName);
        Assert.Equal(session, activity.GetTagItem("session.id"));
        Assert.Equal(request, activity.GetTagItem("request.id"));
        Assert.Equal("Queued", activity.GetTagItem("request.state"));
        Assert.Equal("NotStarted", activity.GetTagItem("execution.certainty"));
    }

    /// <summary>Logger callbacks cannot affect accepted work.</summary>
    [Fact]
    public void FailingLoggerIsContainedAndCounted()
    {
        using SessionDiagnostics diagnostics = new(new CaptureLogger { Throw = true });
        diagnostics.RecordAccepted(Guid.NewGuid(), new(Guid.NewGuid(), 1), TimeSpan.Zero);
        Assert.Equal(1, diagnostics.FailureCount);
    }

    /// <summary>A broken trace listener leaves the independent logging sink available.</summary>
    [Fact]
    public void ListenerFailureDoesNotSuppressLogging()
    {
        CaptureLogger logger = new();
        using SessionDiagnostics diagnostics = new(logger);
        using ActivityListener listener = new()
        {
            ShouldListenTo = source => source.Name == SessionDiagnostics.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => options.Tags!.Any(item => item.Key == "instance.id" && Equals(item.Value, diagnostics.InstanceId))
                ? throw new InvalidOperationException("SECRET-LISTENER") : ActivitySamplingResult.None
        };
        ActivitySource.AddActivityListener(listener);
        diagnostics.RecordAccepted(Guid.NewGuid(), new(Guid.NewGuid(), 1), TimeSpan.Zero);
        Assert.Equal(1, diagnostics.FailureCount);
        Assert.Single(logger.Events);
    }

    /// <summary>Undefined codes never become unbounded telemetry labels.</summary>
    [Fact]
    public void UndefinedEnumsAreNormalizedAndDisposedSourcesAreSilent()
    {
        CaptureLogger logger = new();
        using SessionDiagnostics diagnostics = new(logger);
        ExecutionReservation reservation = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow);
        ProviderReply reply = new((RequestState)123456, (ExecutionCertainty)123456, "SECRET", false, null, new((ErrorCode)123456));
        diagnostics.RecordCompleted(reservation, reply, TimeSpan.FromSeconds(-1));
        Dictionary<string, object?> entry = Assert.Single(logger.Events);
        Assert.Equal("Unknown", entry["request.state"]);
        Assert.Equal("Unknown", entry["execution.certainty"]);
        Assert.Equal("Unknown", entry["error.code"]);
        Assert.Equal(0d, entry["duration.seconds"]);
        diagnostics.Dispose();
        diagnostics.RecordCompleted(reservation, reply, TimeSpan.Zero);
        Assert.Single(logger.Events);
    }

}
