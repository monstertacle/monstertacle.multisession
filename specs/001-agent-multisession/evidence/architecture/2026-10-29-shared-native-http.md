# Native HTTP: gemeinsame Infrastruktur ohne Kerntransport

Created: 2026-10-29T08:23:32Z
Owner: ausführende Gesamtimplementierungssitzung, feature/reserved-dispatch-contract.
Basis b795a6d, 362 Tests/96,0417 Prozent lokal grün. Kein neues fachliches Verhalten.

Deep-Dive: OpenAiTransport besitzt HttpClient/Frischcredentials/Header30s/Bodylimit und
sicheren GET-Retry. OpenAiResponseBody/ReadRetry sind bis auf Namen vollständig generisch.
OpenAiFailure besitzt generische HTTPkategorien, aber der HTTP400-Interpreter hängt an
OpenAiResponse.NativeError. OpenAiValidation.Endpoint ist eine generische HTTPS-/Loopback-
Normalisierung. Operation/Conversation/Responseparser sind dagegen herstellerspezifisch.
Gemini benötigt denselben überprüften Transport mit x-goog-api-key statt Bearer und eigenem
BadRequestcode-Interpreter; eine Abhängigkeit Gemini→OpenAI oder HTTP im Commonkern wäre falsch.

SHA256 OpenAiTransport 6435d342cdf0f052573670031e91bdd2b53b42fae67595e3e2070f632488e63b;
ResponseBody 672f867f6ef3449fc4d13837912a90ef97b4ef23c4ab8ad7c525138239ed92b8;
ReadRetry ad5047e18d958f47fac1a625645cc1b6dc06182c2458974a6604b8e64c705a28;
Failure faccd4627705d0ccd770fd14580336b19a8f9857704b72bb565ad042a5daaac3;
Validation 9004acc6bf438bda9ceed56214604baffcb31393fe963529ef9ed9299e42dafa.

Entscheidung: interne Infrastrukturquellen in src/Shared/NativeHttp, zunächst ausschließlich
ins bestehende OpenAI-Projekt per Compile-Link aufgenommen. Gemini bindet sie erst mit
seinem Implementierungszyklus ein. Keine siebte Assembly/kein neues NuGetpaket, keine
Common-HTTPtypen, kein Herstellerzweig. NativeHttpConnection enthält bereits normalisierten
Endpoint, Credentialreferenz, Headername/Scheme und Bodylimit. HTTP400-Interpretation ist
ein explizit injizierter lokaler Parser. Operation-/Binding-/Resultprotokolle bleiben im Adapter.
Gleiche Fristen/Poll-/Bodydefaults besitzen eine interne gemeinsame Definition.

Refactor startet mit 240 Unit/120 Contract und voller lokaler Qualität grün. Bestehende
OpenAI-61-Fälle sichern Methoden/URIs/Header, Fehler, native IDs, Fristen, Callback/GET-Retry
und Ausgabe unverändert ab. Danach komplette Unit-/Contractregression und Full-Guard;
native Qualität nach Gemini wieder für beide Assemblies, Scope unverändert. Keine Baseline,
keine Quellausschlüsse. Gemeinsame Quellen zählen in jeder kompilierten Produktassembly
zur Coverage. Rein kosmetische EOF-Leerzeile in ProviderCapabilities wird bereinigt.
Keine ungeklärte architekturrelevante Frage für diesen verhaltenserhaltenden Schnitt.
