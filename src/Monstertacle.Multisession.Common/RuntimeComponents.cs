using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Cqrs;
using Monstertacle.Multisession.Diagnostics;
using Monstertacle.Multisession.Requests;

namespace Monstertacle.Multisession;

internal sealed record RuntimeComponents(RuntimeFacades Facades, RuntimeDispatch Dispatch,
    RequestDispatcher Dispatcher, RuntimeOptions Options, RuntimeStatisticsMonitor Statistics);
