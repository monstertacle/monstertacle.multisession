using Monstertacle.Multisession.Contracts;

namespace Monstertacle.Multisession.ValidationHost;

internal sealed class ValidationProbeException(string code, ValidationFailure? failure = null,
    IReadOnlyList<ValidationFailure>? cleanupFailures = null) : Exception("Validation probe did not pass.")
{
    internal string Code { get; } = code;
    internal ValidationFailure? Failure { get; } = failure;
    internal IReadOnlyList<ValidationFailure> CleanupFailures { get; } = cleanupFailures ?? [];

    internal static ValidationProbeException FromException(Exception failure) => failure as ValidationProbeException
        ?? new(failure is SessionOperationException operation ? operation.Failure.Code.ToString() : "ScenarioExecutionFailed",
            ValidationFailure.FromException(failure));

    internal ValidationProbeException WithCleanup(Exception cleanup) => new(Code, Failure,
        [.. CleanupFailures,
            .. cleanup is ValidationProbeException { Failure: null, CleanupFailures.Count: > 0 } probe
                ? probe.CleanupFailures : [ValidationFailure.FromException(cleanup), .. (cleanup as ValidationProbeException)?.CleanupFailures ?? []]]);
}

internal static class ValidationCheck
{
    internal static void Require(bool condition, string code)
    {
        if (!condition) { throw new ValidationProbeException(code); }
    }
}
