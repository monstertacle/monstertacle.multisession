# T030/T036: generische Registrierung und Profilsnapshots

Created: 2026-10-29T07:42:16Z
Owner: ausführende Gesamtimplementierungssitzung, feature/reserved-dispatch-contract.
Basis: 99b1ffd, Runtimequalität 279 Tests/95,4674 Prozent lokal grün.

Deep-Dive: ProviderRegistration/AgentProfile/ProfileSettings sind unverhaltete Records.
SessionCatalog-Konstruktor erzeugt zwei FrozenDictionaries direkt aus Hostlisten; doppelte
IDs werfen rohe ArgumentExceptions, fehlende Registrierungen werden erst bei Create erkannt,
leere IDs/Adapter/version/model/revision nicht validiert. Nested ProfileSettings.Values
bleibt bis zum Session-Create hostveränderbar. ProviderSessionContext friert Werte erst
beim Create ein. SessionRuntime friert Nestedwerte vorab ein, aber dupliziert diesen Schritt
und prüft Registrierungen nicht. RequestDispatcher/RecoveryService frieren Registrylookups
selbst ein; ihr Ausführungssnapshot bleibt unverändert. Kein ProviderCatalog vorhanden.

Quell-SHA256: SessionRuntime e03e159a05b385e604287a0cf9dd33b71249ebcb71095fcfcc9c1be73354f46f;
SessionCatalog b98cc7f1fcd46ed5f160034bd98532a979e58fec02e7476b82148c3e0323ceff;
ProviderRegistration 5afb2b10f58f25fea6b5af5388d49bc36de21791954b851e51e362bc1b64aed8;
AgentProfile 586f4926862e6114d281550e2fa25d5e79c51e7f10da48a5188a2a34b51abd5c;
ProfileSettings c4df7340d8f174ac8573fcb16da2fcd53bd6de6f9e310c9eb1a620d53b08514f.

Entscheidung: eigener kleiner ProviderCatalog validiert explizite Registrierungen/Profile
und friert Listen/Nestedsettings ordinal ein. SessionCatalog und Runtimekonstruktion
verwenden ihn. Keine Herstellerwahl oder HTTP-Eigenschaften im generischen Vertrag.
ProviderId/AdapterId/ProfileId/ModelId/Revision müssen nichtleer sein, Version positiv,
Adapter vorhanden, IDs je Art ordinal eindeutig, Profilreferenz existent und ExecutionKind
bekannt. Zwei Profile dürfen dasselbe opaque Modell wählen; Groß-/Kleinschreibung bleibt
signifikant. Fehler sind bereinigtes InvalidConfiguration ohne Hostwerte/InnerException.
Der Host verantwortet nichtgeheime Settings; spezifische zulässige Feldlisten prüfen erst
native Adapter. Kein allgemeiner heuristischer Secretklassifikator.

Endpoint/CredentialReference bleiben native Adapterkonfiguration, da auch nicht-HTTP-
Agenten registrierbar sein müssen. Frische Credentialauflösung/HTTPS/Loopback/Redirect
werden in T032–T040 vor Green an HTTP-Fixtures geprüft. T030/T036 werden daher erst nach
allen dazugehörigen nativen Konfigurationsfällen vollständig abgeschlossen. SettingsRevision
im gespeicherten Kontext folgt dem separaten T031-Snapshotzyklus; hier keine Feldmigration.

Redplan: SessionCatalog-Konstruktor akzeptiert ungültige Registrierung/Profile bisher;
fehlende Referenz und rohe Duplicate-Exception statt typisiertem Fehler, mutierbare
Profilwerte vor Create. Positivregression zwei Profile/ein Modell, ordinal getrennte IDs,
keine Provideraufrufe bei Konfigurationsfehler. Erst beobachtete Assertions dann Catalog,
öffentliche read-only Konfigurationslisten separat prüfen. Regression alle Unit/Contract,
Full-Guard, komplette native Qualität. Keine ungeklärte Frage für diesen generischen Schnitt.
