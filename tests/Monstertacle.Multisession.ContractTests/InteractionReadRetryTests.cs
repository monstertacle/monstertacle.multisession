using System.Net;
using System.Net.Http.Headers;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.ContractTests.Fixtures;
using Monstertacle.Multisession.Providers.Interactions;
using Xunit;
using static Monstertacle.Multisession.ContractTests.Fixtures.InteractionFixtureData;

namespace Monstertacle.Multisession.ContractTests;

/// <summary>Read-only retry delays cannot cause an execution POST to be repeated.</summary>
public sealed class InteractionReadRetryTests
{
    /// <summary>Safe GET retries honor native delta/date Retry-After within the overall operation deadline.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WaitsForRetryAfterOnSafeRead(bool asDate)
    {
        using HttpTestClock clock = new();
        int reads = 0;
        using ScriptedHttpHandler handler = new((request, token) =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            reads++;
            if (reads == 1)
            {
                HttpResponseMessage retry = ScriptedHttpHandler.Json("{}", HttpStatusCode.ServiceUnavailable);
                retry.Headers.RetryAfter = asDate ? new RetryConditionHeaderValue(clock.GetUtcNow().AddSeconds(2)) : new RetryConditionHeaderValue(TimeSpan.FromSeconds(2));
                return Task.FromResult(retry);
            }
            return Task.FromResult(ScriptedHttpHandler.Json(Json("new_completed")));
        });
        using InteractionSessionProvider provider = new(new() { Endpoint = new("https://fixture.example/v1/"), CredentialReference = "reference" }, new FixtureCredentials(), handler, clock);
        Task<ProviderReply> pending = provider.InspectAsync(Context(), "interaction_next", CancellationToken.None).AsTask();
        Task waiting = clock.WaitForTimerAsync(TimeSpan.FromSeconds(2));
        await Task.WhenAny(waiting, pending).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(pending.IsCompleted);
        await waiting.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, reads);
        clock.Fire(TimeSpan.FromSeconds(2));
        Assert.Equal(RequestState.Succeeded, (await pending.WaitAsync(TimeSpan.FromSeconds(5))).Outcome);
        Assert.Equal(2, reads);
    }

    /// <summary>Rate rejection of an execution is final for that send attempt even with Retry-After present.</summary>
    [Fact]
    public async Task NeverRetriesExecutionPostAfterRateRejection()
    {
        using ScriptedHttpHandler handler = new((request, token) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            HttpResponseMessage response = ScriptedHttpHandler.Json("{}", HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(1));
            return Task.FromResult(response);
        });
        using InteractionSessionProvider provider = new(new() { Endpoint = new("https://fixture.example/v1/"), CredentialReference = "reference" }, new FixtureCredentials(), handler);
        ProviderReply reply = await provider.SendAsync(Call(), Binding(), (item, token) => ValueTask.CompletedTask, CancellationToken.None);
        Assert.Equal(ErrorCode.ProviderRateLimited, reply.Error?.Code);
        Assert.Equal(ExecutionCertainty.NotStarted, reply.Certainty);
        Assert.Equal(1, handler.Calls);
    }
}
