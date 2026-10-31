using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Requests;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.UnitTests;

/// <summary>Provider categories cannot impersonate local persistence or admission failures.</summary>
public sealed class ProviderFailurePolicyTests
{
    private static readonly ErrorCode[] _allowed = [ErrorCode.InvalidConfiguration, ErrorCode.InvalidInput, ErrorCode.InputLimitExceeded,
        ErrorCode.OutputLimitExceeded, ErrorCode.AuthenticationFailed, ErrorCode.ProviderRateLimited, ErrorCode.ProviderUnavailable,
        ErrorCode.ProviderSessionExpired, ErrorCode.ProviderContextLimit, ErrorCode.UnsupportedProviderAction, ErrorCode.ExecutionFailed,
        ErrorCode.Cancelled, ErrorCode.TimedOut, ErrorCode.Interrupted];

    /// <summary>All declared categories and an undefined category are covered explicitly.</summary>
    public static IEnumerable<object[]> Categories() => Enum.GetValues<ErrorCode>().Append((ErrorCode)123456).Select(code => new object[] { code, _allowed.Contains(code) });

    /// <summary>Only the public provider allowlist survives normalization.</summary>
    [Theory]
    [MemberData(nameof(Categories))]
    public void RejectsLocalOrUndefinedCategories(ErrorCode code, bool allowed)
    {
        ProviderBinding binding = new("controlled", 1, "native-session", null);
        ProviderReply reply = new(RequestState.Failed, ExecutionCertainty.ConfirmedEnded, string.Empty, false, binding, new(code));
        ProviderReply normalized = DispatchReplyPolicy.Normalize(reply, binding, null, 1024);
        Assert.Equal(allowed ? code : ErrorCode.ExecutionFailed, normalized.Error?.Code);
    }

    /// <summary>Provider-supplied local identities, admission status and contradictory certainty are removed.</summary>
    [Fact]
    public void DoesNotTrustProviderErrorMetadata()
    {
        ProviderBinding binding = new("controlled", 1, "native-session", null);
        ProviderReply reply = new(RequestState.Failed, ExecutionCertainty.ConfirmedEnded, string.Empty, false, binding,
            new(ErrorCode.AuthenticationFailed, ExecutionCertainty.NotStarted, Guid.NewGuid(), Guid.NewGuid(), AdmissionStatus.Committed));
        ProviderReply normalized = DispatchReplyPolicy.Normalize(reply, binding, null, 1024);
        Assert.Equal(new(ErrorCode.AuthenticationFailed, ExecutionCertainty.ConfirmedEnded), normalized.Error);
    }

    /// <summary>Typed exceptions retain a permitted category but do not prove external termination.</summary>
    [Theory]
    [InlineData(ErrorCode.AuthenticationFailed, ErrorCode.AuthenticationFailed)]
    [InlineData(ErrorCode.ProviderRateLimited, ErrorCode.ProviderRateLimited)]
    [InlineData(ErrorCode.ProviderContextLimit, ErrorCode.ProviderContextLimit)]
    [InlineData(ErrorCode.StorageUnavailable, ErrorCode.ExecutionFailed)]
    public async Task TypedExceptionsKeepOnlyAllowedCategoryAndRemainUnknown(ErrorCode supplied, ErrorCode expected)
    {
        ControlledProvider provider = new()
        {
            Sending = (call, binding, observe, token) => throw new SessionOperationException(new(supplied, ExecutionCertainty.NotStarted))
        };
        DispatchScenario scenario = await DispatchScenario.CreateAsync(provider);
        RequestReceipt receipt = await scenario.SubmitAsync();
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => scenario.Dispatcher.TryDispatchAsync(CancellationToken.None).AsTask());
        Assert.Equal(expected, failure.Failure.Code);
        RequestSnapshot snapshot = (await ((ISessionStore)scenario.Store).GetRequestAsync(receipt.RequestId, CancellationToken.None))!;
        Assert.Equal(expected, snapshot.Result?.Error?.Code);
        Assert.Equal(ExecutionCertainty.Unknown, snapshot.Certainty);
        Assert.Single(scenario.Store.Reservations);
    }
}
