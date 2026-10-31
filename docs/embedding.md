# Einbettung in einen .NET-Host

Die Bibliothek wird im Prozess konsumiert. Kein Dienst, Port oder produktiver
Supervisor muss installiert werden. SharpClaw ist ein möglicher späterer
In-Process-Konsument; dieses Repository führt keine Bestandsmigration durch.

## Voraussetzungen und Paketbezug

Für Entwicklung: SDK 10.0.403, PowerShell 7 und Zugriff auf genehmigte NuGet-
Abhängigkeiten. Ein framework-dependent Publish benötigt auf dem Zielsystem
.NET Runtime 10 x64. Unterstützte Abnahmeziele sind Windows 11 25H2 x64,
Windows Server 2025 x64 und Ubuntu 24.04 LTS x64. Die vollständige Zielsystem-
Abnahme ist noch offen; ein erfolgreicher Build ersetzt sie nicht.

Die sechs lokalen Pakete tragen Version 0.2.0-alpha.1 und werden nicht veröffentlicht.
Die Produktlizenz muss vor Veröffentlichung festgelegt werden. Mit einem lokalen
Feed und nuget.org als separater Quelle referenziert der Host:

- `Monstertacle.Multisession.Common`
- `Monstertacle.Multisession.Storage.Sqlite`
- `Monstertacle.Multisession.Linux` für `net10.0` oder
  `Monstertacle.Multisession.Windows` für `net10.0-windows`
- optional `Monstertacle.Multisession.Providers.OpenAI` und/oder `.Providers.Gemini`

Ein privater `NuGet.Config` im Consumerprojekt kann so aussehen; den lokalen
Pfad durch den tatsächlichen Paketordner ersetzen:

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

Danach im Consumerprojekt etwa `dotnet add package
Monstertacle.Multisession.Common --version 0.2.0-alpha.1` und entsprechend Storage sowie
das native Paket hinzufügen. Keine Projektverweise auf den Bibliotheksquellbaum
sind erforderlich. Den direkten/transitiven Audit nicht deaktivieren.

## Vollständiger technischer Einstieg ohne Anbieterzugang

Vom Repositoryroot nach dem Release-Build/Pack:

```powershell
dotnet pack Monstertacle.Multisession.slnx -c Release --no-build -o artifacts/packages -p:PackageVersion=0.2.0-alpha.1
pwsh -NoProfile -File eng/PackageSmoke.ps1
```

Der Smoke erzeugt einen frischen privaten Konsumenten außerhalb des Quellbaums.
Er benötigt keine Anbietercredentials und sendet keine Herstelleranfragen. Seine
Vorlage liegt unter
[PackageConsumer.cs.txt](../tests/Monstertacle.Multisession.IntegrationTests/Fixtures/PackageConsumer.cs.txt).
Sie enthält eine eigene vollständige `ISessionProvider`-Implementierung und die
öffentliche Einbettung: zwei Sessions, überlappende erste Anfragen, Folgeanfragen,
Status/History, bestätigter Cancel, Stop und native Wiederöffnung aller IDs.
Die generierte `Consumer.csproj` benutzt ausschließlich NuGets. Der ausgegebene
Workspace enthält Projekt, Code, Paketlock, Reports und Hashmanifest. Derselbe
Befehl wird in nativer Windows-/Linux-CI ausgeführt; Ergebnis nicht aus bloßer
Skriptvorhandenheit ableiten. Die bisherigen Belege stehen in
[package-consumer.md](../specs/001-agent-multisession/evidence/package-consumer.md).

## Komposition mit einem Herstelleradapter

Der Host wählt Endpoint, Modellkennung, Profilrevision und Credentialreferenz.
Vor echten Aufrufen sind Kosten, Providerretention und Testkonto ausdrücklich
freizugeben. `ModelResponse` startet keinen externen Agenten und führt keine
Werkzeuge aus. Ein kompatibler OpenAI-Zugang muss Conversations/Responses,
ein Gemini-Zugang den dokumentierten Interactions-Vertrag unterstützen;
Chat-Completions allein genügt nicht. Details: [providers.md](providers.md).

Das folgende Beispiel zeigt die erste Initialisierung. `stateDirectory` muss ein
neuer absoluter privater Blattpfad unter einem vorhandenen lokalen Elternordner
sein. `trustScopeId` kommt aus stabiler hosteigener Konfiguration und wird für
jede Wiederöffnung unverändert benötigt. `modelId` ist eine ausdrücklich vom
Host gewählte Kennung; die Bibliothek besitzt keinen Modellkatalog.

```csharp
using Monstertacle.Multisession;
using Monstertacle.Multisession.Contracts;
using Monstertacle.Multisession.Providers.OpenAI;
using Monstertacle.Multisession.Storage.Sqlite;
using NativeLease = Monstertacle.Multisession.Linux.LinuxStateDirectoryLease;

// Unter Windows den Alias auf Windows.WindowsStateDirectoryLease ändern.
await using var lease = NativeLease.CreateNew(stateDirectory);
var options = new RuntimeOptions
{
    StateDirectory = stateDirectory,
    Retention = new RetentionPolicy(true, null),
    MaxConcurrentRequests = 2
};
using var provider = new OpenAiSessionProvider(
    new OpenAiOptions { CredentialReference = "openai-test-account" },
    hostCredentialResolver);
await using var store = await SqliteStoreFactory.CreateNewAsync(
    lease, trustScopeId, options, CancellationToken.None);
await using var runtime = new SessionRuntime(store, lease,
    [new ProviderRegistration("openai", "openai-responses", 1, provider)],
    [new AgentProfile("assistant", "openai", modelId, ExecutionKind.ModelResponse,
        new ProfileSettings("revision-1", new Dictionary<string, string>()))]);
await runtime.StartAsync(options, CancellationToken.None);
var session = await runtime.Sessions.CreateSessionAsync(
    "assistant", new SessionExecutionContext(null, null), CancellationToken.None);
var receipt = await runtime.Requests.SubmitAsync(
    new SubmitRequest(session.SessionId, "Eine kurze Testantwort bitte.",
        TimeSpan.FromMinutes(1), null, false), CancellationToken.None);
```

