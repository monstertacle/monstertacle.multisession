# Public Library Contract v1

**Datum**: 2026-10-27 | **Basis**: [spec.md](../spec.md), [data-model.md](../data-model.md)

Dies ist der Bibliotheksvertrag mit Implementierungsabgleich vom 2026-10-29T21:57:58Z.
Er definiert keine HTTP-Routen, Dienstinstallation oder Benutzeroberfläche.
Namespace-Wurzel: `Monstertacle.Multisession`.

Erneut gegen Constitution v2.0.0 geprüft; Konkretisierung des bestehenden Umfangs.
Für seine Verhaltenszusagen sind zuerst ausführbare fehlschlagende Vertrags-/Konsumtests
zu schreiben (insbesondere TDD-01/TDD-04/TDD-07), bevor das jeweilige Verhalten entsteht.
Die verbindlichen Dispatch-/Recovery-/Annahme-/Pagingdetails stehen in
[orchestration-details.md](orchestration-details.md). T112–T115 setzen die dort ausdrücklich
benannten Unterschiede nach passendem Red um; diese vier Vertragsschnitte sind
inzwischen implementiert und in den storybezogenen TDD-Nachweisen belegt.

SharpClaw ist als langfristiger In-Process-Konsument vorgesehen. Es nutzt dieselben
öffentlichen Verträge wie andere Hosts, ohne Sonderzugriffe auf interne Klassen.
Die Bibliotheken referenzieren weder dessen Assemblies noch dessen Quellcode oder
Konfigurationsdateien. Ein eigener zusätzlicher Dienst ist keine Einbettungsvoraussetzung.
Der Zielhost kann vorhandene Anbindungen über eigene ISessionProvider-Adapter registrieren;
deren konkrete Migration und Bestandszustände sind nicht Teil dieses Erstvertrags.

## 1. Zusammensetzung und Lebenszyklus

Der Host verbindet Common mit einem Store, einer Plattformimplementierung, explizit
registrierten Providern und einem CredentialResolver. Logging und TimeProvider sind
injizierbar. Keine versteckten statischen Registrierungen oder herstellerbezogenen Switches.

`SessionRuntime` stellt die drei fachlichen Facetten Sessions, Requests und Recovery
bereit. Diese sind getrennte Verantwortlichkeiten, keine Pflicht für eine universelle
Manager-Klasse mit sämtlichen Methoden. Die Vertragsversion und das NuGet-Paket folgen
SemVer; Version 1 startet als Paketversion 0.1.0 vor einer stabilen 1.0-Zusage.

| Operation | Ergebnis / Pflicht |
| --- | --- |
| StartAsync(options, cancellationToken) | Besitzt danach exklusiv den Store; Schema, Optionen und Recovery geprüft; keine Netzwerklistener |
| StopAsync(deadline, cancellationToken) | Nimmt nichts neu an; endliche Frist; alle offenen Anfragen behalten einen sichtbaren Zustand |
| DisposeAsync() | Gibt Ressourcen/Store-Lease erst nach kontrolliertem Stop frei; keine implizite Löschung von Zustand |

Options werden vor Start validiert und danach nicht still geändert. Ein zweiter Start
oder eine Operation nach abgeschlossenem Stop ist ein eindeutiger Lifecycle-Fehler.
Der Host muss Stop ausdrücklich abwarten; das bloße Verwerfen eines Objekts ist kein
Beweis für die Beendigung externer Arbeit.

Die Stop-Frist beginnt beim Eingang der Stop-Anforderung und wird monoton gemessen.
Neue Annahme wird ab diesem Zeitpunkt gesperrt. Innerhalb der Frist wird offene Arbeit
abgeschlossen; danach verbleibende Queued-Anfragen enden ausdrücklich als
Cancelled/NotStarted. Laufende Arbeit erhält einen bestätigten Endzustand oder Unknown
mit erhaltener Reservierung. Ein Prozessabsturz vor gesicherter Finalisierung ist dagegen
Wiederanlauf: Noch Queued gespeicherte Anfragen werden nicht als abgebrochen angenommen.
Bei nicht verfügbarem Store darf Stop keinen dauerhaften Erfolg behaupten; der Fehler
bleibt sichtbar, und der zuletzt gesicherte Zustand ist beim nächsten Start maßgeblich.

## 2. Sessions — Sitzungskatalog

