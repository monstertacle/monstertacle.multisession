# Native Provider

Die Bibliothek registriert gewöhnliche `ISessionProvider`-Objekte pro Hostinstanz.
Profile wählen Provider und opaque Modell-/Deploymentkennungen ausdrücklich. Der Kern
besitzt keinen Modellkatalog und wechselt Anbieter oder Modell nicht automatisch.
Ein `ModelResponse` ist kein Nachweis einer externen Agentaufgabe.

## Conversations und Responses

`OpenAiSessionProvider` implementiert den begrenzten REST-Vertrag `/v1`:
Conversation per POST eröffnen, denselben Bezug per GET wieder öffnen, neue Eingabe
per Responses-POST mit `conversation`, `background=true`, `store=true` ausführen.
`previous_response_id`, Transcript-Replay, eigene Tools oder Compaction werden nicht
hinzugefügt. Der Adapter unterstützt `ModelResponse`, keine `AgentTask`-Ausführung.

```csharp
using var provider = new OpenAiSessionProvider(
    new OpenAiOptions
    {
        Endpoint = new Uri("https://api.openai.com/v1/"),
        CredentialReference = "host-owned-reference"
    },
    hostCredentialResolver);
```

Der konsumierende Host stellt den Resolver bereit und verfügt über die Berechtigung
zur Nutzung des konfigurierten Endpoints. Der Resolver erhält bei jedem HTTP-Aufruf die
Referenz neu; Credentialwerte werden weder Binding noch Profilzustand hinzugefügt.
HTTPS ist erforderlich. HTTP ist nur mit `AllowLocalHttp=true` für Loopbacktestendpunkte
zulässig. Endpunkte mit UserInfo, Query oder Fragment werden abgewiesen. Ein kompatibler
Zugang muss diesen Conversations-/Responses-Vertrag anbieten; Chat-Completions allein
reicht nicht. Modellnamen werden unverändert übergeben. Zulässiges Profileinstellungfeld:
`instructions`; unbekannte Felder werden vor I/O abgewiesen.

Operations-IDs werden vor Polling oder Ergebnisrückgabe über den awaitbaren Callback
bestätigt. Polls beginnen mit einer Sekunde Abstand. Temporäre GET-Fehler 429/503 dürfen
innerhalb der Gesamtfrist mit Retry-After wiederholt werden. Ausführende POSTs werden
nie automatisch wiederholt. Inspect ist ein einzelner fachlicher GET-Zugriff (mit begrenzten
Transportwiederholungen); Cancel sendet einmal POST und liest den Endstatus danach.
Keine dieser Operationen entscheidet eigenständig über lokale Recovery oder Slotfreigabe.

Headerfrist: 30 Sekunden. Send-Gesamtfrist: `ProviderCall.Timeout`, einschließlich
Callbacks und Polling. Open/Reopen/Inspect/Cancel haben eine endliche 600-Sekunden-Frist;
der Hostkern darf eine kürzere äußere Frist setzen. `MaximumResponseBytes` begrenzt native
JSONantworten standardmäßig auf 32 MiB; `MaxOutputBytes` begrenzt die endgültige UTF8-
Textausgabe separat. Eine zu große Ausgabe wird leerer Fehler, kein gekürzter Erfolg.
Timeout oder Verbindungsabbruch liefert keinen externen Endbeleg. Unknown erfordert die
vorgesehene Recoveryentscheidung. Eine bestätigte native Endantwort bleibt von einem
lokalen Cancelwunsch unterscheidbar.

Ohne injizierten Handler besitzt und entsorgt der Adapter einen HttpClient mit deaktivierten
Redirects/Cookies. Ein explizit injizierter HttpMessageHandler bleibt hostbesessen und muss
die Transportgrenze selbst einhalten; er dient insbesondere deterministischen Fixtures.
Der Host muss den Provider nach Beendigung seiner Runtimebenutzung entsorgen.

Der Vertrag wurde deterministisch mit synthetischen Antworten geprüft. Keine Aussage über
reale Kontoberechtigung, Modellverfügbarkeit oder Herstellerabnahme. Daten werden beim
Hersteller gespeichert; die lokale Retention löscht keine Conversations/Responses und keine
Backups. Die maßgeblichen Bedingungen kommen vom freigegebenen Providerkonto.
Quellen: [Conversation state](https://developers.openai.com/api/docs/guides/conversation-state),
[Background mode](https://developers.openai.com/api/docs/guides/background).

## Gemini Interactions

Die Implementierung des stabilen `/v1`-Vertrags folgt als nächster offener Arbeitsschritt.
Das vorhandene Projektgerüst ist noch kein funktionsfähiger Gemini-Adapter. Der geplante
native Interaktionskopf wird ausschließlich aus bestätigten Abschlüssen fortgeführt;
keine eigene Geschichte oder Toolschleife. Aktuelle Umsetzung und Prüfnachweise stehen in
[Gesamtimplementierung](../specs/001-agent-multisession/evidence/implementation-progress.md).
