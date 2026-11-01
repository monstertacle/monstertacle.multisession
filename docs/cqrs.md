# Typed runtime operations

Common references Monstertacle.SharpQrs 0.2.0-alpha.1 transitively. After the normal
`StartAsync`, `runtime.Commands` and `runtime.Queries` are available:

```csharp
using Monstertacle.Multisession.Cqrs;

// Persist the identifiers in the host assignment before the call.
Guid sessionId = Guid.NewGuid();
Guid requestId = Guid.NewGuid();
var session = await runtime.Commands.DispatchAsync(
    new CreateSessionCommand(sessionId, "configured-profile", new(null, null)), cancellationToken);
var receipt = await runtime.Commands.DispatchAsync(new SubmitRequestCommand(
    requestId, new(session.SessionId, "input", TimeSpan.FromSeconds(30), null, false)), cancellationToken);
var lookup = await runtime.Queries.DispatchAsync(
    new GetRequestQuery(receipt.RequestId), cancellationToken);
var snapshot = lookup.Value; // null if the record does not exist
```

| Message | Dispatcher | Result |
| --- | --- | --- |
| CreateSessionCommand | Command | `SessionSnapshot` |
| CloseSessionCommand | Command | `CommandAcknowledgement` |
| DeleteSessionCommand | Command | `CommandAcknowledgement` |
| SubmitRequestCommand | Command | `RequestReceipt` |
| CancelRequestCommand | Command | `CommandAcknowledgement` |
| ResolveRecoveryCommand | Command | `CommandAcknowledgement` |
| GetSessionQuery | Query | `LookupResult<SessionSnapshot>` |
| ListSessionsQuery | Query | `Page<SessionSnapshot>` |
| ReadHistoryQuery | Query | `Page<HistoryEntry>` |
| GetRequestQuery | Query | `LookupResult<RequestSnapshot>` |
| ListRequestsQuery | Query | `Page<RequestSnapshot>` |
| GetRecoveryCaseQuery | Query | `LookupResult<RecoveryCaseSnapshot>` |
| ListRecoveryCasesQuery | Query | `Page<RecoveryCaseSnapshot>` |
| InspectRecoveryQuery | Query | `ProviderReply` |

The messages are in the namespace `Monstertacle.Multisession.Cqrs`, the existing
snapshots in `Monstertacle.Multisession.Contracts`. Lookup results are always a
non-null `LookupResult<T>`; its `Value` may be absent. A `CommandAcknowledgement`
confirms only the return of the local handler. Cancel and Close therefore do not confirm
an actual external completion.

The existing `Sessions`, `Requests` and `Recovery` ports remain available and
translate into exactly these messages. Behind the handlers, the existing
lifecycle, transaction, revision and reservation rules apply unchanged.
`InspectRecoveryQuery` reads native evidence without local release. Stop also tracks
the public dispatcher tasks of already started lifecycle operations until their
end before it releases store and lease.

Registrations are fully built per runtime before publication.
The public dispatchers offer no registration. Unknown messages
are rejected; there is no automatic scanning, retry or replay.
The caller token and original handler failures are preserved; existing
lifecycle operations continue to link the token with their Stop request.
No additional database, read replica or event-sourcing layer is required.

Coverage: `dotnet tool restore`, then `pwsh scripts/Invoke-Quality.ps1` on the
native target operating system. For the multi-target platform project, the quality check
specifically selects the native framework and then passes all fresh reports to Cover.
The generated `cover-native-config.json` counts all shared libraries and the
native platform. The foreign platform is mandatory in the respective other native CI.
A Cover invocation with the root configuration, by contrast, counts both platforms and reports
foreign interop methods that cannot be executed locally as untested accordingly.
