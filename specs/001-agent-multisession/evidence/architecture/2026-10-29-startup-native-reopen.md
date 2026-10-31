# Native Wiederaufnahme vor Freigabe persistierter Queueköpfe

Gemessen: 2026-10-29T21:24:37Z. Basis: 5774e4f.
RecoveryCoordinator.cs SHA256: 6bc2ae3d63e781283cd683429a6bb4ebac21b9bcc05d80429468e303ddd996f6.
SessionAvailabilityPolicy.cs: 5500e2409a778f1b66eaebaf0eafe57d1da5387d6dec052fc2582c55c9b02ab1.
SessionSnapshot.cs: 6719a5a13d77049b1e8af4bc27b818627386ac42910dd942fc45c8d369fe591b.
SessionTransactions.cs: ccf820dbc8e399d1a0224fce59a0568df22d3f0fa9c11fdb43b66c1b35a34fad.

Bezug T043/T055/T061 und Edge Case spec.md:233. Die bisherige lokale Verfügbarkeitsprüfung
ist ausdrücklich kein Nachweis gültiger nativer Wiederaufnahme.

## Aktueller Kontrollfluss

RuntimePreparation ruft RecoveryCoordinator nach Storebootstrap und vor Workerstart auf.
Der Coordinator prüft bisher nur Registrierung, Bindingversion und Fähigkeiten und
committet die boolesche Konfigurationsverfügbarkeit. ReopenAsync wird dabei nie aufgerufen.
OpenAI besitzt bereits einen sicheren GET auf dieselbe Conversation, Gemini einen GET
auf den bestätigten Interactionshead (Lazybinding bleibt ohne HTTP zulässig). Beide
Adapter besitzen bereits endliche I/O und Hostcredentialresolver. Der generische Port
ist also vorhanden; keine neuen Herstellerzweige oder native APIerfindung erforderlich.
AdmissionProvider als bisheriger Integrationstestdouble wirft bei Reopen noch
NotImplementedException. Für die jetzt tatsächlich verwendete Facette erhält er einen
expliziten Reopening-Delegate und einen identitätserhaltenden Standard. Das ändert keinen
Produktvertrag; Fehlerszenarien werden über diesen Delegate gezielt ausgelöst.

SessionAvailabilityPolicy darf ausschließlich selbst markierte Konfigurationssperren
aufheben. Bestehende RecoveryRequired-, StorageFailure- und native Continuationblockaden
bleiben unangetastet. Unbekannte laufende Arbeit erhält deshalb keinen gewöhnlichen
Reopenversuch; operation-aware Recovery bleibt exklusiv beim RecoveryService.

## Entscheidung und Grenzen

Für lokal kompatible Open-Sitzungen ohne fremde Sperre vor Workerstart genau einen
Reopen auf die gespeicherte Identität ausführen. Rückgabe muss dem eingefrorenen Binding
entsprechen; anderer Bezug/Head wird abgewiesen, nicht übernommen. Fehler blockieren
nur diese Sitzung, andere Registrierungen bleiben nutzbar. Eine erneute Hostöffnung
darf diese ausdrücklich erneut geprüfte Verfügbarkeit wiederherstellen. Kein Send/Open,
kein Requestverbrauch, keine Wiederholung verlorener Arbeit. Reopen wird zusätzlich durch
RuntimeOptions.RequestTimeout und TimeProvider begrenzt; Caller-Cancel bricht Startup ab.
Späte Readantworten erhalten keine Storecallbacks und verändern kein Binding.

Dieser Schnitt beweist vorhandene native Reopenprüfungen, keine vollumfängliche externe
Modellcatalogprüfung. Lazy-Gemini und ein OpenAI-Conversation-GET liefern keinen Beleg
für jedes Modell. Öffentliche genaue Fehlerursache ist ein anschließender, eigener
Vertragsschnitt; der aktuelle boolesche Port kann nur ProviderUnavailable darstellen.
Diese Grenzen bleiben im Folgeauftrag T121, nicht still als erledigt gelten.

## Tests vor Green

Echte SQLite-Neuöffnung mit erhaltener ursprünglicher Queue: gezielte Reopenfehler
AuthenticationFailed/ProviderSessionExpired/ProviderUnavailable sowie verändertes Binding
müssen ausschließlich betroffene Sitzung blockieren, deren Request Queued und null Sends
lassen; unabhängige Sitzung beendet ihren ursprünglichen Request. Bestehende lokalen
Configrepair-/Recoverytests unverändert als Regression. Red muss bisher fehlende
Reopenprüfung zeigen, nicht Compilerfehler. Nach minimalem Green gesamte Integrations-/
Vertragssuite, Fullguard und native Quality.

Integrationabgleich vor Fixtureanpassung, gemessen 2026-10-29T21:26:42Z:
402/403 Integrationfälle bestehen; echte Diskfullprobe endet bereits mit SessionBlocked,
weil FaultProbeProvider.Reopen bisher NotSupported wirft. Geprüfter SHA256
078ff182da3ed49f423075763588ecadc3c6ee53ba3435825e1859edd83ad4fd.
DiskFullWorker öffnet ausschließlich seine synthetische Seedkopie und benötigt einen
kontrolliert verfügbaren identischen Bezug, bevor er den Datenträger füllt. Deshalb
Reopen im Probeadapter identitätserhaltend implementieren; Open bleibt verboten,
Sendzähler und sämtliche Diskfullassertions bleiben unverändert. Kein Produktfallback.

## Öffentliche Ursache: Vertragsschnitt vor Umsetzung

Gemessen 2026-10-29T21:27:23Z nach 403 grünen Integration- und 211 Contracttests.
Coordinatorhash nach erstem Green: 3575cd30f326738b75aae809c227bc520c858a6dcaf4d94968aab3d1df3a179d.
SessionStoreConsistency.cs: cf9325960fa86d39fd9b5182060e15471af152379caae6d7179b7192de3ff316.

SessionSnapshot besitzt bisher keinen zugreifbaren Verfügbarkeitsfehler; der boolesche
Storeport verwirft die konkrete Reopenexception. Additiv nullable AvailabilityError
(ErrorCode, keine Texte/IDs/Exception) aufnehmen und über den bestehenden atomaren
Availabilitywrite als optionalen letzten Parameter durchreichen. Bestehende Aufrufer
bleiben quellkompatibel. Lokale Inkompatibilität erhält InvalidConfiguration, fremdes
Binding ProviderSessionExpired, echte native Kategorien werden per ProviderFailurePolicy
normalisiert. Erfolgreiche erneute Prüfung löscht ausschließlich eigenen Fehler/Block.
Native-/Recoveryfremdsperren werden weiterhin nicht aufgehoben. JSON-Snapshotpersistenz
übernimmt das Feld ohne neue SQL-Spalte; unbekannte Enumwerte oder Ursache ohne eigenes
Verfügbarkeitsflag werden beim vollständigen Preflight abgewiesen. Alte Snapshots ohne
Feld bleiben lesbar (null); ältere Binärstände dürfen neue Felder wie bisher ablehnen,
keine rückwirkende Kompatibilität oder Migration behauptet.

Vor Green ausführbare Property als passiven Vertrag ergänzen, vier öffentlichen
SQLite-Reopenfälle um genaue Kategorie erweitern und zwei gezielt korrumpierte
Snapshotfälle zuerst fachlich rot prüfen. Positive Wiederherstellung und null alte
Snapshotursache als Regression; rohe Providerpayloads gelangen nie in den Datenträger.
