using System.Collections.Frozen;
using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Sessions;

/// <summary>Validated immutable host configuration, using ordinal technical identities.</summary>
public sealed class ProviderCatalog
{
    private readonly FrozenDictionary<string, ProviderRegistration> _providers;
    private readonly FrozenDictionary<string, AgentProfile> _profiles;

    /// <summary>Validates explicit registrations and freezes nested non-secret profile values.</summary>
    public ProviderCatalog(IReadOnlyList<ProviderRegistration> providers, IReadOnlyList<AgentProfile> profiles)
    {
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(profiles);
        Dictionary<string, ProviderRegistration> registrations = new(StringComparer.Ordinal);
        foreach (ProviderRegistration item in providers)
        {
            if (item is null || string.IsNullOrWhiteSpace(item.ProviderId) || string.IsNullOrWhiteSpace(item.AdapterId)
                || item.ContractVersion < 1 || item.Provider is null || !registrations.TryAdd(item.ProviderId, item))
            {
                throw InvalidConfiguration();
            }
        }
        _providers = registrations.ToFrozenDictionary(StringComparer.Ordinal);
        Dictionary<string, AgentProfile> frozenProfiles = new(StringComparer.Ordinal);
        foreach (AgentProfile item in profiles)
        {
            ValidateProfile(item, registrations);
            AgentProfile frozen = item with { Settings = item.Settings with { Values = item.Settings.Values.ToFrozenDictionary(StringComparer.Ordinal) } };
            if (!frozenProfiles.TryAdd(item.AgentProfileId, frozen)) { throw InvalidConfiguration(); }
        }
        _profiles = frozenProfiles.ToFrozenDictionary(StringComparer.Ordinal);
        Providers = Array.AsReadOnly(registrations.Values.OrderBy(item => item.ProviderId, StringComparer.Ordinal).ToArray());
        Profiles = Array.AsReadOnly(frozenProfiles.Values.OrderBy(item => item.AgentProfileId, StringComparer.Ordinal).ToArray());
    }

    /// <summary>Gets stable read-only registrations; adapter lifetime remains host-owned.</summary>
    public IReadOnlyList<ProviderRegistration> Providers { get; }
    /// <summary>Gets stable read-only profiles including their independently frozen settings.</summary>
    public IReadOnlyList<AgentProfile> Profiles { get; }
    /// <summary>Finds an explicit profile, without fallback or inferred model selection.</summary>
    public AgentProfile GetProfile(string agentProfileId)
        => _profiles.TryGetValue(agentProfileId, out AgentProfile? profile) ? profile
            : throw new SessionOperationException(new PublicError(ErrorCode.NotFound));
    /// <summary>Finds the explicitly registered adapter connection.</summary>
    public ProviderRegistration GetProvider(string providerId)
        => _providers.TryGetValue(providerId, out ProviderRegistration? provider) ? provider : throw InvalidConfiguration();

    private static void ValidateProfile(AgentProfile item, Dictionary<string, ProviderRegistration> providers)
    {
        if (item is null || string.IsNullOrWhiteSpace(item.AgentProfileId) || string.IsNullOrWhiteSpace(item.ProviderId)
            || !providers.ContainsKey(item.ProviderId) || string.IsNullOrWhiteSpace(item.ModelId)
            || !Enum.IsDefined(item.ExecutionKind) || item.Settings is null
            || string.IsNullOrWhiteSpace(item.Settings.Revision) || item.Settings.Values is null)
        {
            throw InvalidConfiguration();
        }
    }
    private static SessionOperationException InvalidConfiguration() => new(new PublicError(ErrorCode.InvalidConfiguration));
}
