using System.Diagnostics;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Diagnostics;

// Captures a monotonic start after reservation; only confirmed store writes report completion.
internal sealed class DispatchDiagnostics(SessionDiagnostics? diagnostics, ExecutionReservation reservation)
{
    private readonly long _started = Stopwatch.GetTimestamp();
    internal void Complete(ProviderReply reply) => diagnostics?.RecordCompleted(reservation, reply, Stopwatch.GetElapsedTime(_started));
}
