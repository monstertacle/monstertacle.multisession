# Native Providerverträge im ValidationHost

Bestandsprüfung 2026-10-29T19:25:20Z gegen e051325 und den noch uncommitteten
reinen Diagnose-Schnitt. NativeWireFixture/NativeWireReply, ScriptedHttpHandler,
FixtureCredentials, NativeProviderHost und die öffentlichen OpenAI-/Gemini-
Provider wurden gelesen. Die bisherigen Vertragstests verwenden bereits
getrennte synthetische Conversations und Interactions. Gemini bestätigt den
jeweiligen Chain-Head; der nächste POST muss genau darauf verweisen. OpenAI
behält die Conversation. Die Handler führen weder Netzwerke noch Tools aus.

SHA256 vor Erweiterung:
NativeWireFixture.cs 77d63dd3e45cc4e113db64caa0d5ce11040ffb3e4bffb4626dfbba77c374b67c;
NativeWireReply.cs c99e204eb8eb5c0b73130792d097e46fefd963cf8c1d5471a389173b7fc681bd;
ProbeHost.cs 22331b81a14be1e5291ec31233fe571aae5cd1e89cee388c88af92ce2987abae;
ScenarioRunner.cs c69af57e29ee2c1e380f0ed9b26707c3fb6498565a98cd64576949e35d025afd.

Entscheidung vor Implementierung: dieselben BCL/Common-basierten Wire-Fixtures
als Compile-Links einbinden; keine Testassembly referenzieren. Eine kleine
Host-Probe komponiert die echten Providerpakete mit diesen kontrollierten
HTTP-Handlern, dem vorhandenen ProbeHost und einem nativen SQLite-Store.
Zwei erste Sends werden bis zur Parallelitätsprüfung gehalten. Erste und
Folgeanfragen müssen exakte neue Inputs, unveränderte native Identitäten und
getrennte History behalten. Eine zusätzliche isolierte OpenAI-Probe liefert
`context_length_exceeded`; der öffentliche Request muss ProviderContextLimit
ohne privaten Meldungstext und ohne Wiederholung behalten. Vorhandene
OpenAiResultTests/OpenAiProtocolSafetyTests belegen diese Abbildung bereits.
Kein neues Providerverhalten ist vorgesehen.

Zuerst wird die fehlende CLI-Route mit unabhängigen Datenbankzählungen rot
geprüft. Dann folgt die minimale Komposition, gezielte Regression und Full-Guard.
Ungeklärte Gemini-Kontextgrenzen werden nicht durch erfundene Codes verdeckt:
Die am selben Tag geprüfte offizielle Fehlerreferenz
https://ai.google.dev/gemini-api/docs/api-errors nennt keinen eindeutigen
Kontextgrenzen-Code. Diese Teilabnahme bleibt T034/T094/T105 zugeordnet;
PASS dieser kontrollierten Probe ersetzt weder sie noch Live-Abnahme.
