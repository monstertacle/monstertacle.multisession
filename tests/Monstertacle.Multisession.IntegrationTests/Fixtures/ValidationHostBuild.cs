using System.Reflection;

namespace Monstertacle.Multisession.IntegrationTests.Fixtures;

// Shared location of the already built repository validation executable.
internal static class ValidationHostBuild
{
    internal static string WorkerPath()
    {
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Monstertacle.Multisession.slnx"))) { root = root.Parent; }
        if (root is null) { throw new InvalidOperationException("Probe requires its repository build."); }
        string configuration = typeof(ValidationHostBuild).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
        string worker = Path.Combine(root.FullName, "examples", "Monstertacle.Multisession.ValidationHost", "bin", configuration, "net10.0", "Monstertacle.Multisession.ValidationHost.dll");
        return File.Exists(worker) ? worker : throw new InvalidOperationException("Validation worker has not been built.");
    }
}
