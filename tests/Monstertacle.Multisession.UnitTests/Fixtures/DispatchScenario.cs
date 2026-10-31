using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Requests;
using Monstertacle.Multisession.Sessions;

namespace Monstertacle.Multisession.UnitTests.Fixtures;

internal sealed record DispatchScenario(
    InMemorySessionStore Store,
    ControlledProvider Provider,
    SessionSnapshot Session,
    RuntimeOptions Options,
    RequestDispatcher Dispatcher)
{
    public static async Task<DispatchScenario> CreateAsync(
        ControlledProvider? provider = null, InMemorySessionStore? store = null, RuntimeOptions? options = null, TimeProvider? clock = null)
    {
        provider ??= new ControlledProvider { OpeningBinding = new("controlled", 1, "native-session", null), Sending = (call, binding, observe, token) => ValueTask.FromResult(Success(binding)) };
        store ??= new InMemorySessionStore();
        options ??= RuntimeOptionsTests.ValidOptions();
        ProviderRegistration[] registrations = [new("connection", "controlled", 1, provider)];
        AgentProfile[] profiles = [new("profile", "connection", "opaque-model", ExecutionKind.AgentTask,
            new("revision", new Dictionary<string, string> { ["setting"] = "frozen" }))];
        SessionCatalog catalog = new(store, registrations, profiles);
        SessionSnapshot session = await catalog.CreateSessionAsync("profile", new(Path.GetTempPath(), "workspace"), CancellationToken.None);
        return new(store, provider, session, options, new(store, registrations, options, clock));
    }

    public ValueTask<RequestReceipt> SubmitAsync(string input = "  only new input\nα🙂\r\n", string? writeKey = null)
        => new RequestAdmission(Store, Options).SubmitAsync(new(Session.SessionId, input, TimeSpan.FromSeconds(10), writeKey, writeKey is not null), CancellationToken.None);

    public static ProviderReply Success(ProviderBinding binding) => new(
        RequestState.Succeeded, ExecutionCertainty.ConfirmedEnded, "complete output", true, binding, null);
}
