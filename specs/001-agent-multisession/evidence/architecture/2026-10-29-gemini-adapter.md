# T033/T039: stabile native Interaktionskette

Created: 2026-10-29T08:28:01Z
Owner: ausführende Gesamtimplementierungssitzung, feature/reserved-dispatch-contract.
Basis: fd3e7e3 mit gemeinsamem internem HTTPtransport; T116 operationsgebundene Recovery
lokal umgesetzt. Alle früheren nativen Adapterfälle und volle Qualität grün.

Bestandsanalyse: Providers.Gemini enthält bisher nur das csproj/Commonreferenz, keine
Ausführungs-/Binding-/JSONimplementierung. Common kennt ausschließlich generische
Snapshots/Providerports; Dispatch persistiert neue native Beobachtungen vor Bestätigung.
NativeHttpTransport/Endpoint/Body/Errors/ReadRetry besitzen bereits geprüfte endliche
HTTPregeln und erlauben Headername/Scheme sowie lokalen HTTP400-Parser. Sie werden in
Gemini als interne Compile-Link-Quellen eingebunden, keine OpenAIreferenz und keine
HTTPtypen im Commonkern. Bestehende native HTTPquellen siehe den unmittelbar vorherigen
Refactor-/Sourcehashbeleg architecture/2026-10-29-shared-native-http.md.

Tatsächlich abgerufene Primärquellen: https://ai.google.dev/api/interactions-api-v1,
https://ai.google.dev/static/api/interactions-v1.md.txt sowie
https://ai.google.dev/gemini-api/docs/interactions-overview. Stabile /v1-Routen und
serverseitige previous_interaction_id sind dokumentiert. Der aktuelle Ressourcenvertrag
liefert steps/model_output/content/text; keine Übernahme alter outputs/v1beta-Beispiele.
Zulässige feste ModelResponse-Einstellung zunächst system_instruction; unbekannte Felder
werden abgewiesen. Keine eigene Tool-/Verdichtungs-/Continuation-Token-Schleife. Opaque
Modellkennungen bleiben Hostwahl, keine dokumentierten Modellbeispiele als Modellkatalog.

Entscheidung: GeminiSessionProvider mit eigenen GeminiOptions/BindingPolicy/Protocol/
Response/Operation/ObservationAccess und Recoveryport. ModelResponse unterstützt, keine
AgentTask-Zusage aus einer Modellantwort. Open ist ausschließlich lokales lazy Binding.
SessionReference wird erste native Interaktions-ID, ContinuationState ausschließlich
letzter bestätigter completed-Kopf. Erster Send hat keinen previous_interaction_id,
Folgesends verwenden nur bestätigten Kopf. Erhaltene erste ID wird vor weiterem Ablauf
als SessionEstablished bestätigt, jede neue Operation als OperationAccepted. Auch bei
sofortigem Endresultat beide Callbackgrenzen beachten. Keine Credentials in Binding.

Gewöhnliches Reopen prüft denselben bereits bestätigten Kopf per GET; ein reines lazy
Binding ohne frühere native Übergabe bleibt lokal. Ein bekannter erster Bezug ohne
bestätigten Kopf ist kein wieder sendbares lazy Binding und wird vor HTTP abgewiesen.
Fehlerhafte erste native Ausführung darf keine spätere stillschweigende neue Kette erzeugen.
Native completed-Evidenz bestimmt den neuen Kopf auch dann, wenn lokale Textaufnahme
scheitert; kein Rückfall auf den alten Kopf nach Outputlimit. failed/incomplete/cancelled
erzeugen keinen neuen completed-Kopf. T043 muss unbrauchbare Bindungen zusätzlich in der
Session-Lebenszyklusprojektion blockieren; native Send/Reopen verweigern sie schon lokal.

IExecutionRecoveryProvider prüft den gespeicherten ungewissen Operationsbezug statt nur
den alten Kopf. Ersten bereits persistierten Bezug darf er als bekannte Identität prüfen,
aber nicht neu eröffnen. Native ID muss passen; vorhandenes previous_interaction_id darf
dem bestätigten Vorgänger nicht widersprechen. Nur completed kann neuen bestätigten Kopf
liefern; andere sichere Endzustände dürfen nur mit noch gültigem altem Kopf fortsetzen.
Ohne prüfbaren Bezug bleibt Recovery gesperrt, Close ist weiterhin möglich. Keine Posts
in Reopen/Recovery. Native Statusinspektion verändert keine lokale Reservierung.

Rote Zyklen: zuerst lokales Lazy-Open/Reopen und bestätigtes GET, dann erste/Folge-Sends
mit zwei awaitbaren ID-Grenzen und unveränderten Settings; danach Unknown-/Action-/Status-
und UTF8fehler, Inspect/Cancel, Operation-Recovery/Lineage. Anschlussprüfungen verwenden
die vorhandenen HTTPfristen/Body-/GET-Retryregeln unter x-goog-api-key, kein POST-Retry.
Native 400-Fehlercodes sind laut Referenz URIartig ohne erschöpfenden Kontextcodekatalog.
Keine message-Textheuristik erfinden; unbekannte Codes bleiben bereinigtes ExecutionFailed.
Konkrete Gemini-Kontextcodeabnahme bleibt T034/T105 (Owner dieselbe Sitzung: dokumentierten
Code oder freigegebenen nativen Fehlerbeleg beschaffen, dann Red-Fixture/Mappings prüfen).
Dies blockiert die vollständige T034-/Herstellerfreigabe, nicht die übrige deterministische
Adapterimplementierung. Keine weiteren ungeklärten Implementierungsfragen für diese Zyklen.

