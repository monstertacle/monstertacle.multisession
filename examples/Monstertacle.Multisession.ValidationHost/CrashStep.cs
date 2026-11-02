namespace Monstertacle.Multisession.ValidationHost;

// One completed worker step and the time it took, for attributing a slow or failed crash setup.
internal sealed record CrashStep(string Name, long ElapsedMilliseconds);
