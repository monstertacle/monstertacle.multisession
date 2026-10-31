using Monstertacle.SharpQrs;

namespace Monstertacle.Multisession.Cqrs;

internal sealed class RuntimeQueryDispatch(QueryDispatcher dispatcher, RuntimeLifecycle lifetime) : IQueryDispatcher
{
    public Task<TResult> DispatchAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default)
        => lifetime.TrackDispatch(() => dispatcher.DispatchAsync(query, cancellationToken));
}
