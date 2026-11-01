# Embedding in a .NET host

The library is consumed in-process. No service, port or productive
supervisor needs to be installed. SharpClaw is a possible later
in-process consumer; this repository does not perform any migration of existing systems.

## Prerequisites and package reference

For development: SDK 10.0.403, PowerShell 7 and access to approved NuGet
dependencies. A framework-dependent publish requires .NET Runtime 10 x64
on the target system. Supported acceptance targets are Windows 11 25H2 x64,
Windows Server 2025 x64 and Ubuntu 24.04 LTS x64. The complete target-system
acceptance is still open; a successful build does not replace it.

The eight local packages carry version 0.2.0-alpha.7 and are not published.
The packages are licensed under the MIT License. With a local
feed and nuget.org as a separate source, the host references:

- `Monstertacle.Multisession.Common`
- `Monstertacle.Multisession.Storage.Sqlite`
- `Monstertacle.Multisession.Linux` for `net10.0` or
  `Monstertacle.Multisession.Windows` for `net10.0-windows`
- optionally `Monstertacle.Multisession.Providers.Conversations` and/or `.Providers.Interactions`

A private `NuGet.Config` in the consumer project can look like this; replace the local
path with the actual package folder:

```xml
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="/absolute/path/to/artifacts/packages" />
    <add key="nuget" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="local"><package pattern="Monstertacle.Multisession.*" /></packageSource>
    <packageSource key="nuget">
      <package pattern="Monstertacle.SharpQrs" />
      <package pattern="Microsoft.*" /><package pattern="System.*" /><package pattern="SQLitePCLRaw.*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
```

Then, in the consumer project, add for example `dotnet add package
Monstertacle.Multisession.Common --version 0.2.0-alpha.7` and correspondingly Storage and
the native package. No project references to the library source tree
are required. Do not disable the direct/transitive audit.

## Complete technical entry point without vendor access

From the repository root after the release build/pack:

```powershell
dotnet pack Monstertacle.Multisession.slnx -c Release --no-build -o artifacts/packages
pwsh -NoProfile -File eng/PackageSmoke.ps1
```

The smoke test creates a fresh private consumer outside the source tree.
It requires no vendor credentials and sends no vendor requests. Its
template is located at
[PackageConsumer.cs.txt](../tests/Monstertacle.Multisession.IntegrationTests/Fixtures/PackageConsumer.cs.txt).
It contains its own complete `ISessionProvider` implementation and the
public embedding: two sessions, overlapping first requests, follow-up requests,
status/history, confirmed Cancel, Stop and native reopening of all IDs.
The generated `Consumer.csproj` uses NuGets exclusively. The output
workspace contains project, code, package lock, reports and hash manifest. The same
command is executed in native Windows/Linux CI; do not infer the result from the mere
existence of the script. The evidence so far is in
[package-consumer.md](../specs/001-agent-multisession/evidence/package-consumer.md).

## Composition with a vendor adapter

The host chooses endpoint, model identifier, profile revision and credential reference.
Before real calls, costs, provider retention and test account are to be explicitly
approved. `ModelResponse` does not start an external agent and does not execute
tools. A compatible OpenAI access must support Conversations/Responses,
a Gemini access the documented Interactions contract;
Chat Completions alone is not sufficient. Details: [providers.md](providers.md).

The following example shows the first initialization. `stateDirectory` must be a
new absolute private leaf path under an existing local parent folder.
`trustScopeId` comes from stable host-owned configuration and is required
unchanged for every reopening. `modelId` is an identifier explicitly chosen by the
host; the library has no model catalog.

```csharp
using Monstertacle.Multisession;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Providers.Conversations;
using Monstertacle.Multisession.Storage.Sqlite;
using NativeLease = Monstertacle.Multisession.Linux.LinuxStateDirectoryLease;

// On Windows, change the alias to Windows.WindowsStateDirectoryLease.
await using var lease = NativeLease.CreateNew(stateDirectory);
var options = new RuntimeOptions
{
    StateDirectory = stateDirectory,
    Retention = new RetentionPolicy(true, null),
    MaxConcurrentRequests = 2
};
using var provider = new ConversationSessionProvider(
    new ConversationOptions { Endpoint = new Uri("https://api.openai.com/v1/"), CredentialReference = "openai-test-account" },
    hostCredentialResolver);
await using var store = await SqliteStoreFactory.CreateNewAsync(
    lease, trustScopeId, options, CancellationToken.None);
await using var runtime = new SessionRuntime(store, lease,
    [new ProviderRegistration("openai", "openai-responses", 1, provider)],
    [new AgentProfile("assistant", "openai", modelId, ExecutionKind.ModelResponse,
        new ProfileSettings("revision-1", new Dictionary<string, string>()))]);
await runtime.StartAsync(options, CancellationToken.None);
// Persist these in the host assignment before the operations.
Guid sessionId = Guid.NewGuid();
Guid requestId = Guid.NewGuid();
var session = await runtime.Sessions.CreateSessionAsync(
    sessionId, "assistant", new SessionExecutionContext(null, null), CancellationToken.None);
var receipt = await runtime.Requests.SubmitAsync(
    requestId, new SubmitRequest(session.SessionId, "A short test answer, please.",
        TimeSpan.FromMinutes(1), null, false), CancellationToken.None);
```