| Operation | Eingabe | Ergebnis und Semantik |
| --- | --- | --- |
| CreateSessionAsync | string agentProfileId, SessionExecutionContext context, CancellationToken | Lokale SessionId und Snapshot; Context enthält nullable WorkspaceDirectory/WorkspaceKey; native Eröffnung soweit der Provider sie separat unterstützt |
| GetSessionAsync | SessionId | Snapshot einschließlich Providerbindung, Lifecycle und BlockReason; keine Geheimnisse |
| ListSessionsAsync | Begrenzte Seitengröße, optionaler Cursor | Nur Sitzungen dieser Instanz; stabile Sortierung nach CreatedUtc und SessionId |
| ReadHistoryAsync | SessionId, begrenzte Seitengröße, Cursor | Lokal beobachtete Eingaben, Ergebnisse und Betriebsereignisse; kein behaupteter vollständiger nativer Chat |
| CloseSessionAsync | SessionId | Neue Annahme sofort sperren; wartende Anfragen abbrechen, laufende innerhalb der Frist beenden oder ungewiss sichern |
| DeleteSessionAsync | SessionId | Nur Closed ohne aktive/ungeklärte Reservierung; lokale Daten gemäß Löschvertrag entfernen |

Neue lokale Identität und SettingsSnapshot werden vor externer Eröffnung gespeichert.
Wenn ein Provider erst mit der ersten Nachricht eine native ID vergibt, bleibt das
Binding bis dahin leer, ohne eine erfundene externe Sitzung zu behaupten.
Schlägt eine externe Eröffnung möglicherweise nach Annahme fehl, bleibt ein blockierter
lokaler Datensatz auffindbar; der Fehler enthält dessen SessionId.

Schließen oder Löschen löst keine implizite Löschung beim Hersteller aus. Verändernde
Operationen mit fremden/unbekannten Kennungen geben NotFound zurück; einzelne Get-Lese-
operationen liefern bei unbekannter Identität null. Daten anderer Instanzen werden nicht gesucht.
Ein Close mit ungewisser Reservierung liefert Closing mit RecoveryRequired, nicht Closed.
Die Schließabsicht allein ist weder Beendigungsbeweis noch Freigabe des Workspaces.

## 3. Requests — dauerhafte Annahme und Status

| Operation | Eingabe | Ergebnis und Semantik |
| --- | --- | --- |
| SubmitAsync | SubmitRequest(SessionId, Input, Timeout, WorkspaceKey, WriteIntent), CancellationToken | RequestReceipt mit RequestId und Sequence erst nach Commit; WorkspaceKey ist nullable, WriteIntent ist ein expliziter bool |
| GetRequestAsync | RequestId | Snapshot von Zustand, Ergebnis, Gewissheit und bereinigtem Fehler |
| ListRequestsAsync | SessionId, begrenzte Seitengröße, Cursor | Auffindbarkeit auch nach verlorener Annahmebestätigung |
| CancelRequestAsync | RequestId | Sichtbarer Abbruchwunsch; Rückgabe beweist nicht die externe Beendigung |

Submit bedeutet Annahme, nicht bereits abgeschlossene Providerarbeit. Der Aufrufer kann
den Zustand wiederholt lesen; ein dauerhaftes UI-/SSE-Pushprotokoll wird nicht vorausgesetzt.
Der an Submit übergebene CancellationToken betrifft zunächst die lokale Annahmeoperation.
Nach Commit wird für das Beenden des gespeicherten Auftrags CancelRequest verwendet;
ein verlorenes Client-Warten darf keine bestätigte Anfrage still löschen.
AdmissionTimeout beginnt beim Eintritt in SubmitAsync und begrenzt nur das lokale
Warten auf Annahme, nicht die spätere Queue-Wartezeit. Frist-/Tokenabbruch vor sicher
erfolgtem Commit bedeutet keine bestätigte Annahme. Ist der Commit-Ausgang beim
Abbruch unbekannt, darf der Fehler keine sichere Nichtannahme behaupten; die Sitzungsliste
der Anfragen bleibt der Nachschlageweg. Ein neuer Submit wird nicht automatisch dedupliziert.

Annahmereihenfolge ist die unter SQLite-Transaktion vergebene Sequence, nicht die
Wanduhr-Reihenfolge paralleler Aufrufer. Je Sitzung höchstens ein laufender Dispatch.
Getrennte Sitzungen können parallel laufen, solange globale Kapazität und Workspace zulassen.

