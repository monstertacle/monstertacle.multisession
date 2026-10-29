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

## Kanonische Gemini-Modellressource: Nachzug vor Korrektur

Gemessen 2026-10-29T21:53:57Z, Standd2a0c2f. GeminiSessionProvider SHA256
0fa5900746de4851c83a76385f077c2da99e88888ce7da376f3b708e76620f19,
GeminiProtocol42f8cf33a831e03e63d33ea71596d662aab4ea3548e47b85be187cfc7b5b2509.
Die [stabile v1-Referenz](https://ai.google.dev/api/interactions-api-v1) nennt ModelOption
auch als models/<id>; die bereits geprüfte Model-GET-Route erwartet models/<id> als
Ressource. Unser neuer gemeinsamer Reader stellt models/ vor eine escaped opaque
Kennung. Für bereits kanonisches Gemini models/<id> entsteht deshalb models/models%2F<id>.
OpenAI-opaque Deploymentkennungen dürfen dagegen nicht umgeschrieben werden.

Bestehender NativeModelAccess bleibt Transport-/Validierungsautorität. Nur der
Gemini-Protokollrand entfernt ein bereits vorhandenes models/-Ressourcenpräfix für
diesen Metadatenaufruf, nie für Context.ModelId/Generation/Binding. Bare Aliasse bleiben
gleich. Leere oder dot-Segmente nach dem Präfix müssen vor HTTP abgewiesen werden.
Zwei positive Auswahlvarianten plus drei unzulässige kanonische Ressourcen zuerst
gegen den aktuellen Reader ausführen: mindestens Präfixverdopplung und falsche
Kategorie/HTTPaufruf müssen tatsächlich rot sein. Danach kleinste Adapterabbildung,
alle native/Transport-/Startupregressionen, Fullguard/Gesamttor und neuer identischer
Stundenkandidat. Keine Änderung der Persistenz oder Öffnungs-/Lazyverträge.
