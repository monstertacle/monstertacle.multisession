# Lokale Konfigurationsverfügbarkeit vor Start der Worker

Gemessen2026-10-29T12:01:09Z, Ausgang1bf65f1. T061/T066, Provider-Edge-Cases aus spec.md.
RuntimeLifecycle.cs SHA2560f3afb486ca6bf75bf26738665a4ad317c5784bebe0dc0a14d465100f5487429;
SessionSnapshot.cs9450d28a3ae898f3c480a0f0292c208a084f8102b246155e1f712d5cc5e4b9d3;
ISessionStore.cs29c429211153c696405f305fc29bc5779678e987548b094734681eb083a0f681.

## Bestand und konkretes Loch

RuntimeLifecycle.Start startet Storebootstrap vor Facaden/Workern. SQLbootstrap stellt
Unknown sicher her, kennt aber keine Hostregistrierungen. Dispatcher bemerkt fehlende/
unpassende Registrierung erst nach Reservation und finalisiert dann Failed/NotStarted
mit ContinuationAvailable=false. Eine noch nie versendete persistierte Anfrage sollte
bei lokal bekannt fehlender Registrierung stattdessen Queued bleiben und deren Session
blockiert werden. Native Versions-/Executionkindprüfung gehört vor Workers. Kein Modell-
Featurekatalog erforderlich: opaque Modelle/Settings bleiben gefroren, erst nativ erkennbare
Modell-/Credentialfehler bleiben T034/T080 und dürfen nicht als vorher geprüft gelten.
ProviderCatalog validiert/friert aktuelle Konfiguration bereits; keine neue Discovery/DI.

Ein bloßes ProviderUnavailable reicht nicht als Herkunft: native Endbelege können denselben
Blockgrund setzen. Startup darf einen nachweislich unbrauchbaren nativen Head niemals nur
wegen wieder vorhandener Registrierung freigeben. Daher additive boolConfigurationUnavailable
im SessionSnapshot als technische Ursache. Bestehende Positionals bleiben kompatibel;
JSONschema benötigt keine Änderung, Defaultfalse bewahrt ältere native Sperren.

## Entscheidung vor Umsetzung

ISessionStore erhält eine gezielte atomare Konfigurationsavailabilityoperation (zehnte
Writeoperation, kein generischer Update-/Service-Locatorport). Gemeinsame reine
SessionAvailabilityPolicy entscheidet über Flag/Block: fehlende Konfiguration blockiert
Open ohne bestehende andere Sperre; RecoveryRequired bleibt priorisiert. Wieder gültige
Konfiguration löst ausschließlich selbst markierte Konfigurationssperren. Native Sperren
mit Flagfalse bleiben bestehen. SQL/kontrollierter Writer verwenden dieselbe Regel und
stempeln tatsächliche Änderungen; Bindingrevision und Fälle bleiben unangetastet.

RecoveryCoordinator liest StoreSessionpages, vergleicht ProviderId, Adapter/Format und
explizite Executionkind-/Workspacefähigkeiten ordinal, schreibt Availability vor Workers.
Keine Herstellerverzweigung oder native I/O unter SQLlock. Keine Bindungs-/Modelländerung
als Ersatz. Auf Fehler kein Workerstart. Entfernte Profile ändern keinen bereits gültigen
gefrorenen Kontext; Profilname ist keine implizite Modellallowlist.

Tests zuerst rot: reine Statusmatrix einschließlich nicht aufhebbarer nativer Sperre und
Recoverypriorität; echte öffentliche Runtime mit SQLite, getrenntem nicht löschendem
Testleasewrapper und äußerem privatem Cleanupowner. Missingreg/Format-/Fähigkeitsfehler
lassen gespeicherte Anfrage Queued, unabhängiger Provider arbeitet im freien Slot.
Wiederkehrende Registrierung kann nur eigene Konfigurationssperre lösen und ursprüngliche
Kennung fortsetzen. Wrapper beweist keinen nativen Lifetime-Lock; Rechte/Kill bleibenUS4.
Native Auth-/Modellverfügbarkeit wird nicht erfunden; FolgeumfangT034/T080 bleibt konkret.
Volle Qualität/Guard/CI nach Green; kein implizites Löschen des Teststores durch RuntimeStop.