Bei vollem Wartelimit erfolgt QueueFull ohne Annahme. Infrastrukturwarteschlangen sind
ebenfalls begrenzt; Annahme darf kontrolliert warten oder mit LocalCapacityExceeded
abgewiesen werden, aber niemals unbeschränkt Tasks oder Nachrichten puffern.

Eine angenommene Queued-Anfrage besitzt keine automatische Wartefrist; RequestTimeout
beginnt erst beim Dispatch und wird monoton gemessen. Sie kann vorher ausdrücklich
abgebrochen werden. Unter zulassungsfähigen Sitzungsköpfen zählt die älteste Annahme,
bei gleichem AcceptedUtc die ordinal verglichene RequestId. Blockierte Köpfe werden
übersprungen, ohne die Reihenfolge innerhalb derselben Sitzung zu ändern. Feste Wartezeiten
oder globale Starvation-Freiheit sind keine Zusicherung. Ungewisse Reservierungen belegen
weiter Slots; bei vollständig belegtem Limit besteht keine Zusage weiteren Fortschritts.

Die Bibliothek startet nach Timeout/Abbruch keine weiteren Folgeanfragen einer Sitzung,
solange die tatsächliche Beendigung des vorherigen Dispatch nicht geklärt ist.
Das gilt auch dann, wenn lokal bereits ein Cancelled-/TimedOut-Ergebnis sichtbar ist.

## 4. Recovery — ausdrücklich statt blindem Replay

| Operation | Eingabe | Ergebnis und Semantik |
| --- | --- | --- |
| ListRecoveryCasesAsync | Begrenzte Seitengröße, Cursor | Offene RecoveryCaseSnapshot für SessionOpening und RequestExecution samt aktueller Revision |
| GetRecoveryCaseAsync | RecoveryTarget | Einzelner Fallsnapshot samt Revision, auch nach Klärung solange die Sitzung erhalten ist; unbekannt → null |
| InspectRecoveryAsync | RecoveryTarget | Liest bekannten nativen Zustand ohne Send/Retry, Revisionsänderung oder Reservierungsfreigabe |
| ResolveRecoveryAsync | RecoveryDecision mit Target/ExpectedRevision/Begründung | Persistiert Klärung atomar; veraltete oder doppelte Entscheidung → RevisionConflict |

RecoveryTarget adressiert entweder eine ungewisse Eröffnung per SessionId oder eine
Ausführung per RequestId. Es werden keine Dummy-Requests angelegt. Erzeugung, Revision,
Schließung ohne bekannten nativen Bezug und CAS-Regeln: orchestration-details.md §2.

ConfirmEnded setzt voraus, dass externer Abschluss tatsächlich festgestellt oder vom
verantwortlichen Betreiber ausdrücklich nach Untersuchung bestätigt wurde. Eine fehlende
lokale PID, ein abgelaufener Timer oder ein HTTP-Verbindungsende genügt nicht.

Eine Fortsetzung benötigt zusätzlich einen gültigen nativen Bezug beziehungsweise einen
vom Adapter aus bestätigten Belegen gewonnenen Fortsetzungsstand. Ohne diesen Nachweis
bleibt die Sitzung blockiert oder wird geschlossen. Kein Transcript-Replay und kein
stilles Neueröffnen unter der bisherigen Kennung. Das ursprüngliche Interrupted-/Fehler-
Ergebnis wird nicht nachträglich zu einem unbewiesenen Erfolg umgeschrieben.

Wiederholung geschieht ausschließlich durch einen neuen Submit mit neuer Kennung nach
ausdrücklicher Entscheidung. Es gibt kein Exactly-once-Versprechen für externe Hersteller.

## 5. Optionen und Hostpflichten