`hostCredentialResolver` implementiert `ICredentialResolver.ResolveAsync` und
liefert den geheimen Wert zur nicht geheimen Referenz nur zur Laufzeit. Er bleibt
hostbesessen, berücksichtigt den CancellationToken und darf seine Werte weder
loggen noch in Profile/Bindings speichern. Neue Credentials werden je HTTP-
Versuch aufgelöst. Der Host entsorgt den Provider erst nach dessen letzter
Runtimebenutzung. Den tatsächlich registrierten AdapterId/FormatVersion-Wert
unverändert verwenden; eigene Provider können andere positive Versionen haben.

`GetRequestAsync(receipt.RequestId, token)` liefert Status und gegebenenfalls
`Result`. Solange Queued/Running vorliegt, begrenzt und abbrechbar warten. Eine
Folgeanfrage verwendet dieselbe `session.SessionId` und ausschließlich die neue
Nachricht. Der Host baut keinen Transcript-Replay. Die Bibliothek gibt den
zuletzt bestätigten nativen Fortsetzungsstand weiter. `ReadHistoryAsync` ist
lokale Beobachtung und keine vollständige Providerhistorie.

Zum Abbruch einer noch offenen Anfrage
`runtime.Requests.CancelRequestAsync(receipt.RequestId, token)` aufrufen und
anschließend Zustand **und** Gewissheit lesen. Ein Cancelwunsch allein bestätigt
kein Backendende. `runtime.Sessions.CloseSessionAsync(session.SessionId, token)`
sperrt diese Sitzung; `runtime.StopAsync(TimeSpan.FromSeconds(10), token)` beendet
die Runtime kontrolliert. Stop/Dispose löschen keine gespeicherten Ergebnisse.
Die tatsächliche Bereinigung abwarten, bevor hosteigene Ressourcen entsorgt werden.

Bei Neustart `NativeLease.OpenExisting(stateDirectory)` und
`SqliteStoreFactory.OpenExistingAsync(lease, trustScopeId, options, token)` wählen;
keinen fehlenden Store still neu initialisieren. Die vorhandenen Bindings und
Profilrevisionen bleiben maßgeblich. Ungewisse Arbeit wird nicht replayt;
[operations.md](operations.md) beschreibt die ausdrückliche Recoveryentscheidung.

## Framework-dependent Publish des ValidationHosts

Der Host deklariert beide RuntimeIdentifiers. Aus dem Repositoryroot zunächst
die eingecheckten Locks restaurieren, dann das jeweilige Ziel publizieren:

```powershell
dotnet restore Monstertacle.Multisession.slnx --locked-mode
dotnet publish examples/Monstertacle.Multisession.ValidationHost -c Release -f net10.0 -r linux-x64 --self-contained false --no-restore -o artifacts/publish/linux-x64
dotnet publish examples/Monstertacle.Multisession.ValidationHost -c Release -f net10.0-windows -r win-x64 --self-contained false --no-restore -o artifacts/publish/win-x64
```

Keine stillen Paketupdates oder Umgehung des Locked-Restores.
Cross-Publish ist kein nativer Funktionsnachweis. Auf dem tatsächlichen Zielsystem
zum Beispiel `dotnet Monstertacle.Multisession.ValidationHost.dll --scenario
embedded-consumer --state-root <absoluter-privater-testpfad>` ausführen.
Alle Elternverzeichnisse müssen existieren. Der Host legt markierte private
Run-Unterordner an und verwendet keine vorhandenen Produktdaten.
PASS hat Exitcode 0, FAIL 2, NOT RUN 3. Eine unbekannte oder fehlerhafte Auswahl
besteht nie. `soak` benötigt eine echte Stunde; live-openai/live-gemini bleiben
ohne ausdrückliche Freigabe NOT RUN. Die komplette Szenariomatrix steht im
[Quickstart](../specs/001-agent-multisession/quickstart.md).

Eine endgültige Freigabe braucht denselben Source-/Paketstand auf allen
Zielsystemen, Live-Freigaben und einen unabhängigen Integratorlauf. Vorhandene
lokale/NuGet-Proben behaupten weder diese Abnahme noch einen SharpClaw-Umbau.

Bei `OpenExistingAsync` prüft `StartAsync` erhaltene Open-Sitzungen vor Freigabe der
Worker. Die nativen Adapter lesen den ausgewählten Modell-Metadateneintrag mit aktuellem
Credential und prüfen anschließend denselben nativen Sitzungsbezug. Betroffene Queue-
einträge bleiben bei Ablehnung erhalten; `SessionSnapshot.AvailabilityError` erklärt die
bereinigte Ursache. Eigene Verfügbarkeitssperren sind nach korrigiertem Zugang und
positiver erneuter Prüfung reparierbar; Recovery-/andere Nativeblockaden bleiben bestehen.
Die optionale `ISessionAvailabilityProvider`-Facette erlaubt hosteigenen Adaptern dieselbe
lesende Prüfung. Der direkte Gemini-Lazy-Reopen erzeugt weiterhin keine HTTPoperation.
