using Monstertacle.SharpQrs;

namespace Monstertacle.Multisession.Cqrs;

internal sealed class RuntimeCommandDispatch(CommandDispatcher dispatcher, RuntimeLifecycle lifetime) : ICommandDispatcher
{
    public Task<TResult> DispatchAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default)
        => lifetime.TrackDispatch(() => dispatcher.DispatchAsync(command, cancellationToken));
}