| Einstellung | Startwert / Validierung |
| --- | --- |
| MaxConcurrentRequests | 4; positiv; Unknown-Reservierungen zählen weiter |
| MaxQueuedRequests | 100; positiv; zählt dauerhafte Queued-Aufträge instanzweit |
| MaxInputBytes / MaxOutputBytes | 1 MiB / 4 MiB; positiv; UTF-8-Bytes, keine versteckten Zeichenannahmen |
| AdmissionTimeout | 5 Sekunden ab Eintritt in SubmitAsync; positiv und endlich; betrifft nur lokale Annahme |
| RequestTimeout | 600 Sekunden; positiv und endlich; beginnt beim Dispatch |
| ShutdownTimeout | 10 Sekunden; positiv und endlich |
| DbWorkQueueCapacity | 256; positiv; begrenzt lokale Datenbankarbeit |
| DbBusyTimeout | 1 Sekunde; positiv und kleiner als ShutdownTimeout; keine unbeschränkten DB-Retries |
| Retention | Explizite Frist oder RetainUntilExplicitDeletion; keine stille Inhaltslöschung |
| StateDirectory | Absolut, privat, lokales Dateisystem; explizit initialisiert |

Eingabe-/Ausgabegrenzen sind inklusive: Genau das Limit ist zulässig, ein Byte darüber
nicht. Eine leere Eingabe oder Überschreitung wird vor Annahme als InvalidInput beziehungsweise
InputLimitExceeded abgewiesen. Bei zu großer Provider-Ausgabe endet die Anfrage mit
Failed/OutputLimitExceeded, leerem Output und OutputComplete=false, nicht mit gekürztem
Erfolg. Frühere Verlaufseinträge bleiben unverändert. Kann externes Ende nicht bestätigt
werden, bleibt Unknown mit Reservierung; die native Kontextverwaltung wird nicht verändert.

Ein Anbieteraufruf hat zusätzlich einen endlichen HTTP-Header-Timeout von 30 Sekunden.
Polling beginnt mit einer Sekunde Abstand; serverseitige Retry-After-Angaben werden für
ungefährliche Statusabfragen respektiert, ohne die gesamte Anfragefrist zu überschreiten.
Es gibt keine automatische Wiederholung von Eröffnungs-/Send-POSTs.

Der Host verantwortet Authentifizierung, Benutzerberechtigungen, Providerzugänge, erlaubte
Endpunkte, Registrierung der Adapter und konsistente Arbeitsbereichsschlüssel.
Eine Hostregistrierung ist keine Sandbox-Zulassung für untrusted Code. Von unterschiedlichen
Benutzern geteilter Arbeitsspeicher oder bewusst geteilte Speicherpfade werden nicht als
sichere interne Mandantentrennung angeboten.

Der Host muss Datenträger-/Providerretention und eventuell notwendige Backups betreiben.
Providerzustand kann trotz vorhandener lokaler Historie ablaufen. Diese Bibliothek
garantiert kein unbegrenztes Provider-Gedächtnis oder Wiederherstellung nach Datenträgerverlust.

## 6. Store- und Plattformverträge

`ISessionStore` exponiert fachliche atomare Operationen wie CommitAcceptance,
TryReserveHead, CommitCompletion, MarkUncertain und ResolveReservation statt eines
generischen CRUD-Repositories, mit dem der Kern Transaktionsregeln versehentlich umgehen könnte.
Der Store implementiert keine Provideraufrufe. Alle DB-Arbeiten sind begrenzt und serialisiert.

Die reine `ReservationPolicy` kann innerhalb dieser atomaren Storeoperation eine
vollständige konsistente `QueuedRequest`-Projektion auswerten. Sie enthält nur bereits
angenommene/validierte Queued-Datensätze mit Sequence/AcceptedUtc; WriteWorkspaceKey ist
nur für Schreibaufträge gesetzt. Auswahl und anschließende Reservierung müssen unter
derselben Transaktion erfolgen. Ein Policyaufruf allein reserviert nichts.
`RequestScheduler.TryReserveAsync` delegiert eine solche Operation an den Store;
RequestDispatcher.TryDispatchAsync verarbeitet eine frische Reservierung mit deren
ReservedDispatch und fixierten Registrierungen. False bedeutet kein zulässiger Dispatch;
true bestätigt ein gespeichertes lokales Endergebnis, auch ein bestätigtes Fehlerergebnis.
Storefehler werden als Fehler gemeldet, nicht als leere Queue. Callbackbestätigung,
Bindingidentität und Ausgabegrenze werden vor Completion geprüft; unbestätigte Arbeit
behält ihre Reservierung. SessionRuntime-/Workerkomposition bleibt T026.