`hostCredentialResolver` implements `ICredentialResolver.ResolveAsync` and
returns the secret value for the non-secret reference only at runtime. It remains
host-owned, honours the CancellationToken and must neither log its values
nor store them in profiles/bindings. New credentials are resolved per HTTP
attempt. The host disposes of the provider only after its last
runtime use. Use the actually registered AdapterId/FormatVersion value
unchanged; custom providers can have other positive versions.

`GetRequestAsync(receipt.RequestId, token)` returns the status and, where applicable,
`Result`. As long as Queued/Running is present, wait in a bounded and cancellable manner. A
follow-up request uses the same `session.SessionId` and exclusively the new
message. The host does not build a transcript replay. The library passes on the
most recently confirmed native continuation state. `ReadHistoryAsync` is
local observation and not a complete provider history.

To cancel a request that is still open, call
`runtime.Requests.CancelRequestAsync(receipt.RequestId, token)` and
then read the state **and** the certainty. A cancellation request alone does not confirm
a backend end. `runtime.Sessions.CloseSessionAsync(session.SessionId, token)`
blocks this session; `runtime.StopAsync(TimeSpan.FromSeconds(10), token)` ends
the runtime in a controlled manner. Stop/Dispose do not delete stored results.
Wait for the actual cleanup before disposing of host-owned resources.

On restart, choose `NativeLease.OpenExisting(stateDirectory)` and
`SqliteStoreFactory.OpenExistingAsync(lease, trustScopeId, options, token)`;
do not silently re-initialize a missing store. The existing bindings and
profile revisions remain authoritative. Uncertain work is not replayed;
[operations.md](operations.md) describes the explicit recovery decision.

## Framework-dependent publish of the ValidationHost

The host declares both RuntimeIdentifiers. From the repository root, first
restore the checked-in locks, then publish the respective target:

```powershell
dotnet restore Monstertacle.Multisession.slnx --locked-mode
dotnet publish examples/Monstertacle.Multisession.ValidationHost -c Release -f net10.0 -r linux-x64 --self-contained false --no-restore -o artifacts/publish/linux-x64
dotnet publish examples/Monstertacle.Multisession.ValidationHost -c Release -f net10.0-windows -r win-x64 --self-contained false --no-restore -o artifacts/publish/win-x64
```

No silent package updates or circumvention of the locked restore.
Cross-publish is not native functional evidence. On the actual target system,
run for example `dotnet Monstertacle.Multisession.ValidationHost.dll --scenario
embedded-consumer --state-root <absolute-private-test-path>`.
All parent directories must exist. The host creates marked private
run subfolders and does not use any existing product data.
PASS has exit code 0, FAIL 2, NOT RUN 3. An unknown or faulty selection
never passes. `soak` requires a real hour; live-openai/live-gemini remain
NOT RUN without explicit approval. The complete scenario matrix is in the
[Quickstart](../specs/001-agent-multisession/quickstart.md).

A final release needs the same source/package state on all
target systems, live approvals and an independent integrator run. Existing
local/NuGet probes claim neither this acceptance nor a SharpClaw conversion.

With `OpenExistingAsync`, `StartAsync` checks preserved Open sessions before releasing the
workers. The native adapters read the selected model metadata entry with the current
credential and then check the same native session reference. Affected queue
entries are preserved on rejection; `SessionSnapshot.AvailabilityError` explains the
sanitized cause. Own availability locks can be repaired after access has been corrected and
a positive re-check; recovery/other native blocks remain in place.
The optional `ISessionAvailabilityProvider` facet allows host-owned adapters the same
read-only check. The direct Gemini lazy reopen still does not create an HTTP operation.
