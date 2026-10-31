# Persistierte Reservierung, Beobachtung und bekannter Abschluss

Gemessen2026-10-29T11:16:12Z, Ausgang427691b. T051/T053/T059/T060 sowie HistoryteilT063.
SqliteSessionStore.cs SHA256d2112153e9cf5201dfab033baa1ba68528950c0c128206fd825393a2749ddd0a;
SqliteStoreState.cs28e6b20963828d1e0cd022f57d6992e8e3ed286dec49fbd98986b6161ad343e7;
ReservationPolicy.cs572cf1e25e51fbec1cb9e3c3646a576396f6a531c89fdd955b5278a78791fe19;
RequestDispatcher.cs55e769f4b00cfb08070e1df0ca2b491fb494cb86e054039e49b39b14bdff19b2.

## Bestand und Datenflüsse

SQLstore kann Annahme/Sessionbindung/Lookup und Session-/Requestkeysets. TryReserve,
GetReservedDispatch, Observation, Completion, Unknown und Recovery sind noch Gerüste.
Ein DBworker serialisiert Transaktionen; Öffnung/Bindung/Annahme liegen nie über Provider-I/O.
Schema besitzt Requests.DispatchId/BindingRevision/ReservedDispatchJson und alle acht
Tabellen einschließlich Results/History/Reservations. Es gibt noch keine Dispatchdaten,
weil der entsprechende Port bislang fehlt. ActiveSession/Workspaceindices sind vorhanden.

Common ReservationPolicy kennt die maßgebliche FIFO-/Workspace-/Slotregel einschließlich
Unknownbelegung und blockierter Sessionköpfe. RequestDispatcher sendet ausschließlich frisch
reservierte IDs, liest FrozenSnapshot, wartet jeden Observationcallback und normalisiert
Reply/Output vor Commit. Completionrevision conflict oder unbekannter Commit berechtigen
keinen erneuten Send/kompensierenden Write. DispatchObservationSink prüft stabile Session-ID
und awaitbare Callbackbestätigung; observations allein bestätigen keinen neuen Head.
Kontrollierter Store hält Observations getrennt, Historyeintrag enthält Input+Result und
monotone sitzungsbezogene Ordinal. SQLpfade dürfen das nicht als Haltbarkeitsbeleg übernehmen.

## Erweiterungsentscheidung vor Code

ReservationTransactions liest aktive Reservierungen und den vorhandenen begrenzten
persistierten Queuebestand als reine Projektionsdaten, verwendet dieselbe ReservationPolicy
und friert gewählten Session-/Submitstand ein. Kein Nachbau der Fachregel in herstellerspezifischer
SQLlogik. Unter derselben Transaktion: Running/InProgress, StartedUtc, DispatchId,
BindingRevision, ReservedDispatch und aktive Reservation. Andere Instanzen/Hostprofile
liefern keine Eingaben; kein Provider-I/O im Worker. Queueprojektion ist durch durable
Annahmegrenzen begrenzt, keine Taskliste je Eintrag; öffentliches Paging bleibt nativesLIMIT.

Private typed StoredDispatch-Envelope in vorhandener ReservedDispatchJson-Spalte:
Snapshot unveränderlich, daneben beobachteter Sessionbezug/Operationsbezug. Keine neue
Tabelle, DDLänderung oder Formatmigration nötig: ältere Implementierung konnte diese
nullable Spalte noch nie befüllen. Eigenes JSONdatenformat, keine ausführbaren Typnamen.
GetReservedDispatch prüft aktuellen Running/Cancellation/Releasezustand, gibt ausschließlich
FrozenSnapshot zurück. Vor erfolgreichem Bootstrap kein Sendrecht; nach Reopen bleibt alte
Running bis Recoveryumsetzung sicher nicht startbar. Wiederholtes Lesen ist kein Resend.
Observation schreibt Envelope vor Callbackack, veröffentlicht keinen bestätigten Head.
Ungültige/längst beendete Dispatchbezüge werden ohne Mutation abgewiesen.

