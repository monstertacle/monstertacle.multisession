using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Requests;
using Monstertacle.Multisession.UnitTests.Fixtures;
using Xunit;

namespace Monstertacle.Multisession.UnitTests;

/// <summary>Malformed UTF16 must never be silently replaced at a UTF8 persistence or provider boundary.</summary>
public sealed class Utf8IntegrityTests
{
    /// <summary>Unpaired surrogates are rejected before an acceptance attempt or external send.</summary>
    [Theory]
    [InlineData(0xD800)]
    [InlineData(0xDC00)]
    public async Task InvalidInputIsNotAcceptedOrSent(int codepoint)
    {
        DispatchScenario scenario = await DispatchScenario.CreateAsync();
        string malformed = Malformed(codepoint);
        Assert.Equal(ErrorCode.InvalidInput, Utf8MessageLimits.ValidateInput(malformed, 1024)?.Code);
        SessionOperationException failure = await Assert.ThrowsAsync<SessionOperationException>(() => scenario.SubmitAsync(malformed).AsTask());
        Assert.Equal(ErrorCode.InvalidInput, failure.Failure.Code);
        Assert.Equal(AdmissionStatus.NotAccepted, failure.Failure.AdmissionStatus);
        Assert.Empty(scenario.Store.Submissions);
        Assert.False(scenario.Provider.Started.Task.IsCompleted);
        Assert.DoesNotContain("SECRET", failure.ToString(), StringComparison.Ordinal);
    }

    /// <summary>Invalid output is removed without changing the independently established execution certainty.</summary>
    [Theory]
    [InlineData(0xD800, ExecutionCertainty.NotStarted)]
    [InlineData(0xD800, ExecutionCertainty.ConfirmedEnded)]
    [InlineData(0xD800, ExecutionCertainty.Unknown)]
    [InlineData(0xDC00, ExecutionCertainty.NotStarted)]
    [InlineData(0xDC00, ExecutionCertainty.ConfirmedEnded)]
    [InlineData(0xDC00, ExecutionCertainty.Unknown)]
    public void InvalidFailedOutputKeepsCertaintyAndCannotEscape(int codepoint, ExecutionCertainty certainty)
    {
        ProviderReply reply = new(RequestState.Failed, certainty, Malformed(codepoint), true, null, new(ErrorCode.AuthenticationFailed, certainty));
        Assert.Equal(new(ErrorCode.ExecutionFailed, certainty), Utf8MessageLimits.ValidateOutput(reply.Output, 1024, certainty));
        ProviderReply normalized = ProviderReplyPolicy.NormalizeOutput(reply, 1024);
        Assert.Empty(normalized.Output);
        Assert.False(normalized.OutputComplete);
        Assert.Equal(certainty, normalized.Certainty);
        Assert.Equal(ErrorCode.ExecutionFailed, normalized.Error?.Code);
    }

    /// <summary>A confirmed external end with malformed output produces a stored failure, never a replaced success.</summary>
    [Theory]
    [InlineData(0xD800)]
    [InlineData(0xDC00)]
    public async Task DispatcherNeverPublishesReplacedSuccess(int codepoint)
    {
        ControlledProvider provider = new()
        {
            OpeningBinding = new("controlled", 1, "native-session", null),
            Sending = (call, binding, observe, token) => ValueTask.FromResult(DispatchScenario.Success(binding) with { Output = Malformed(codepoint) })
        };
        DispatchScenario scenario = await DispatchScenario.CreateAsync(provider);
        RequestReceipt receipt = await scenario.SubmitAsync();
        Assert.True(await scenario.Dispatcher.TryDispatchAsync(CancellationToken.None));
        RequestSnapshot request = (await ((ISessionStore)scenario.Store).GetRequestAsync(receipt.RequestId, CancellationToken.None))!;
        Assert.Equal(RequestState.Failed, request.State);
        Assert.Equal(ExecutionCertainty.ConfirmedEnded, request.Certainty);
        Assert.Equal(ErrorCode.ExecutionFailed, request.Result?.Error?.Code);
        Assert.Empty(request.Result!.Output);
        Assert.Empty(scenario.Store.Reservations);
    }

    /// <summary>Valid surrogate pairs and whitespace retain their exact inclusive byte boundaries.</summary>
    [Fact]
    public void ValidUnicodeAndWhitespaceKeepExactByteLimits()
    {
        const string text = " 🙂\r\n";
        Assert.Null(Utf8MessageLimits.ValidateInput(text, 7));
        Assert.Equal(ErrorCode.InputLimitExceeded, Utf8MessageLimits.ValidateInput(text, 6)?.Code);
        Assert.Null(Utf8MessageLimits.ValidateOutput(text, 7, ExecutionCertainty.ConfirmedEnded));
        Assert.Equal(ErrorCode.OutputLimitExceeded, Utf8MessageLimits.ValidateOutput(text, 6, ExecutionCertainty.ConfirmedEnded)?.Code);
    }

    /// <summary>A genuine replacement scalar or emoji is valid user content and is never rejected heuristically.</summary>
    [Theory]
    [InlineData(0xFFFD, 3)]
    [InlineData(0x1F642, 4)]
    public void ValidScalarsRemainUnchanged(int scalar, int bytes)
    {
        string text = char.ConvertFromUtf32(scalar);
        Assert.Null(Utf8MessageLimits.ValidateInput(text, bytes));
        Assert.Equal(ErrorCode.InputLimitExceeded, Utf8MessageLimits.ValidateInput(text, bytes - 1)?.Code);
    }

    private static string Malformed(int codepoint) => "SECRET-" + (char)codepoint + "-TEXT";
}
