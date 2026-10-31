# Sitzungsmetadaten vor Persistenz

Gemessen: 2026-10-29T10:03:10Z. Ausgangscommit1431da6.
Anforderung T031/T037, data-model.md Session und Constitution II/IV.
Geprüfte Quellen SHA256:
SessionCatalog.cs aa1bd34ad7ab70ac5189559b5dc43a7b9a29482cbf7c4b21c0c040891d3b6962;
SessionSnapshot.cs b3784c956c149d3378f0cda1c4539d8c734f09e3270786005d219590d849398b;
Fixtures/InMemorySessionStore.cs 72f48ff3c332120161641a21b150523522afe48401c9e13cd52db1a74d805249.

## Bestand und Fluss

SessionCatalog.CreateSessionAsync liest den eingefrorenen ProviderCatalog, speichert
Opening vor OpenAsync und bestätigt danach CommitSessionBindingAsync. Context enthält
bereits unveränderliche Settingswerte, aber ProfileSettings.Revision wird verworfen.
SessionSnapshot hat nur CreatedUtc, Lifecycle, BlockReason, BindingRevision. Die Rückgabe
wird aus der lokalen Vorfassung konstruiert statt aus dem atomaren Commit. Lookup/Paging
lesen hingegen den Store. Unterschiedliche Zeitpunkte während Open können so nicht
korrekt abgebildet werden. SessionOpeningTests prüfen Bestätigung, Fehler und lokalen
Erhalt; ProviderConfigurationTests prüfen das Einfrieren der Settingswerte.

Aktuell ist nur kontrollierter Store vorhanden, keine echte SQLimplementation.
InMemorySessionStore schreibt Binding/Block. ControlledCompletionStore schreibt Binding
und Block atomar mit Resultat/History/Release. ControlledRequestFinalization schreibt
Unknown. ControlledStoreBootstrap projiziert Opening/Running beim Neustart.
ControlledRecoveryState.Resolve setzt Closing und bei bestätigtem Ende gegebenenfalls
Closed; keine Schließzeit vorhanden. Alle Zugriffe werden durch dasselbe Gate serialisiert.
RecoveryService verwendet nur öffentliche Storeverträge, bestätigt native I/O außerhalb
CAS. Kein neuer Herstellerzweig oder Provider-HTTPaufruf ist nötig.

## Entscheidung und Abgrenzung

Additive Snapshotfelder SettingsRevision, UpdatedUtc und nullable ClosedUtc. Bestehende
Positionskonstruktoren bleiben unverändert. Neue Session übernimmt Revision und ein
UTC-Erstellungs-/Aktualisierungspaar. CommitSessionBindingAsync liefert den atomar
bestätigten SessionSnapshot; Catalog gibt genau diesen zurück. Der Store besitzt den
Zeitpunkt seiner Sessionmutationen. Kontrollierte Writer stempeln Block/Binding/Abschluss/
Unknown/Bootstrap/Recovery; CreatedUtc und SettingsRevision bleiben unverändert.
ClosedUtc wird ausschließlich beim tatsächlichen Closed gesetzt, nicht bei Schließabsicht.
Keine automatische „Zeitkorrektur“ über monotone Fristen; UTC-Zeitpunkte und monotone
Ausführungsfristen erfüllen unterschiedliche Aufgaben.

Kompatibilität: Der Storeport-Rückgabetyp ändert sich vor erster Paketfreigabe, alle
Implementierungen/Aufrufer per rg geprüft; allein der kontrollierte Store implementiert
ihn. Native Adapter konsumieren weiterhin unveränderten Context; Revision ist Metadatum,
keine neue Provideroption. Produktive Zeitstempel müssen die SQLtransaktionen T060/T065
übernehmen. Close/Cancel/Delete sind weiterhin gesonderte offene T031/T037/T079/T064,
dieser Schnitt schließt deren Gesamtumfang nicht still ab.

Testplan vor Green: Revision und Zeitpaar bereits während Open auffindbar; vor/nach
Open verstrichene Zeit und exakte Gleichheit Return/Lookup; blockierte Opening mit
neuem Update; bekannte/ungewisse Completion stempeln ohne Created/Revision zu ändern;
Recovery Closing ohne ClosedUtc, ConfirmEnded setzt ClosedUtc/UpdatedUtc gemeinsam.
Manuelle Uhr, keine Echtzeitsleeps. Minimal ausführbare Felder/Signaturen vor Red,
dann fachliche Assertions beobachten und erst danach Writer/Catalog ergänzen.

## Strukturelle Nachprüfung vor Refactoring

Gemessen 2026-10-29T10:06:24Z. 253 Unit/195 Contractfälle grün.
Der volle Guard meldet Resolve-Komplexität28 bei Grenze25. Ursache: wiederholte
Closing/Restarbeit-Entscheidung für Lifecycle und ClosedUtc. Diese Entscheidung wird
in eine benannte private Lifecyclefunktion extrahiert und deren Ergebnis für beide
Felder verwendet; CAS, Gate und Commitreihenfolge bleiben unverändert. Anschließend
erneute vollständige Qualität und Guard, keine Grenzwertänderung.
