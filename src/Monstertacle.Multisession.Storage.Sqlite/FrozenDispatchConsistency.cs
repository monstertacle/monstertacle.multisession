using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.Storage.Sqlite;

internal static class FrozenDispatchConsistency
{
    internal static bool Matches(StoredDispatch stored, ExecutionReservation reservation, SubmitRequest submission,
        SessionSnapshot current, long sequence, long revision)
    {
        if (stored.Snapshot is not
            {
                Reservation: { } frozenReservation, Submission: { } frozenSubmission,
                Session: { Context: { } context, Binding: { } binding } frozen
            } snapshot) { return false; }
        return frozenReservation == reservation && frozenSubmission == submission && snapshot.Sequence == sequence
            && snapshot.BindingRevision == revision && frozen.BindingRevision == revision && revision >= 0
            && current.BindingRevision >= revision && frozen.SessionId == reservation.SessionId && context.SessionId == reservation.SessionId
            && frozen.ProviderId == current.ProviderId && frozen.AgentProfileId == current.AgentProfileId
            && frozen.SettingsRevision == current.SettingsRevision && ContextMatches(context, current.Context)
            && current.Binding is { } latest && binding.AdapterId == latest.AdapterId && binding.FormatVersion == latest.FormatVersion
            && ReferencesMatch(binding, latest, stored.SessionReference, stored.OperationReference)
            && (stored.NativeSessionReference is null || !string.IsNullOrWhiteSpace(stored.NativeSessionReference));
    }

    private static bool ContextMatches(ProviderSessionContext frozen, ProviderSessionContext current)
    {
        if (frozen.SessionId != current.SessionId || frozen.ModelId != current.ModelId || frozen.ExecutionKind != current.ExecutionKind
            || frozen.ExecutionContext != current.ExecutionContext || frozen.Settings.Count != current.Settings.Count) { return false; }
        foreach (var entry in frozen.Settings)
        {
            if (!current.Settings.TryGetValue(entry.Key, out string? value) || !string.Equals(entry.Value, value, StringComparison.Ordinal)) { return false; }
        }
        return true;
    }

    private static bool ReferencesMatch(ProviderBinding frozen, ProviderBinding current, string? acknowledged, string? operation)
    {
        if ((acknowledged is not null && string.IsNullOrWhiteSpace(acknowledged)) || (operation is not null && string.IsNullOrWhiteSpace(operation))) { return false; }
        if (frozen.SessionReference is { } original && original != current.SessionReference) { return false; }
        return acknowledged is null || ((frozen.SessionReference is null || frozen.SessionReference == acknowledged)
            && (current.SessionReference is null || current.SessionReference == acknowledged));
    }
}
