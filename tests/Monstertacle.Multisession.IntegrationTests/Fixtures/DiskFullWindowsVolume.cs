using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;

namespace Monstertacle.Multisession.IntegrationTests.Fixtures;

// Only the fresh VHD file under our private fixture is selected; no physical disk is targeted.
[SupportedOSPlatform("windows")]
internal sealed class DiskFullWindowsVolume(string root, string nonce) : IAsyncDisposable
{
    private const string _probeLetters = "ZYXWVUTSRQP";
    private readonly string _image = Path.Combine(root, "disk-full.vhdx");
    private readonly string _label = "ms" + nonce[..8];
    private bool _mayOwnImage;
    internal string StateDirectory { get; private set; } = string.Empty;

    internal async Task CreateAsync()
    {
        if (File.Exists(_image)) { throw new InvalidOperationException("Probe disk already exists."); }
        _mayOwnImage = true;
        string[] used = Environment.GetLogicalDrives();
        char letter = _probeLetters.First(candidate => !used.Contains(candidate + @":\", StringComparer.OrdinalIgnoreCase));
        string drive = letter + @":\";
        string script = $"create vdisk file=\"{_image}\" maximum=64 type=expandable\nattach vdisk\ncreate partition primary\nformat fs=ntfs unit=4096 quick label={_label}\nassign letter={letter}\nexit\n";
        ProbeProcessResult created = await DiskpartAsync("create-volume.txt", script);
        if (created.ExitCode != 0) { throw new InvalidOperationException("Private VHD preparation failed."); }
        DriveInfo info = new(drive);
        if (!info.IsReady || info.VolumeLabel != _label || info.TotalSize > 128L * 1024 * 1024) { throw new InvalidOperationException("Private VHD identity is not confirmed."); }
        StateDirectory = Path.Combine(drive, "multisession-" + nonce);
        SecurityIdentifier owner = WindowsIdentity.GetCurrent().User!;
        DirectorySecurity security = new();
        security.SetOwner(owner);
        security.SetAccessRuleProtection(true, false);
        security.AddAccessRule(new FileSystemAccessRule(owner, FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        new DirectoryInfo(StateDirectory).Create(security);
    }

    public async ValueTask DisposeAsync()
    {
        if (!_mayOwnImage || !File.Exists(_image)) { return; }
        await DiskpartAsync("detach-volume.txt", $"select vdisk file=\"{_image}\"\ndetach vdisk\nexit\n");
        File.Delete(_image); // A remaining attachment must surface as a cleanup failure.
        _mayOwnImage = false;
    }

    private async Task<ProbeProcessResult> DiskpartAsync(string name, string script)
    {
        string path = Path.Combine(root, name);
        await File.WriteAllTextAsync(path, script, Encoding.Unicode);
        ProcessStartInfo start = new("diskpart.exe") { WorkingDirectory = root };
        start.ArgumentList.Add("/s");
        start.ArgumentList.Add(path);
        return await ProbeProcess.RunAsync(start);
    }
}
