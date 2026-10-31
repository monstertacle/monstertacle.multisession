# Laufenden dauerhaften Abbruch beobachten

Gemessen 2026-10-29T12:41:54Z, Ausgang da4f78a.
RequestDispatcher.cs SHA256 3ed1c7703c87ea1c2345b06a1de3403e72c278e051e2d77258e53818d46b868e;
DispatchObservationSink.cs 96a13a437555df4cc193788f0c00c5f676675547a500d00e8dd86533eed2f83a;
RuntimeDefaults.cs deced4e15e02f5d9f4e29194b4451502beeb57100f007ccfafb3798b53d6e4e2.

## Bestand und Entscheidung vor Erweiterung

T071/T077/T082. RequestAdmission speichert Intent über IRequestCancellationStore.
RuntimeRequests delegiert dorthin; aktive Dispatcher haben bisher nur Runtime-Stop- und
Requestdeadline-Token und beobachten die gespeicherte Absicht nicht. SQL verweigert nach
Intent neue Sendberechtigung; ein Cancel zwischen Reservation und Snapshotlesen ergibt
null und lässt Running bislang unverarbeitet. Completion/Observation lesen auch bei Intent;
Unknown behält Slot/Case. Store besitzt endlichen serialisierten Queue-/Busyvertrag.
Unitfixture ControlledQueryStore implementiert den Intentvertrag; synthetischer alter
DispatchState-Cancel ohne Timestamp bleibt eine separate Sendverweigerungsregression.

Ein Dispatcher beobachtet während genau seines Send die gespeicherte Requestabsicht.
Eigener kleiner Monitor: erste Abfrage und dann benanntes 100-ms-Intervall; keine statischen
Register, kein flüchtiger Intent als Quelle. Polling verwendet wie der bestehende Scheduler
den Systemtimer, der keine fachliche Requestfrist darstellt; Requestdeadline weiter über
Host-TimeProvider. Monitor-I/O wird mit dem Ausführungstoken begrenzt; Fehler beenden lokal
mit StorageUnavailable/Unknown. Beim Ende Monitor abbrechen und abwarten; keine losgelöste
Hintergrundschleife. Intent löst den Adaptertoken aus und vorhandene Unknown-Finalisierung
schreibt Cancelled/Unknown, wenn nur Abbruch ohne Endnachweis vorliegt.

Vor Send zusätzlich Intent prüfen. Fehlt der Snapshot wegen bereits gespeicherter Absicht,
wird der noch laufende, garantiert hier nicht gesendete Versuch Cancelled/NotStarted
abgeschlossen. Reines null ohne Intent bleibt keine Sendberechtigung. Kein zweiter Send,
keine vorzeitige Freigabe bei unbekannter externer Arbeit. Bekannter Reply/Completion darf
im Rennen weiterhin gewinnen. Keine Providerherkunfts-Verzweigung oder neue Storemethoden.

Tests zuerst: öffentliche laufende Cancelabsicht erreicht kooperativen und unkooperativen
Adapter; unbestätigtes Ende bleibt Unknown reserviert, kein späterer Erfolg überschreibt.
Cancel direkt zwischen Reservation/Snapshot verhindert Send und endet NotStarted. Echter
SQLruntimefall misst monotone Zeit bis sichtbarem dauerhaften Cancelled/Unknown unter 2s,
ursprüngliche Resultate bleiben nach Wiederöffnung. Native Cancel-/Endbestätigung folgt als
weiterer T071/T077-Schnitt; kein Cancel-Token oder HTTPende wird hier als Endnachweis verkauft.
Fullqualitätslauf/Guard/Coverage. Keine offene Architekturfrage für diesen begrenzten Schnitt.
