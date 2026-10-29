# T032/T034/T038/T040: OpenAI Conversations/Responses

Created: 2026-10-29T07:48:22Z
Owner: ausführende Gesamtimplementierungssitzung, feature/reserved-dispatch-contract.
Basis: 0a76b51; generische Konfiguration und Runtime implementiert, Gesamtauftrag aktiv.

Bestandsanalyse: Providers.OpenAI ist bislang nur ein csproj mit Commonreferenz, keine
Adapter-/Transport-/Protokollimplementierung. ContractTests referenzieren dieses Projekt,
aber kein HTTP-Fixture vorhanden. ISessionProvider besitzt Open/Reopen/Send/Inspect/Cancel;
ProviderSessionContext enthält eingefrorene nichtgeheime Settings. RequestDispatcher
persistiert Reservierung vor Send, wartet Observationcallbacks und finalisiert nur sichere
Replies. Callbackfehler/Unknown werden konservativ blockiert, kein Replay/Herstellerwechsel.
ProviderCatalog wählt keine Hersteller und kennt keine HTTP-Endpunkte. ICredentialResolver
ist hostseitiger Frischauflösungsport. Neues HTTP-Verhalten gehört ausschließlich ins
Providerpaket, kein Transporttyp in Common und keine zusätzliche SDK-Abhängigkeit.

Verifizierte Primärquellen am aktuellen Tag: offizielle OpenAI Docs
https://developers.openai.com/api/docs/guides/conversation-state (tatsächlich abgerufen):
Conversations erzeugen dauerhafte IDs; Responses verwenden diese ID mit neuem Input.
https://developers.openai.com/api/docs/guides/background (tatsächlich abgerufen):
background=true, GET-Poll für queued/in_progress und Cancel-POST, Endstatus separat prüfen.
https://developers.openai.com/api/reference/resources/conversations/methods/create
und /responses/methods/create: Docs-Fetch lieferte nur generierten Index, Web-Open scheiterte
am übergroßen Dokument. Offizieller Suchauszug belegt POST conversations/IDobjekt und
conversation/previous_response_id-Ausschluss; kein weitergehendes Schema daraus erfunden.
Der eingecheckte begrenzte Vertragsumfang provider-contract.md §3 bleibt maßgeblich.
Keine Modellliste, keine Übernahme fremder Beispiele/Prompts als Fixtures.

Entscheidung: OpenAiSessionProvider ist kleine ISessionProvider-Fassade mit eigenem
OpenAiOptions, OpenAiTransport, OpenAiProtocol und OpenAiOperation. Öffentliche Konfiguration
enthält HTTPS-Basisendpoint/CredentialReference; HTTP ausschließlich explizit für Loopback-
Fixtures. Konfiguration und native Bindung vor I/O prüfen. Productionhandler verbietet
Redirect/Cookies; injizierter Handler ist explizit hostseitiger Transport für Fixtures.
HttpClient-Lifetime gehört Provider, Credentials werden je HTTP-Operation frisch aufgelöst,
nie in Binding/Settings gespeichert. Keine Wiederholung von Open-/Send-/Cancel-POSTs.

Zuerst ausschließlich Open/Reopen-Red: POST conversations mit leerem Body, stabile ID,
frische Bearerauflösung, GET genau derselben nativen ID statt neuer Identität. Minimale
Signaturstubs für übrige ISessionProvider-Methoden bis zu deren eigenen Redfällen.
Danach getrennte Redzyklen: Endpoint-/Binding-/Settings-/HTTPfehler; Send/Callbacks/Poll;
Status-/Text-/Action-/UTF8grenzen; Inspect/Cancel; Timeout/Redirect/Retention/kein Retry.
Jeder Zyklus erhält Quellmanifest und echte Assertion vor Green. Spezifische Settingsliste
zunächst instructions für ModelResponse, fremde Settings explizit ablehnen statt still
ignorieren. Kein AgentTask-Support ohne externe Agentlaufzeit, keine Tools/Compaction.

