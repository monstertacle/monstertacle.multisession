using Monstertacle.Multisession.Contracts;
using Monstertacle.SharpQrs;

namespace Monstertacle.Multisession.Cqrs;

/// <summary>Reads optional current recovery evidence.</summary>
/// <param name="Target">The Target argument of the corresponding runtime operation.</param>
public sealed record GetRecoveryCaseQuery(RecoveryTarget Target) : IQuery<LookupResult<RecoveryCaseSnapshot>>;
