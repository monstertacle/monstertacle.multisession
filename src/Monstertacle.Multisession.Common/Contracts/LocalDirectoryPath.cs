namespace Monstertacle.Multisession.Contracts;

// Syntactic admission only. Native ownership, symlink and mounted-filesystem checks remain separate.
internal static class LocalDirectoryPath
{
    public static bool IsValid(string? path)
        => !string.IsNullOrWhiteSpace(path)
            && !path.Contains('\0')
            && Path.IsPathFullyQualified(path)
            && !path.StartsWith(@"\\", StringComparison.Ordinal)
            && !path.StartsWith("//", StringComparison.Ordinal);
}
