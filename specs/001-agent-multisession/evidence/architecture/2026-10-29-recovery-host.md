# Recovery als öffentlich auswählbare native Prozessprobe

Bestandsprüfung 2026-10-29T19:28:06Z gegen 3e20a54 und den separat geprüften
native-context-Schnitt. Geprüft wurden PersistenceRecoveryTests, CrashHarness,
CrashProtocol, CrashWorker/CrashRecoveryWorker/CrashSessionProvider,
SqliteRuntimeFixture, AdmissionProvider, ProbeHost und die nativen Worker.

Ausgangshashes SHA256:
CrashRecoveryWorker.cs c9d6fcfc945e2190a5eed7bb4701da6b34a41799b330d29942570dcc5c36aa48;
CrashWorker.cs 9cde22416720d0d8e502aa79c7ef2196aeda4fe6fa6fc9079a548c095800c267;
PersistenceRecoveryTests.cs d3a035e33cb4df5e4ed5ab2dc84e492c7b7313a7b06ef2b5849cc46dc6423cbd.

Der vorhandene SC-009-Test erzeugt zwei bestätigte Ergebnisse, drei wartende
Anfragen in A und eine extern gestartete Anfrage in B. Die Signalschranke liegt
hinter dem Operations-ID-Commit. Der bestehende Worker nutzt eine kooperative
Test-Lease; dessen Messung ist kein nativer Besitznachweis. CrashHarness benötigt
außerdem ein Repository zum Finden des Workers und ist deshalb ungeeignet für
den publizierten CLI-Host. Diese Grenzen werden nicht als erfüllt ausgegeben.

Entscheidung vor Implementierung: bestehenden Crash-Setup-Code wiederverwenden,
aber eine explizite interne Worker-Auswahl mit nativer OpenExisting-Lease
anbieten. Marker/Nonce und erlaubte Testverzeichnisnamen bleiben Pflicht. Ein
kleiner Host-eigener Prozesshalter startet exakt seine eigene Assembly, wartet
auf das bestehende Signal, tötet ausschließlich sein Kind und wartet endlich
auf dessen Ende. Der Elternprozess gibt die native Lease vor Start frei und
öffnet sie nach dem Kill neu; kein gleichzeitiger kooperativer Zweitbesitz.

Zehn separate Unterstores vermeiden sich aufsummierende Unknown-Reservierungen.
Jeder wird initialisiert, an das Kind übergeben, gekillt und über dieselben
öffentlichen Runtime-/Storeports wieder geöffnet. Wiederaufnahme muss die drei
ursprünglichen A-IDs in FIFO senden, beide früheren Ergebnisse behalten, B samt
Operations-ID blockieren und dessen Slot/Workspace reserviert lassen. Der
konsumierende AdmissionProvider wird als BCL/Common-Fixture verlinkt; sein Open
wirft ausdrücklich, damit eine versehentlich neue native Identität auffällt.
ProbeHost erhält eine explizite OpenExisting-Option; ValidationRunState kann den
vorher gemessenen TrustScope übernehmen. Keine Änderung an Recoveryregeln.

Zuerst fehlende CLI-Probe rot: zehn tatsächliche Datensätze nach Prozessende
unabhängig auf zwei Sessions, sechs Requests/History und genau eine offene
Reservierung prüfen. Danach minimale Komposition, bestehende Crash-/CLI-
Regressionen und Full-Guard. Der Test erhält ein explizites endliches Fünfminuten-
Prozessbudget für zehn reale Neustarts; das Standardbudget bleibt unverändert.
Native Windows-CI und Zielsystemabnahme bleiben erforderlich.

Nach 19 grünen gezielten Regressionen meldete der vollständige Guard eine
Kopplung von 42 (Grenze 40) für CrashRecoveryWorker. Ursache ist die neu dort
platzierte native/kooperative Lease-Auswahl zusätzlich zum fachlichen Setup.
Entscheidung vor Refactoring: die Lease im bereits für Worker-Auswahl und
Cleanup zuständigen CrashWorker erzeugen und als IStateDirectoryLease übergeben.
CrashRecoveryWorker bleibt auf seinem vorhandenen Common-/SQLite-Vertrag und
liest das Verzeichnis aus der Lease. Keine Grenzwert- oder Scopeänderung;
gezielte Tests und vollständiger Guard werden wiederholt.

Vor dem Refactoring gemessen am 2026-10-29T19:31:49Z:
CrashRecoveryWorker.cs 703c91c8b56838aed064727026fddbef753d73bd8fa45bfcf64b5482e0bb4934;
CrashWorker.cs 2777338150f3200ae3206dc90b63eef20deb3d2486f8931bd97a2d0fdbecfe99.
Danach 31 gezielte Regressionen grün; Full-Guard 420 Dateien/436 Typen ohne
Befund, Parsefehler oder übersprungene Dateien. Bestehende kooperative
Crash-Proben bleiben unverändert als solche ausgewiesen. Die neue Recovery-CLI
verwendet dagegen tatsächliche native Leases in beiden Prozessen.
