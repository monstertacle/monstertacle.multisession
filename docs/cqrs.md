# Typisierte Runtime-Operationen

Common referenziert Monstertacle.SharpQrs 0.2.0-alpha.1 transitiv. Nach dem normalen
`StartAsync` stehen `runtime.Commands` und `runtime.Queries` bereit:

```csharp
using Monstertacle.Multisession.Cqrs;

// Die Kennungen vor dem Aufruf im Host-Auftrag persistieren.
Guid sessionId = Guid.NewGuid();
Guid requestId = Guid.NewGuid();
var session = await runtime.Commands.DispatchAsync(
    new CreateSessionCommand(sessionId, "configured-profile", new(null, null)), cancellationToken);
var receipt = await runtime.Commands.DispatchAsync(new SubmitRequestCommand(
    requestId, new(session.SessionId, "input", TimeSpan.FromSeconds(30), null, false)), cancellationToken);
var lookup = await runtime.Queries.DispatchAsync(
    new GetRequestQuery(receipt.RequestId), cancellationToken);
var snapshot = lookup.Value; // null, falls der Datensatz nicht existiert
```

| Nachricht | Dispatcher | Ergebnis |
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

Die Nachrichten liegen im Namespace `Monstertacle.Multisession.Cqrs`, die bestehenden
Snapshots in `Monstertacle.Multisession.Contracts`. Lookup-Ergebnisse sind immer ein
nicht-null `LookupResult<T>`; dessen `Value` darf fehlen. Ein `CommandAcknowledgement`
bestätigt nur die Rückkehr des lokalen Handlers. Cancel und Close bestätigen damit
keinen tatsächlichen externen Abschluss.

Die bestehenden `Sessions`-, `Requests`- und `Recovery`-Ports bleiben verfügbar und
übersetzen in genau diese Nachrichten. Hinter den Handlern gelten unverändert die
bisherigen Lifecycle-, Transaktions-, Revisions- und Reservierungsregeln.
`InspectRecoveryQuery` liest native Evidenz ohne lokale Freigabe. Stop verfolgt auch
die öffentlichen Dispatcher-Tasks bereits gestarteter Lifecycle-Operationen bis zum
Ende, bevor es Store und Lease freigibt.

Registrierungen sind je Runtime vollständig vor der Veröffentlichung aufgebaut.
Die öffentlichen Dispatcher bieten keine Registrierung an. Unbekannte Nachrichten
werden abgewiesen; es gibt kein automatisches Scanning, Retry oder Replay.
Der Caller-Token und ursprüngliche Handlerfehler bleiben erhalten; vorhandene
Lifecycle-Operationen verknüpfen weiterhin den Token mit ihrer Stop-Anforderung.
Keine zusätzliche Datenbank, Read-Replica oder Event-Sourcing-Schicht ist erforderlich.

Coverage: `dotnet tool restore`, danach `pwsh scripts/Invoke-Quality.ps1` auf dem
nativen Zielbetriebssystem. Beim Multitarget-Plattformprojekt wählt die Qualitätsprüfung
gezielt das native Framework und übergibt anschließend alle frischen Berichte an Cover.
Die erzeugte `cover-native-config.json` zählt alle gemeinsamen Bibliotheken und die
native Plattform. Die fremde Plattform ist im jeweils anderen nativen CI verpflichtend.
Ein Cover-Aufruf mit der Root-Konfiguration zählt dagegen beide Plattformen und meldet
fremde, lokal unausführbare Interop-Methoden entsprechend als ungetestet.