Der in derselben Reservierungstransaktion gespeicherte ReservedDispatch ist über
GetReservedDispatchAsync lesbar und trägt Eingabe, Timeout, eingefrorenen Sitzungskontext
und BindingRevision. Öffentliche RequestSnapshots ersetzen diesen Datensatz nicht.
Die C#-Queryschnittstelle besitzt diese Operation seit T112; Vertrag siehe
orchestration-details.md §1. Die Annahme verwendet seit T114 RequestAcceptance mit vorab
erzeugter Kandidatenkennung; nur Sequence/Queuekapazität bleiben Storeautorität (§3).

Alle Listen folgen denselben PageLimits-/Cursorregeln aus orchestration-details.md §4:
Seitengröße 1–100, versionierte bereichsgebundene Keysetcursor, keine seitenübergreifende
Snapshotzusage. Diese Regeln sind in T115 lokal testgetrieben umgesetzt; produktive SQLitequeries und
Persistenzabnahmen bleiben T063.

`IStateDirectoryLease` repräsentiert geprüften privaten Speicher und exklusiven Besitz.
Windows-/Linux-Implementierungen prüfen Rechte, lokale Pfade und den Lifetime-Lock.
Lease bleibt für die gesamte Store-Nutzung offen. Eine Fremdinstanz erhält StoreAlreadyOwned.
Fehlerhafte Rechte, nicht unterstützte Plattform oder Netzwerkpfade werden vor Annahme
einer Anfrage gemeldet. Ein Windows-Paket auf Linux ist kein stiller No-op-Fallback.

Der gemeinsame Kern wird mit Testimplementierungen dieser Verträge geprüft. Dazu sind
keine plattformspezifischen Bibliotheken oder echte Herstellerzugänge erforderlich.

## 7. Fehlervertrag

Öffentliche Fehler bestehen aus stabiler Kategorie, SessionId/RequestId soweit vorhanden,
Ausführungsgewissheit und einem bereinigten optionalen Provider-Code. Rohdaten, Secrets,
Authorization-Header und vollständige Prompts gehören nicht in die Fehlerbeschreibung.
Für Annahmefehler kommt das separate optionale AdmissionStatus hinzu; externe Gewissheit
ersetzt keine Aussage über lokalen Commit. Caller-Cancel bleibt über eine von
OperationCanceledException abgeleitete AdmissionCancellationException erkennbar.
Die konkrete Fehler-/Receipt-/Unknown-Semantik ist in orchestration-details.md §3 festgelegt.

Kategorien: InvalidConfiguration, NotFound, SessionClosed, SessionBlocked, QueueFull,
LocalCapacityExceeded, StoreAlreadyOwned, StorageUnavailable, UnsupportedStoreVersion,
UnsupportedPlatform, AuthenticationFailed, ProviderRateLimited, ProviderUnavailable,
ProviderSessionExpired, ProviderContextLimit, UnsupportedProviderAction, ExecutionFailed,
Cancelled, TimedOut, Interrupted, RevisionConflict, InvalidInput, InputLimitExceeded,
OutputLimitExceeded und AdmissionTimedOut.

Lokal ungültige Eingaben werden vor Annahme zurückgewiesen. Ein bereits angenommener
Auftrag erhält seinen Fehler dauerhaft als Ergebnis. Kontextgrenzen werden weitergegeben,
ohne Kompression, Modellwechsel, versteckten Retry oder Kürzen vorhandener Historie.
Queued/Running sind nicht final; Succeeded, Failed, Cancelled, TimedOut und Interrupted
sind lokale Endzustände. ExecutionCertainty ist davon getrennt und kann insbesondere
nach Cancelled/TimedOut/Interrupted weiterhin Unknown sein. Zulässige Kombinationen
sind im Datenmodell verbindlich festgelegt; Lifecycle und BlockReason gehören zur Sitzung.

## 8. Beobachtbarkeit und Abnahme

Strukturierte Events enthalten Instanz-, Sitzungs-, Anfrage- und Dispatchkennung,
Lifecycle-Übergang, Gewissheit, Dauer und Fehlerkategorie, aber keine Inhalte/Secrets.
BCL-Metriken: wartende Anfragen, aktive beziehungsweise ungewisse Reservierungen,
Annahmedauer, Ausführungsdauer und Ergebniszählungen. Keine hochkardinalen RequestIds
als Metriklabels; Activity-Korrelation kann diese in gezielten Traces verwenden.
Logfehler dürfen keinen bereits committed Auftrag verlieren lassen.

