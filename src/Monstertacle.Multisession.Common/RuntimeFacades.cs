using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession;

internal sealed record RuntimeFacades(ISessions Sessions, IRequests Requests, IRecovery Recovery);
