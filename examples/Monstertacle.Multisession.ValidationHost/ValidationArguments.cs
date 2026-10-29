namespace Monstertacle.Multisession.ValidationHost;

internal sealed record ValidationArguments(string Scenario, string StateRoot)
{
    internal static ValidationReport? Parse(string[] args, out ValidationArguments? options)
    {
        options = null;
        if (args.Length != 4) { return ValidationReport.Fail("invalid", "InvalidArguments"); }
        string? scenario = null;
        string? root = null;
        for (int index = 0; index < args.Length; index += 2)
        {
            switch (args[index])
            {
                case "--scenario" when scenario is null: scenario = args[index + 1]; break;
                case "--state-root" when root is null: root = args[index + 1]; break;
                default: return ValidationReport.Fail("invalid", "InvalidArguments");
            }
        }
        if (scenario is null || root is null) { return ValidationReport.Fail("invalid", "InvalidArguments"); }
        if (!ValidationScenarioCatalog.Contains(scenario)) { return ValidationReport.Fail("invalid", "UnknownScenario"); }
        if (string.IsNullOrWhiteSpace(root) || root.Contains('\0') || !Path.IsPathFullyQualified(root)
            || root.StartsWith(@"\\", StringComparison.Ordinal) || root.StartsWith("//", StringComparison.Ordinal))
        {
            return ValidationReport.Fail(scenario, "InvalidStateRoot");
        }
        options = new(scenario, root);
        return null;
    }
}