| Anforderungen | Vertrags-/Abnahmebezug |
| --- | --- |
| FR-001, FR-014, FR-017, FR-023 | NuGet-Konsum, Common ohne Plattform, echter Windows-/Linux-Hosttest |
| FR-027 | Eingebetteter konsumierender Host über dieselben öffentlichen Verträge; SC-011 |
| FR-002–FR-007, FR-022, FR-024–FR-025 | Providervertrag, Routing, native Bindung und unveränderte Inhalte |
| FR-008–FR-013, FR-016 | Annahme, FIFO, Kapazität, Workspace, Cancel und Stop |
| FR-015, FR-021, FR-026 | bereinigte Diagnosen, Rechte, Retention und Instanztrennung |
| FR-018–FR-020 | Commit, Wiederanlauf, Unknown und ausdrückliche Klärung |

Alle Zeit-/Lastwerte sind zu validierende Ziele, keine bereits erbrachten Messergebnisse.

## 9. Agent-/Modellgrenze und Zielhostabbildung

Der generische Adaptervertrag aus `provider-contract.md` §8 ist verbindlich.
CreateSession erhält zusätzlich den expliziten `SessionExecutionContext`; Workspace,
ExecutionKind, Modell und Adapterzuordnung werden für Fortsetzungen eingefroren.
`ProviderRegistration` registriert eine Adapterinstanz samt technischer Identität und
Vertragsversion. Endpoint und Authentifizierungsoptionen gehören in den konkreten
Adapter, nicht als zwingende HTTP-Felder in eine generische Registrierung.
HTTP-Adapter müssen weiterhin erlaubte absolute HTTPS-Endpunkte prüfen; dies ist kein
Wegfall der Sicherheitsvorgaben. Credentials werden nicht in Sitzungssnapshots kopiert.

Die lokale UUID und die opaque externe Sitzungskennung bleiben getrennt; weder
Providerpräfix noch UUID-Format darf für externe Kennungen zentral vorausgesetzt werden.
Die Integration mit dem Zielhost erfordert passende Vertragsfälle mit beobachteter
nativer ID, bestätigtem terminalem Ende, Workspace und Cancellation. Ein Modell-REST-
Fixture allein erfüllt diesen Nachweis nicht. Bestehende Sitzungen werden nicht migriert.

## Implementierungsabgleich: Wiederaufnahme und Availability

Der aktuelle öffentliche Aufruf für die Erzeugung lautet
`CreateSessionAsync(string agentProfileId, SessionExecutionContext context, CancellationToken cancellationToken)`;
`SessionExecutionContext` trägt optionalen WorkspaceDirectory und WorkspaceKey. Die
Tabellen oben sind semantische Kurzformen. Vor Workerstart werden gespeicherte Open-
Sitzungen lokal und über ihren gleichen nativen Reopenbezug geprüft. Die optionale
Facette `ISessionAvailabilityProvider.ValidateAvailabilityAsync(ProviderSessionContext,
CancellationToken)` ermöglicht eine ausdrücklich lesende Modell-/Credentialprüfung;
beide nativen Adapter implementieren sie, die generische Runtime kennt keine Herstellernamen.

SessionSnapshot ergänzt `ConfigurationUnavailable` als Herkunft einer erneut prüfbaren
Verfügbarkeitssperre und nullable `AvailabilityError` als bereinigte ErrorCode-Kategorie.
Erfolgreiche erneute Prüfung entfernt ausschließlich eigene Sperren/Ursache; andere
native Restriktionen und Recoveryvorrang bleiben. Kein gebundenes Modell, native ID,
Settings oder Bindingrevision werden aus Metadaten ersetzt. Queued bleibt bei Start-
ablehnung unverändert. Die Prüfung verwendet die Runtimeausführungsfrist; ein verfügbarer
anderer Provider kann nach Vorbereitung weiterhin arbeiten. Details und Grenzen in
`docs/operations.md`, konkrete Red-/Greennachweise in `evidence/tdd-us2.md`.

Der bestehende Availability-Storewrite erhält als optionalen letzten Parameter eine
ErrorCode-Kategorie; alte Aufrufe bleiben quellkompatibel. Aktuelle Snapshots persistieren
das Feld im vorhandenen JSON. Ältere Snapshots ohne Property bleiben lesbar; ältere
Binärstände können unbekannte neue Felder sicher ablehnen. Daraus folgt keine Zusage
rückwärtskompatibler Vorabformatmigration oder einer neuen Schemaversion.
