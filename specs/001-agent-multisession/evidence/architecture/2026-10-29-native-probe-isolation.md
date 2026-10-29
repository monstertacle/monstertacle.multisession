# Vollständige native Hostproben ohne konkurrierende Testlast

Bestandsprüfung 2026-10-29T20:31:06Z gegen e385ca8. SHA256:
ValidationScenarioTests.cs cfcbec3a33f8790a5bd432558884df15198d737a92553cea52a40582c7f13d2d;
ExclusiveStorageVolumeGroup.cs 219ae4a992d72db1f4a1a56b2ae653a3c10a08555bc0e5cb835b44d9f37f72b1;
DiskFullStorageTests.cs 3f6d3a60975f0594428094a679965ca11963a8debb44feaaeebf39023b39b126.

Lauf 40947706248 meldet in Windows ausdrücklich
WaveAdmission_AdmissionTimedOut während der 100-Request-Routingprobe. Ihr
Fünfminuten-Prozessbudget ist nicht abgelaufen. Die Standard-Admissionfrist von
fünf Sekunden arbeitet korrekt; die parallele Integrationstestlast konkurriert
mit den synchron dauerhaft geschriebenen Stores. Diese Probe prüft Zuordnung,
FIFO und Haltbarkeit, nicht globale Rechnerlast oder Durchsatz einer gemeinsam
mit hunderten anderen Stores überlasteten CI-Maschine. Isolierte veröffentlichte
Linuxproben und frühere native Windows-Läufe bestanden denselben fachlichen Ablauf.

Entscheidung vor Änderung: ValidationScenarioTests in die vorhandene xUnit-
Collection mit DisableParallelization=true aufnehmen. Diese führt bereits reale
Speichervolumen-/Diskfull-Proben ohne konkurrierende Collections aus. Es bleiben
alle 100 Requests, alle drei Recovery-/Capacity-/Cancel-Grenzen, sämtliche
Datensatzassertionen und beide Prozess-/Produktfristen erhalten. Keine Änderung
an RuntimeOptions, Scheduler, SQLite, Qualitätsgrenzen oder Fehlerkategorien.
Der tatsächliche CI-Fehler ist der Red-Beleg; kein künstlicher lokaler Lastfehler
wird erzeugt. Die vollständige lokale Suite und beide nativen CI-Proben folgen.
