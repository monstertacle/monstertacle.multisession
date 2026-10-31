using System.Diagnostics;
using System.Globalization;

namespace Monstertacle.Multisession.IntegrationTests.Fixtures;

// Only mount setup needs elevation; the database worker always runs as the original user.
internal static class DiskFullLinuxProcess
{
    internal static async Task<ProcessStartInfo> BuildAsync(string root, string nonce)
    {
        ProcessStartInfo probe = new("unshare");
        foreach (string argument in new[] { "--user", "--map-root-user", "--mount", "--propagation", "private", "true" }) { probe.ArgumentList.Add(argument); }
        ProbeProcessResult available = await ProbeProcess.RunAsync(probe);
        bool privileged = available.ExitCode != 0 || Environment.GetEnvironmentVariable("MULTISESSION_TEST_PRIVILEGED_MOUNT") == "1";
        ProcessStartInfo start = new(privileged ? "sudo" : "unshare") { WorkingDirectory = root };
        string script = "set -eu; umask 077; mount -t tmpfs -o size=8m,mode=700,nodev,nosuid,noexec tmpfs \"$1/volume\"; printf '%s' \"$3\" > \"$1/volume/.disk-full-marker\"; exec dotnet \"$2\" --disk-full-worker \"$1\" \"$3\"";
        if (privileged)
        {
            string uid = await IdentityAsync("-u");
            string gid = await IdentityAsync("-g");
            foreach (string argument in new[] { "-n", "timeout", "--kill-after=2s", "50s", "unshare" }) { start.ArgumentList.Add(argument); }
            script = "set -eu; umask 077; mount -t tmpfs -o \"size=8m,mode=700,nodev,nosuid,noexec,uid=$4,gid=$5\" tmpfs \"$1/volume\"; exec setpriv --reuid=\"$4\" --regid=\"$5\" --clear-groups sh -c 'printf %s \"$3\" > \"$1/volume/.disk-full-marker\"; exec dotnet \"$2\" --disk-full-worker \"$1\" \"$3\"' multisession-disk-full \"$1\" \"$2\" \"$3\"";
            AddArguments(start, script, root, nonce);
            start.ArgumentList.Add(uid);
            start.ArgumentList.Add(gid);
        }
        else
        {
            start.ArgumentList.Add("--user");
            start.ArgumentList.Add("--map-root-user");
            AddArguments(start, script, root, nonce);
        }
        return start;
    }

    private static void AddArguments(ProcessStartInfo start, string script, string root, string nonce)
    {
        foreach (string argument in new[] { "--mount", "--propagation", "private", "sh", "-c", script, "multisession-disk-full", root, ValidationHostBuild.WorkerPath(), nonce }) { start.ArgumentList.Add(argument); }
    }

    private static async Task<string> IdentityAsync(string option)
    {
        ProcessStartInfo start = new("id");
        start.ArgumentList.Add(option);
        ProbeProcessResult result = await ProbeProcess.RunAsync(start);
        string value = result.Output.Trim();
        if (result.ExitCode != 0 || !uint.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out uint identity) || identity == 0)
        { throw new InvalidOperationException("Disk-full worker requires a non-root numeric host identity."); }
        return value;
    }
}
