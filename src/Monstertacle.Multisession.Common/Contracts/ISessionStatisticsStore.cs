namespace Monstertacle.Multisession.Contracts;

/// <summary>Content-free local diagnostics, independent of execution decisions.</summary>
public interface ISessionStatisticsStore
{
    /// <summary>Reads queue and reservation counts in one consistent snapshot without changing state.</summary>
    ValueTask<RuntimeStatistics> GetRuntimeStatisticsAsync(CancellationToken cancellationToken);
}