Transport-/Operationsfristen bleiben endlich (30 Sekunden Header, monotone Gesamtfrist,
1 Sekunde Pollstart), Körper begrenzt lesen; keine unbeschränkten Responses/Registrys.
Send persistiert OperationId vor Polling auch bei sofortigem Endresultat. Finale Replys
bewahren Conversation und übernehmen keinen anderen Sitzungsschlüssel. Unknown bei
fehlendem Identitäts-/Endbeleg, Storagecallbackfehler oder verlorenem Netzwerkabschluss.
Konfigurations-/HTTPdetails bleiben aus öffentlichen Ausnahmen/InnerExceptions entfernt.
Open/Reopen, Protokollinterpretation und Ablauf bleiben getrennt, Full-Guard ohne Ausnahmen.
Offene externe Fragen (Kontozugang/Modelle/Retention) blockieren nur Liveabnahme T105;
der deterministische Fixturevertrag kann unabhängig implementiert werden.

Nächster Schnitt vor Erweiterung, gemessen 2026-10-29T07:50:17Z:
Open/Reopen 2/2 grün, Manifeste openai-open-{red,green}. Transport setzt frische Header,
benutzt einzelne POST/GET und liest JSON; Validierung, bereinigte Fehler und Identitätsprüfung
fehlen noch ausdrücklich. Protocol.Conversation übernimmt id ungeprüft; Reopen ignoriert
zurückgelieferte Identität. Diese Lücken sind am aktuellen Code geprüft. Vor Safe-Transport-
Green zuerst InvalidEndpoint-/Binding-/Context-/Response-/HTTPfehlerfälle. Normale Endpunkte
mit oder ohne Endslash müssen denselben Basispfad behalten. HTTPfehler geben ausschließlich
stabile Kategorien, keine Responsebodies/Secrets/InnerExceptions; 404 beim Reopen ist expired,
401/403 auth, 429 rate, Redirect/Server-/JSON-/Identitätsfehler provider unavailable. Bestehender
Bindingstand wird nie ausgetauscht. Kein neues Feature oder ungeklärte Frage in diesem Schnitt.

Status-/Grenzschnitt vor Implementierung, gemessen 2026-10-29T07:54:30Z:
31 Adapterfälle grün. OpenAiOperation persistiert ID vor Poll; Protocol.Reply behandelt
bisher jeden nichtpending Status als Erfolg und liest message/output_text. OperationId/
GET-Identität und finale UTF8grenze sind noch ungeprüft. Erweiterungspunkt: eigener
OpenAiResponse-Interpreter, damit Transport, Drahtinput und Resultpolitik klein bleiben.
Red: failed/incomplete/cancelled/requires_action/unbekannt, leere/überlange Ausgabe,
clientseitiger function_call, fehlende Operations-ID, falsche GET-ID, Providercontextfehler.
completed/failed/incomplete/cancelled sind native Endbelege; required_action/unbekannt
bleiben Unknown. completed mit function_call ist UnsupportedProviderAction, aber bestätigt
serverseitig beendet; keine lokale Toolaktion. Vorgängerkonversation bleibt unverändert.
Begrenzte Ausgabe nie kürzen; kompletter nativer Endbeleg darf trotz Outputlimit erhalten
bleiben. Transportkörper-/Timeoutgrenzen folgen eigenem Red danach, noch keine Freigabe.

