using System.Diagnostics;
using System.Text.Json;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.ValidationHost;

namespace Monstertacle.Multisession.IntegrationTests.Fixtures;

// A rootless private Linux mount namespace or a fresh Windows VHD, both independently bounded.
internal static class DiskFullHarness
{
    internal static async Task<ProbeProcessResult> RunAsync(PersistedDispatchFixture seed)
    {
        await using PrivateStoreDirectory root = new();
        string nonce = Guid.NewGuid().ToString("N");
        DiskFullWindowsVolume? windows = null;
        try
        {
            string state = Path.Combine(root.StateDirectory, DiskFullProtocol.VolumeDirectory);
            if (OperatingSystem.IsWindows())
            {
                windows = new(root.StateDirectory, nonce);
                await windows.CreateAsync();
                state = windows.StateDirectory;
                await File.WriteAllTextAsync(Path.Combine(state, DiskFullProtocol.MarkerFile), nonce);
            }
            else { Directory.CreateDirectory(state, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute); }
            File.Copy(seed.Host.Directory.DatabasePath, Path.Combine(root.StateDirectory, DiskFullProtocol.SeedFile), false);
            await root.PrepareFileAsync(DiskFullProtocol.MarkerFile, StateFileMode.CreateNew, CancellationToken.None);
            await root.PrepareFileAsync(DiskFullProtocol.ManifestFile, StateFileMode.CreateNew, CancellationToken.None);
            await File.WriteAllTextAsync(Path.Combine(root.StateDirectory, DiskFullProtocol.MarkerFile), nonce);
            await File.WriteAllTextAsync(Path.Combine(root.StateDirectory, DiskFullProtocol.ManifestFile),
                JsonSerializer.Serialize(new DiskFullManifest(seed.Host.Directory.TrustScopeId, seed.Session.SessionId, seed.Receipt.RequestId, state)));
            ProcessStartInfo start = OperatingSystem.IsLinux()
                ? await DiskFullLinuxProcess.BuildAsync(root.StateDirectory, nonce)
                : BuildStart(root.StateDirectory, nonce);
            return await ProbeProcess.RunAsync(start);
        }
        finally { if (OperatingSystem.IsWindows() && windows is not null) { await windows.DisposeAsync(); } }
    }

    private static ProcessStartInfo BuildStart(string root, string nonce)
    {
        string worker = ValidationHostBuild.WorkerPath();
        ProcessStartInfo start = new("dotnet") { WorkingDirectory = root };
        if (OperatingSystem.IsWindows())
        {
            foreach (string argument in new[] { worker, DiskFullProtocol.WorkerArgument, root, nonce }) { start.ArgumentList.Add(argument); }
        }
        else { throw new NotSupportedException("Disk-full probe requires a native supported test host."); }
        return start;
    }
}
