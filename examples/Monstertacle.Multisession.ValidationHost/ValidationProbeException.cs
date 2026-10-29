namespace Monstertacle.Multisession.ValidationHost;

internal sealed class ValidationProbeException(string code) : Exception("Validation probe did not pass.")
{
    internal string Code { get; } = code;
}

internal static class ValidationCheck
{
    internal static void Require(bool condition, string code)
    {
        if (!condition) { throw new ValidationProbeException(code); }
    }
}
