using Monstertacle.Multisession.Contracts;
using Monstertacle.SharpQrs;

namespace Monstertacle.Multisession.Cqrs;

/// <summary>Lists unresolved recovery cases.</summary>
/// <param name="PageSize">The PageSize argument of the corresponding runtime operation.</param>
/// <param name="Cursor">The Cursor argument of the corresponding runtime operation.</param>
public sealed record ListRecoveryCasesQuery(int PageSize, string? Cursor = null) : IQuery<Page<RecoveryCaseSnapshot>>;
