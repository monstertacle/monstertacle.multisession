# Vorabanalyse: FIFO-Köpfe und belegte Reservierungen

Datum 2026-10-28; Bezug FR-002/FR-008/FR-016, T017/T018/T023/T024 und vorbereitende
Kapazitätsfälle T019. Diese Bestandsprüfung erfolgt vor Entwurf und Implementierung.
Gesamtstand des letzten Qualitätslaufs:
`b4f7b2df75e280b83e64ea110eafc1ccf47af1f630cfad555bbf91f0bfdcb611`.
Kein Multisession-Git; keine Commit-ID erfunden. SDK 10.0.403 und Codemetr 0.1.0
erneut geprüft, Regeln/Toolpins unverändert. OpenCode bleibt 1.19.0.

## Aktueller Deep Dive

Dateien relativ zu Common beziehungsweise UnitTests:

| Datei / Symbol | SHA256 |
| --- | --- |
| Contracts/ISessionStore.cs / TryReserveHeadAsync | `68C0497D26EA52EF921EF5BD6248AAF43353ECA207E946503777EAB11FB78E3E` |
| Contracts/ExecutionReservation.cs | `211165C62DADE9F75F00DF8657E6A8725A28DB82ADF961C088549E20C8C9D55A` |
| Contracts/RequestSnapshot.cs | `4112FD437027EC85D9116D75515324F438736CD2D13EF173EE134E2F762FDC80` |
| Contracts/SubmitRequest.cs | `176BFEB12D76B32253EFB76DED5EAB88AC059C64043E3DCB0956812858035FAA` |
| Contracts/SessionSnapshot.cs | `4DBB3FF3698F3D3A334552B3E8F85F1E8A3E81E7AACB832B768EED2EBF139800` |
| Requests/RequestAdmission.cs / SubmitAsync | `6A50C0A1828EDB94CA13565E8B20E9006A004DFE1C77127C9B0A68C811D08A43` |
| SessionRuntime.cs | `77ED2805846C3E79CEA64793C1EA5769511655F8E6851ED75AB30B74F89E10B9` |
| Fixtures/InMemorySessionStore.cs | `95E19C1DB179EAD5882A9FDF583A36277AFA62EB9349823951EDBB23871D3985` |
| RequestAdmissionTests.cs | `B837E6DC09B91C010D9BF627B2AF18C96CA4609F11E6B434EA3FE73F19E851B3` |

- RequestAdmission validiert Sitzungszustand, UTF-8, Frist und expliziten Schreibschlüssel
  vor CommitAcceptance. Der Store bleibt atomare Autorität; dort existiert nur ein
  kontrolliertes Testdouble, keine SQLite-Implementierung. Die Sequence wird je Sitzung
  über alle Requestzustände vergeben, Queuelimit zählt nur Queued.
- RequestSnapshot enthält noch keinen AcceptedUtc oder Input. SubmitRequest enthält den
  neuen Text, Timeout, WorkspaceKey und WriteIntent. Kein öffentliches Snapshotformat
  muss für eine interne Schedulingprojektion gebrochen werden.
- ExecutionReservation enthält DispatchId, RequestId, SessionId, optionalen reservierten
  Schreibschlüssel und ReservedUtc; keinen lokalen Requestzustand. Genau das erlaubt,
  jede unfreigegebene Reservierung unabhängig von Running/Unknown weiter mitzuzählen.
- TryReserveHeadAsync ist bereits die atomare Fachgrenze. Im Fake ist sie ein Stub.
  RequestScheduler und ReservationPolicy fehlen tatsächlich; keine existierende
  Dispatchschleife oder atomare Reservierung wird behauptet. SessionRuntime bleibt Gerüst.
- Bisherige Tests belegen Annahmesequenzen/Queuelimit, aber keine Auswahl, Konkurrenz
  oder Slotbelegung. ControlledProvider.Send ist separat anhaltbar; in diesem Schnitt
  wird noch kein Send ausgeführt. Auswahlbelege sind kein Parallelitäts-Livenachweis.

## Entscheidung nach Bestandsaufnahme

1. Immutable `Contracts/QueuedRequest` als schmale Projektion **bereits validierter
   angenommener Queued-Datensätze**: RequestId, SessionId, Sequence, AcceptedUtc und
   WriteWorkspaceKey. Letzterer ist nur bei WriteIntent gesetzt; null bedeutet kein
   Schreibreservierungsbedarf. Input/Secrets gehören nicht in die Auswahlpolicy.
   Pflichtschlüsselprüfung bleibt bei Annahme/Store, nicht als duplizierte Regel hier.