Sendzyklus vor Implementierung, gemessen 2026-10-29T08:33:06Z:
Vier Open-/Reopen-Fälle nach fachlichem Red grün. GeminiSessionProvider hat weiter
Send-/Inspect-/Cancel-/Recovery-Stubs. BindingPolicy trennt wirklich lazy von bekanntem
Bezug ohne bestätigten Kopf; Reopen liest nur completed und bewahrt Root/Head getrennt.
Jetzt Red für drei aufeinanderfolgende neue Nachrichten (dritter previous-Bezug muss Head,
nicht Root sein), Systeminstruction jedes Mal, x-goog-api-key frisch; SessionEstablished
vor OperationAccepted bei erster Interaktion, Callbackbarriere vor Poll, Callbackfehler
ohne neuen Head. NativeHttp bleibt geprüftes Infrastrukturverhalten, kein POST-Retry.

Ergebniszyklus vor Implementierung, gemessen 2026-10-29T08:35:37Z:
Acht Gemini-Fälle grün, drei erste/weitere Eingaben und zwei Callbackarten geprüft.
Response.Reply behandelt noch alle nichtpending Zustände als Erfolg und publiziert
beliebigen neuen ID-Kopf. Jetzt Red für failed/incomplete/cancelled/requires_action/
requires_continuation/unbekannt, leere/überlange Ausgabe, widersprüchliche Vorgänger-ID,
wiederverwendete alte Operations-ID und Fehler bei erster lazy Ausführung. completed
bleibt nativer Kopfbeleg auch bei lokal fehlender/überlanger Textausgabe; dieser Kopf muss
im Failure-Binding erhalten bleiben. Erste sichere Fehlausführung speichert bekannten
Root ohne completed-Kopf, spätere Sendverweigerung ist lokal NotStarted/Expired statt
einer neuen Wurzel. T043 ergänzt die vorgelagerte Sitzungssperre. Keine neuen Vertragstypen.

Recoveryzyklus vor Implementierung, gemessen 2026-10-29T08:38:20Z:
20 Gemini-Fälle grün (Ergebnisred 11 Fehler/1 vorhandene Positivgrenze). Der Parser nutzt
bestehende ProviderReplyPolicy für Textgrenzen und bewahrt completed-Kopf auch im lokalen
Fehler. Unbrauchbare erste Bindung sendet keine neue Wurzel. Recoveryport ist weiterhin
Stub. Jetzt direkte native Redfälle: bekannter neuer completed-Kopf, erster bereits
bekannter SessionEstablished-Bezug mit/ohne getrennte Operations-ID, fehlende neue
Operations-ID bei altem Kopf, pending/action bleibt gesperrt, widersprüchlicher Vorgänger.
Sicher fehlgeschlagene Operation darf nur nach zusätzlicher GET-Prüfung des alten completed-
Kopfes dorthin zurückkehren; nie aus Unknown oder einem fehlenden ersten Bezug ableiten.
Begrenzte Frist umfasst sämtliche Reads, keine POSTs/Toolaktionen/Historywiederholung.

Inspect-/Cancelzyklus vor Implementierung, gemessen 2026-10-29T08:41:49Z:
32 Gemini-Fälle grün, einschließlich 12 zuerst roter operationsgebundener Recoveryfälle.
Recovery liest neuen bekannten Kopf, fehlende Belege bleiben gesperrt und sichere native
Fehler erlauben alten Kopf erst nach zweiter Prüfung. Inspect/Cancel sind weiter Stubs.
Jetzt Red für pending/completed Inspect, ein Cancel-POST plus Endstatus-GET, Expiry,
fremde Operations-ID und fehlende ID vor HTTP. Eigener ObservationAccess mit vorhandenen
Transport-/Responseports; kein lokaler Storezugriff oder neues Binding aus reiner Inspektion.

Konfigurations-/Transportzyklus vor Erweiterung, gemessen 2026-10-29T08:43:51Z:
38 Gemini-Fälle grün. Gemeinsame HTTPvalidierung greift bereits auf GeminiOptions,
aber ein ausdrücklich konfigurierter v1beta-Pfad wird noch nicht abgewiesen und direkte
Calls validieren Timeout/MaxOutputBytes noch nicht vor I/O. Zuerst entsprechende Redfälle;
bestehende generische Endpoint-/Bindings-/Contextregeln als Regression. Zusätzlich die
bereits geprüften Header-/Gesamt-/Callback-/Bodygrenzen und GET-Retry-After an der zweiten
kompilierten Transportkopie wiederholen, ohne neue Reds dafür zu behaupten. Keine volle
Contextcodeabnahme aus unbekannten URIcodes ableiten. Danach komplette native Qualität
und Full-Guard für beide Herstellerpakete, keine Scopeausnahmen für Sharedquellen.
