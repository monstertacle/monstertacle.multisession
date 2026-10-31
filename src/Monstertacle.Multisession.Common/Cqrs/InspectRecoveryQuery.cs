using Monstertacle.Multisession.Contracts;
using Monstertacle.SharpQrs;

namespace Monstertacle.Multisession.Cqrs;

/// <summary>Inspects native evidence without changing local outcomes or reservations.</summary>
/// <param name="Target">The Target argument of the corresponding runtime operation.</param>
public sealed record InspectRecoveryQuery(RecoveryTarget Target) : IQuery<ProviderReply>;
