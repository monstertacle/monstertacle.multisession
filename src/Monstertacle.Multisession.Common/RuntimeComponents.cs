using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Requests;

namespace Monstertacle.Multisession;

internal sealed record RuntimeComponents(ISessions Sessions, IRequests Requests, IRecovery Recovery,
    RequestDispatcher Dispatcher, RuntimeOptions Options);
