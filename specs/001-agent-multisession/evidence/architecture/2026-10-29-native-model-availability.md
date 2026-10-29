# T121: Modell- und Zugangsprüfung als ausdrückliche Providerfacette

Gemessen: 2026-10-29T21:40:52Z. Basis1b2aba6; spätere Linuxänderung berührt diese Module nicht.
OpenAiSessionProvider.cs SHA256 ed2db77c0f9e77f73bada4231f13fd256fd69e33167fe0374217466f0d54e131.
OpenAiProtocol.cs 9d4b7cce29b6936f4ada3e71fc5df0df00f41d4007f60c252320ea66cfba0bd5.
GeminiSessionProvider.cs 09d4e639cdaa08e9110fd9db2f93287654953ee0006afa71fa36dd14f735409c.
GeminiProtocol.cs 7e7af74c29af52d704f7090a00135a46a058a860c8f46926c2b305273bd1c428.
NativeHttpTransport.cs 74508d4080488de8427830f84f889ddc53b04ddc8fe773fccee4fbde06ef6467.
RecoveryCoordinator.cs bf50ce01e66ab431fbb69c691b164bb6337e054cdd4a7d12090217a6d593b90a.

## Befund und Quellen

Der neue Coordinator prüft vorhandene native Bindings über den bestehenden Reopenport.
Reopen liest aber Conversation/Interaction und prüft keinen ausgewählten Modellzugang.
GeminiProviderContractTests garantiert außerdem ausdrücklich HTTP-freies Lazy-Reopen.
Diese Facette nicht still um zusätzliche HTTPmetadatenänderungen erweitern.
NativeHttpTransport besitzt bereits frischen Credentialresolver pro Versuch, Redirect-
schutz, Header-/Bodygrenzen und sichere GET-Retryregeln. Beide Provider teilen diese
Transportquellen; kein zusätzlicher Client oder Credentialweg erforderlich.

Primärquellen am genannten Zeitpunkt geprüft:
- [OpenAI Retrieve model](https://developers.openai.com/api/reference/resources/models/methods/retrieve): GET /models/{model}, Modelmetadata mit id/object; ausgewählten Zugang prüfen, keine Generation.
- [Gemini models.get](https://ai.google.dev/api/models): Metadaten eines bestimmten Modells; deren Beispiel verwendet v1beta und ist alleine kein Beleg für unseren stabilen Endpoint.
- [Offizielle Discovery v1](https://generativelanguage.googleapis.com/$discovery/rest?version=v1): resources.models.methods.get definiert flatPath v1/models/{modelsId}, GET und resource Model. Damit kein Wechsel zu v1beta nötig.

## Entscheidung vor Umsetzung

Optionale generische Facette ISessionAvailabilityProvider.ValidateAvailabilityAsync
(ProviderSessionContext, CancellationToken) für ausdrücklich lesende Modell-/Zugangsprüfung.
Native Adapter implementieren sie; dritte Adapter dürfen dieselbe Zusicherung bereits
innerhalb Reopen erbringen. Coordinator prüft die Facette polymorph vor Reopen, ohne
Hersteller-/Modellnamenszweig. Existierende ISessionProvider-Reopenverträge, besonders
Lazy-Gemini, bleiben unverändert. Keine neue Capabilityboolean-Dopplung erforderlich.

Gemeinsamer NativeModelAccess nutzt ausschließlich vorhandenen Transport, frisches
Hostcredential, GET models/<escaped ModelId>, monotone endliche Gesamtfrist und native
JSONvalidatoren am Protokollrand. OpenAI verlangt Modelobject und nichtleere id,
Gemini einen nichtleeren models/-Ressourcennamen. Zurückgelieferte Aliasmetadaten werden
nicht als Ersatzmodell gespeichert; Context.ModelId und Binding bleiben unverändert.
Kein vollständiger Katalog, keine automatische Ersatzwahl und keine kostenpflichtige
Probeantwort. Undefinierte/leere/dot-Segment-Modellkennung vor HTTP ablehnen. 404 beim
Modelmetadatenlesen ist ProviderUnavailable, nicht ProviderSessionExpired; Auth bleibt
AuthenticationFailed. Der Coordinator speichert die bereinigte Ursache im bestehenden
Availabilitywrite und blockiert nur diese Sitzung. Ein positives Metadata-GET ist ein
Zugangs-/Existenzbeleg, keine Garantie künftiger Generierung, Quote oder aller Features.
Die tatsächliche Integration mit Testkonten bleibt unabhängig T105.

## Redplan und Regression

Neue Interface-/Adapter-Signaturen zuerst verhaltenslos ausführbar machen. Dann echte
HTTP-Contracttests für erfolgreichen Read/Frischcredential/escaped Auswahl, fehlendes
Modell, Auth, unbrauchbare Metadata und ungültige Kennungen vor HTTP zuerst rot.
Ein generischer Runtimeporttest lässt Validierung AuthenticationFailed liefern und
verlangt Blockursache sowie null Reopen/Send; vor Coordinatoranbindung fachlich rot.
Anschließend kleinster Reader/Protocolvalidator und Coordinatoraufruf. Vorhandene
Reopen-/Lazy-/Readretry-/Redirect-/Recoverytests müssen bestehen; nur tatsächlich neu
benötigte Modellmetadatenantworten an expliziten synthetischen HTTPfixtures ergänzen,
keine Assertions entfernen. Privater Livezugang wird zu keiner Zeit aufgerufen.
Full-Codemetr/87-%-Gesamtgate, native CI und Pakete für neuen Stand neu prüfen.