Frist-/Körperschnitt vor Implementierung, gemessen 2026-10-29T07:56:23Z:
43 Adapterfälle grün, Status-/UTF8-/IDregeln sind im separaten OpenAiResponse-Interpreter.
Transport hat noch InfiniteTimeSpan am Client ohne eigene Headerfrist und unbeschränktes
JsonDocument.ParseAsync. Operation hat nur Poll-Delay; Call.Timeout wird noch vom Kern,
nicht vom direkten Adapteraufruf garantiert. Callbackawait ist unbeschränkt. Diese Lücken
werden vor Freigabe geschlossen, kein aktueller Partialstand wird als vollständiger Adapter
committet. Red mit explizit ausgelösten TimeProvider-Timern: Header30s, gesamte Callfrist,
nichtkooperativer Callback und begrenzter JSONkörper. Neues optionales MaximumResponseBytes
(default 32 MiB) begrenzt Transportmetadaten unabhängig vom inklusiven fachlichen Outputlimit.
Produktions-HTTP und Callbackwartezeit erhalten Token und endliches WaitAsync; kein Retry.
Ein überschrittener Rohkörper vor Endbeleg bleibt OutputLimitExceeded/Unknown. Headerfrist
endet nach Headern, gesamte Operation bleibt bis abschließendem JSON/Callback/Poll begrenzt.

Inspect-/Cancelschnitt vor Implementierung, gemessen 2026-10-29T07:59:35Z:
47 Adapterfälle grün. Transport begrenzt Header/Körper, Operation begrenzt Callback/Gesamtfrist.
Inspect/Cancel sind weiter Signaturstubs. Interpreter kennt bereits terminale Statusregeln;
für lesende Inspect-Abfrage muss queued/in_progress als Running/InProgress sichtbar sein.
Erweiterung: eigener OpenAiObservationAccess für bekannte Operations-IDs, ausschließlich
GET für Inspect, ein Cancel-POST plus GET-Endprüfung/Poll bei Cancel, endliche 600s-Frist.
Kein Callback/Storecommit, kein neuer Send, keine Conversation aus Response-ID erfinden.
Redfälle: fertige/laufende Inspectzustände, Cancelack ungleich Endbeleg, Native-ID-Mismatch,
abgelaufene Operation, invalides Argument. Keine Freigabe von Slots durch diese Reads.

Read-Retryschnitt vor Implementierung, gemessen 2026-10-29T08:01:32Z:
53 Adapterfälle grün. Transport liefert HTTPfehler bisher sofort zurück. Ein Retry-After
auf 429/503 bei sicheren GETs soll innerhalb der vorhandenen Gesamtfrist berücksichtigt
werden, niemals auf ausführenden POSTs. Bestehender Transportloop wird dafür in eine
kleine native Read-Retrypolitik ausgelagert; keine generische Retrybibliothek/SDK.
Redfälle mit Timerbarriere: Delta- und HTTP-Datum-Retry-After, GET genau zweimal nach
explizitem Timerfeuer; POST429 bleibt ein Aufruf/NotStarted und wird als Positivregression
bezeichnet. Defaultwartezeit 1s, Datum gegen TimeProvider.UTC, monotone Gesamtfrist bleibt
unverändert. Antwort vor erneuter Wartezeit disposen, max Timergrenze beachten.

Zusatzprüfung vor Sicherheits-Green, gemessen 2026-10-29T08:04:50Z:
56 Fälle und Full-Guard 154 Dateien/156 Typen grün. Direkte Durchsicht von
OpenAiFailure.Http, OpenAiTransport.SendAsync, OpenAiResponse.Identity/Reply und
OpenAiSessionProvider.SendAsync zeigt: HTTP400 wird nicht nach bekanntem Contextcode
unterschieden, whitespace Operation-ID passiert Length>0, explizite abweichende
response.conversation wird ignoriert, direkte Calls können InfiniteTimeout/Outputlimit0
übergeben. Fünf zusätzliche zuerst rote Sicherheitsfälle prüfen diese konkret benannten
Grenzen. Entscheidung: nur bekannte HTTP400-Codes aus begrenztem JSON lesen, keine freie
Fehlermeldung übernehmen; übrige BadRequest sind ExecutionFailed/NotStarted. Native
ID muss nichtwhitespace sein; explizit widersprüchliche Conversation ist Unknown.
Calllimits vor Send prüfen. Kein Modell-/Capabilitykatalog und keine neue Vertragshistorie.