Bekannte Completion (ConfirmedEnded oder belegtes NotStarted) validiert aktuellen Dispatch,
Requestzustand und BindingRevision. Outputgrenze über vorhandene ProviderReplyPolicy
wiederverwenden, keine gekürzte Ausgabe. Neue Bindingidentität muss zum reservierten
Adapter/Format/stabilen Bezug passen. Unter einem Commit: Results, Requestsnapshot mit
EndedUtc, History mit monotoneOrdinal/Guid, Sessionbinding/revision/UpdatedUtc/BlockReason
über SessionBindingPolicy und ReleasedUtc/ReleaseReason der Reservation. NotStarted
veröffentlicht keinen neuen Head. Historyquery nutzt denselben nativen PageReader und
HistoryOrdinal/HistoryId, kein vollständiger Transkriptreplay. Bestehende History bleibt
unverändert. Unknown wird ausdrücklich nicht über diesen Port freigegeben; MarkUncertain/
Recovery folgenT052/T061/T062. Cancel-/Closezustandsmaschine T070+ bleibt gesondert offen.

## Tests vor Green

Reservierung vor tatsächlichem Providerzugriff, unveränderte Snapshotdaten auch nach
späterer Bindungsänderung; FIFO, Session-/Workspace-/Globalgrenze, parallele Reservierungs-
konkurrenz und echte DBconstraints. Persistenz bei Reopen ohne neue Sendberechtigung.
Observationack erst nach tatsächlichem Write, bestätigter Head unverändert. Bekannter
Abschluss persistiert Result/History/Binding/Release und ermöglicht nächsten FIFOhead;
Outputgrenze exakt/1Byte darüber, Providercontextfehler verändert null frühere Einträge.
CASkonflikt und doppelte/gleichzeitige Completion erzeugen null Teilwirkungen/zweiten Erfolg.
Realer Triggerfehler nach Teilwrites rollt sämtliche Abschlussänderungen zurück und hält
Reservation. NativeHistorykeyset und Endzeitpunkte prüfen. Zuerst fachliches Red an den
fehlenden Ports, dann minimaler Green. Crashbarrieren vor/nach Reservation/Completion und
SC-009 bleibenT048/T051/T055; diese Vertikale behauptet sie nicht als bereits ausgeführt.

## Gemeinsame Replyregel vor Green

Gemessen2026-10-29T11:26:26Z: elf Reservierungs-/Abschlussfälle und acht Callback-/
Integritätsfälle fachlich rot an fehlenden Ports. Zusätzlich geprüft: einziger Aufrufer
von DispatchReplyPolicy.Normalize ist RequestDispatcher; er validiert Registrierung
bereits vor Send gegen das originale Binding. Die bestehende reine Normalisierung wird
öffentlich mit originalem Binding statt ProviderRegistration wiederverwendbar gemacht,
damit Storage und Dispatcher dieselbe Outcome-/Identity-/Outputregel anwenden. Der Kern
behält seine vorherige Registrierungskontrolle, keine Hersteller- oder Provider-I/O-
Abhängigkeit in Storage. Fehlende widerspruchsfreie Snapshotgraphen ergeben StorageUnavailable,
kein Sendrecht. SQL-CurrentRead prüft Snapshot/Reservation/Session/Submission/Sequence/Revision.

## Autoritative Requestsession gegen gespeicherten Graphen

Nach Recoverygreen545 Tests ist StoredDispatchAccess.Read erneut geprüft. Es vergleicht
Envelope mit den skalaren Reservationfeldern, liest aber r.SessionId nicht mit. Ein
nachträglich inkonsistenter Request-Fremdschlüssel könnte daher bei intakter Envelope
unentdeckt bleiben. Vor Korrektur neuer Regressionsteil request-owner: zweites echtes
Sessionobjekt, nur Requests.SessionId per parametrisierter Probe auf dessen UUID ändern,
GetReservedDispatch muss StorageUnavailable liefern statt Sendrecht. Danach Scalarowner
in denselben Read aufnehmen/vergleichen; keine Lockerung/Repair/Nativeoperation. Source
StoredDispatchAccess nach cancellationfähigem Read wird unmittelbar bei der Probe gehasht;
kein bereits beobachteter Fehler behauptet, bis Red ausgeführt ist.
