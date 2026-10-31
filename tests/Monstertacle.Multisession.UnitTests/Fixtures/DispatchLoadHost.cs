using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Requests;
using Monstertacle.Multisession.Sessions;
using Xunit;

namespace Monstertacle.Multisession.UnitTests.Fixtures;

internal sealed record DispatchLoadHost(SessionCatalog Catalog, RequestAdmission Admission, RequestDispatcher Dispatcher)
{
    public static ControlledProvider Provider(Task release) => new()
    {
        BindingForContext = context => new("controlled", 1, $"native-{context.SessionId:D}", null),
        Sending = async (call, binding, observe, token) =>
        {
            Assert.Equal($"native-{call.Session.SessionId:D}", binding.SessionReference);
            await release.WaitAsync(token);
            return DispatchScenario.Success(binding with { ContinuationState = call.Input }) with { Output = call.Input };
        },
    };

    public static DispatchLoadHost Create(InMemorySessionStore store, ControlledProvider first, ControlledProvider second, RuntimeOptions options)
    {
        ProviderRegistration[] registrations = [new("first", "controlled", 1, first), new("second", "controlled", 1, second)];
        AgentProfile[] profiles = [new("one", "first", "model-one", ExecutionKind.AgentTask, new("one", new Dictionary<string, string>())),
            new("two", "second", "model-two", ExecutionKind.AgentTask, new("two", new Dictionary<string, string>()))];
        return new(new(store, registrations, profiles), new(store, options), new(store, registrations, options));
    }
}
