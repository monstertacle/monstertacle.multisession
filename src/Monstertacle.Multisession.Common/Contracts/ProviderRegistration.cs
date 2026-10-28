namespace Monstertacle.Multisession.Contracts;

/// <summary>An explicitly registered polymorphic execution adapter, not an HTTP-only connection.</summary>
/// <param name="ProviderId">Technical connection identity.</param>
/// <param name="AdapterId">Expected native binding adapter identity.</param>
/// <param name="ContractVersion">Supported adapter binding format.</param>
/// <param name="Provider">Host-owned adapter instance.</param>
public sealed record ProviderRegistration(string ProviderId, string AdapterId, int ContractVersion, ISessionProvider Provider);