2. `Scheduling/ReservationPolicy.SelectHead` bekommt vollständige Queued-Projektion,
   unfreigegebene Reservierungen, aktuelle Sitzungssnapshots und positives Slotlimit.
   Reine Entscheidung, keine eigenen Locks/Provider-/Storeaufrufe. Zuerst je Sitzung
   kleinste Sequence wählen, **dann** Zulässigkeit filtern: Open/None, keine aktive
   Sessionreservierung, kein ordinal gleicher Schreibschlüssel. Sonst darf ein
   blockierter Kopf nicht durch einen späteren Auftrag derselben Sitzung umgangen werden.
   Global ältester zulässiger Kopf nach AcceptedUtc, Gleichstand RequestId im ordinalen
   kanonischen D-Format. Null bei leerer Queue, vollem Limit oder nur gesperrten Köpfen.
3. `RequestScheduler` delegiert genau eine Zulassung an TryReserveHeadAsync. Kein eigener
   Slotzähler, kein Polling, keine Taskliste, keine zweite Read-then-Write-Reservierung.
   Invalides Limit ergibt InvalidConfiguration; Cancellation wird bis Store gereicht.
4. Fake erfasst AcceptedUtc mit austauschbarem TimeProvider und nutzt dieselbe Policy
   unter seinem Annahmelock für Auswahl+Running/InProgress+Reservierung. Das belegt
   atomare Demonstratorzustände, **keine SQLite-/Crashhaltbarkeit**. MarkUncertain wird
   nur im Fake kontrolliert konkretisiert, um einen lokal unterbrochenen Auftrag mit
   Unknown und unverändert gehaltener Reservierung zu beobachten. Kein Recoverydienst.

Eine Policy außerhalb der Storetransaktion wäre racy; daher hält deren Aufrufer
Auswahl und Zustandsänderung atomar. Künftiger SQLite-Store muss diese Regel unter
seiner Transaktion erfüllen und separat durch echte Konkurrenz-/Crashproben belegen.
Alternative lokale Semaphore würde Unknown nach Neustart vergessen; Fake-only-Sortierung
würde keine wiederverwendbare Kernregel implementieren. Kein Hersteller-/Hostbezug.
Keine bestehende ISessionStore-Signatur oder persistiertes Format verändert.

Regressionsrisiken: globale FIFO statt Session-FIFO, Kopf überspringen, String-Casing,
zwei Scheduler mit getrennten Zählern, Cancellation vor/nach erfolgreicher Reservierung,
Unknown als freier Slot, weiterhin gefüllte Queue trotz Dispatch. Unbekannte/Closing/
Opening/Closed/gesperrte Sitzungen bleiben ausgeschlossen. Keine offene Frage für
diesen begrenzten Schnitt; Freigabe-/Completion-/Reopen-/Dispatchintegration bleibt offen.

## Vorab festgelegte Red–Green-Schritte

1. Null-Signatur und positive SchedulingTests: kleinste Sessionsequence vor Zeitstempel,
   ältester Sitzungskopf, ordinaler RequestId-Gleichstand. Tatsächlich Red, dann Auswahl.
2. WorkspaceReservationTests und weitere SchedulingTests: Slots voll (auch nur gehaltene
   Reservierungen), gleicher Workspace/Session gesperrt, unabhängiger dritter Kopf,
   keine FIFO-Umgehung, blockierte/unbekannte Session, ungültige Limits. Erst Red,
   dann jeweilige Sperrregeln, ohne Freigabelogik oder behauptete Starvationgarantie.
3. Scheduler-/Fake-Vertrag mit angenommenen Eingaben: Receipt/Sequence bleibt zugeordnet,
   Reservierung vor Rückgabe, Cancel am Store, zwei konkurrierende Scheduler respektieren
   gemeinsame Slots, Unknown hält Slot/Session/Workspace. Erst ausführbare Fehler sehen,
   dann minimale Delegation. Tests ohne zufällige Sleeps, Barrieren für wartende I/O.
4. Relevante Regression, Format, vollständiger Codemetr und Windows-Qualitätslauf.
   90%-Gate unverändert; Linux/CI/Live/Paket/Zielsystem nicht als ausgeführt darstellen.
