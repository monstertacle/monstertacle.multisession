# Orchestrierungsverträge v1 — verbindlicher Zielentwurf

Stand 2026-10-28. Konkretisiert FR-005/FR-007/FR-018–FR-021 und die Analysebefunde.
**Noch nicht vollständig in C# umgesetzt.** T112–T115 sichern die folgenden Änderungen
vor ihrer jeweiligen Implementierung durch fachliche Red-Fälle ab. Der Paketstand bleibt
vor Veröffentlichung; dies ist keine ausgeführte Migration oder neue Produktfreigabe.

## 1. Reservierter Dispatch-Snapshot (U1)

TryReserveHeadAsync liefert weiterhin ExecutionReservation. In **derselben** Transaktion
werden Running/InProgress, DispatchId, die belegten Slots/Schlüssel und ein unveränderlicher
ReservedDispatch gespeichert. Der Dispatcher liest ihn über die neue fachliche Operation
`ISessionQueryStore.GetReservedDispatchAsync(dispatchId, cancellationToken)`.

| ReservedDispatch-Feld | Bedeutung |
| --- | --- |
| Reservation | Bereits gespeicherte ExecutionReservation |
| Session | Bei Reservierung eingefrorener SessionSnapshot mit ProviderId, Context und Binding |
| Submission | Gespeicherter SubmitRequest mit ausschließlich neuer unveränderter Eingabe und Ausführungsfrist |
| Sequence | Bestätigte Annahmesequenz |
| BindingRevision | Persistierte Revision des bestätigten Fortsetzungsstands beim Reservieren |

Reservation.RequestId/SessionId, Submission.SessionId und Session.SessionId müssen
konsistent sein. Session.Binding ist ein gültiges Adapterbinding; dessen SessionReference
darf nur bei zulässigem Lazy-Open fehlen. Eine unbekannte oder bereits freigegebene
DispatchId, ein nicht mehr laufender oder bereits zum Abbruch markierter Auftrag liefert
null; der Dispatcher sendet dann nicht. Ein Storefehler wird nicht als leere Queue kaschiert.
Die Prüfung gilt zum Lesezeitpunkt; ein danach eintreffender Cancel wird als Abbruch eines
laufenden Dispatch behandelt, nicht durch eine vorgetäuschte atomare Netz-/DB-Transaktion.

Die Operation ist rein lesend und startet weder Providerarbeit noch einen zweiten Versuch.
Der Runtime-Worker verarbeitet jede von ihm neu erworbene DispatchId höchstens einmal.
Nach Neustart werden alte Reservierungen niemals erneut als Sendberechtigung verwendet.
Ein wiederholtes Lesen ist kein Retry und keine Erlaubnis für einen weiteren Send.
Input und Binding dürfen nicht aus aktuellen Hostobjekten oder aus Historien-Replay
rekonstruiert werden. MaxOutputBytes kommt aus den beim Runtime-Start fixierten Optionen;
ProviderCall wird aus diesem Limit und dem ReservedDispatch gebildet.

BindingRevision beginnt bei 0 für das nach Open bestätigte beziehungsweise explizit lazy
Binding. Jede atomare Veröffentlichung eines neuen bestätigten Fortsetzungsstands erhöht
sie um 1. Neue native IDs/Operations-IDs aus Beobachtungen werden vor Callbackbestätigung
dispatchgebunden gespeichert; sie allein veröffentlichen keinen neuen Fortsetzungskopf.
Completion prüft das beim Dispatch gespeicherte BindingRevision gegen die aktuelle
Sitzungsrevision und genau diesen noch offenen Dispatch. Bei Abweichung: RevisionConflict,
kein Ergebnis-/History-/Binding-Erfolgscommit und keine Reservierungsfreigabe.
Ein abgelehnter oder fehlgeschlagener Commit darf nicht zu einem erneuten Send führen.

## 2. Recovery ohne Dummy-Request und mit lesbarer Revision (U2/U3)

`RecoveryTarget` besteht aus `Kind` und `Id`:

| Kind | Id | RequestId im Fallsnapshot |
| --- | --- | --- |
| SessionOpening | Lokale SessionId der ungewissen Eröffnung | null |
| RequestExecution | RequestId der ungewissen Ausführung | Zugehörige RequestId |

Keine Sentinel-UUID oder künstliche Anfrage ersetzt SessionOpening. RecoveryCaseSnapshot
enthält Target, SessionId, optionale RequestId/DispatchId, unveränderliches CreatedUtc,
Revision, den offenen/aufgelösten Fallstatus, Blockgrund, Gewissheit und verfügbare opaque
native Bezüge. Er enthält keine Credentials, Prompts oder vollständige Eingaben.

Die neue öffentliche Leseoperation `IRecovery.GetRecoveryCaseAsync(target, token)` liefert
den Fallsnapshot oder null. `ListRecoveryCasesAsync` liefert `Page<RecoveryCaseSnapshot>`
für offene Fälle. `InspectRecoveryAsync` adressiert ebenfalls das Target, liest nur den
bekannten nativen Bezug und verändert weder Fall, Revision noch Reservierung. Fehlt ein
inspizierbarer Bezug oder die Fähigkeit, meldet es UnsupportedProviderAction/Unknown,
ohne native Öffnung, Send oder erfundene Endbestätigung.

RecoveryDecision enthält DecisionId, **Target statt RequestId**, ExpectedRevision, Kind,
Explanation und EvidenceReference. Der Store verwendet eine atomare ResolveRecoveryAsync-
Operation für beide Targetarten; die frühere requestgebundene ResolveReservationAsync-
Signatur ist seit T113 abgelöst. Der Store erhält neben der Entscheidung ein optionales
confirmedBinding aus dem vorab ausgeführten Reopen. Dieses gehört nicht zur öffentlichen
Operatorentscheidung: der Service prüft native Identität/Adapter/Format, der Store prüft
ExpectedRevision erneut und veröffentlicht Klärung und bestätigtes Binding gemeinsam. Ein unbekanntes/fremdes Target ergibt NotFound.

Revision ist eine positive persistierte long-Zahl je Target: erster Fall Revision 1,
jede angenommene zustands-/beweisrelevante Falländerung erhöht sie genau um 1. Auch nach
Auflösung und erneutem Öffnen desselben Targets wird sie nicht zurückgesetzt. Reine
Reads/Inspect/erneute unverändernde Abweisung erhöhen sie nicht. ExpectedRevision muss
beim atomaren Entscheidungscommit exakt passen; nur ein konkurrierender Commit mit
derselben Revision kann gewinnen. Die anderen ergeben RevisionConflict ohne Teilwirkung.
Eine doppelte DecisionId wird ohne erneute Wirkung als RevisionConflict abgewiesen;
es gibt keine automatische Wiederholung. GetRecoveryCaseAsync kann den inzwischen
aufgelösten Fall weiterhin liefern, solange die zugehörige Sitzung nicht legitim gelöscht ist.

### Ungewisse Eröffnung

Blockierung und SessionOpening-Fall werden gemeinsam gespeichert, sobald ein unsicherer
Open bekannt wird. Beim Wiederanlauf wird auch eine übrig gebliebene Opening-Sitzung
konservativ als solcher Fall erfasst; sie wird nicht automatisch erneut geöffnet.
Ein fehlgeschlagener Speichercommit wird als Fehler gemeldet, nicht als gesicherte Sperre.

CloseSession setzt Closing und hält den Fall sowie eventuell bestehende Reservierungen
offen, solange kein belegtes Ende vorliegt. ConfirmEnded benötigt ausdrückliche begründete
Untersuchung wie bei RequestExecution. Bei bestehender Schließabsicht und ohne weitere
offene/ungewisse Arbeit darf anschließend Closed atomar erreicht werden. Ohne Schließabsicht
ist Open/None nur nach erfolgreichem Reopen **desselben bekannten** Bindings zulässig.
Fehlt dieses oder schlägt Reopen fehl, bleibt der Fall offen/blockiert; es wird keine neue
native Identität erzeugt. Der Integrator kann nach neuem Read mit aktueller Revision die
Schließung veranlassen. Native Klärungs-I/O erfolgt außerhalb der Transaktion; vor Anwendung
der gewonnenen Belege wird ExpectedRevision erneut atomar geprüft. RequestExecution behält
sein historisches Fehler-/Interrupted-Ergebnis und wird niemals nachträglich Succeeded.

## 3. Annahmegewissheit und Nachschlagen (U4)

`AdmissionStatus` unterscheidet NotAccepted, Committed und Unknown **unabhängig von**
ExecutionCertainty. Ein Auftrag kann lokal committed, aber extern noch NotStarted sein.
PublicError erhält ein optionales AdmissionStatus; für andere Operationen bleibt es null.
SessionId und eine bereits erzeugte Kandidaten-RequestId dürfen als sichere lokale
Nachschlageschlüssel enthalten sein, nicht als Bestätigung der Annahme.

| Beobachtung | Öffentliche Aussage |
| --- | --- |
| Lokale Validierung scheitert oder Store bestätigt Rollback/Nichtbeginn | NotAccepted; kein Receipt |
| Store bestätigt erfolgreichen Commit | Committed; tatsächlichen RequestReceipt zurückgeben, auch bei inzwischen ausgelöstem Caller-Token/Timer |
| Unterbrochenes Warten/Storefehler ohne Beweis für Commit oder Rollback | Unknown; kein Erfolgsreceipt und keine sichere Nichtannahme behaupten |

Vor Beginn der Commitoperation erzeugt RequestAdmission genau eine Kandidaten-RequestId.
`RequestAcceptance(RequestId, Submission)` wird als Eingabe für CommitAcceptanceAsync
verwendet; diese Operation übernimmt dieselbe ID und vergibt Sequence/Queueplatz atomar.
Damit verändert sich in T114 der bisherige Storeparameter, nicht die öffentliche Submit-
Signatur. ID-Kollisionen werden abgewiesen, nicht mit fremden Requests zusammengeführt.
Jeder neue Submit erzeugt eine neue Kandidatenkennung; keine automatische Deduplizierung,
kein automatisches Wiederholen eines unklaren Commits.

Bei Caller-Cancel erhält der Aufrufer eine `AdmissionCancellationException`, abgeleitet
von OperationCanceledException, mit dem bereinigten PublicError samt AdmissionStatus und
Kandidatenkennung soweit bereits vorhanden. Bei Frist-/Speicherfehlern verwendet
SessionOperationException dieselben Metadaten mit AdmissionTimedOut/StorageUnavailable.
ExecutionCertainty wird separat aus tatsächlich vorhandenem Ausführungswissen bestimmt;
Unknown-Annahme darf insbesondere kein sicheres externes NotStarted implizieren.
Der Host kann GetRequestAsync(Kandidatenkennung) beziehungsweise ListRequestsAsync nutzen.
Ein vorläufig fehlender Datensatz während noch unklarem Commitausgang ist **kein** Beweis
für Nichtannahme. Es gibt keinen automatischen erneuten Submit oder späteren Receipt-Push.

## 4. Begrenzte Listen und Cursor (A2)

Für alle Session-/Request-/History-/Recoverylisten gilt explizit `1 <= pageSize <= 100`;
es wird weder still gekappt noch korrigiert. Die empfohlene Hostseitengröße ist 50,
kein impliziter Parameterdefault. Gemeinsame benannte Konstanten stehen seit T115 in
Contracts/PageLimits.cs. Cursor ist null für die erste Seite; ein leerer String ist ungültig.

| Liste | Aufsteigender stabiler Sortierschlüssel |
| --- | --- |
| Sessions | CreatedUtc, SessionId |
| Requests einer Sitzung | Sequence, RequestId |
| History einer Sitzung | HistoryOrdinal, HistoryId |
| Offene Recoveryfälle | CreatedUtc, Target.Kind, Target.Id |

UUIDs werden im kanonischen D-Format ordinal verglichen, Zeitpunkte als UTC; Target.Kind
hat die feste Ordnung SessionOpening vor RequestExecution. HistoryOrdinal ist ein beim
Einfügen monoton vergebener sitzungsbezogener Zähler; HistoryId eine lokale UUID. Auch
mehrere lokale Beobachtungen zur gleichen Anfrage sind damit eindeutig sortierbar.

Der maximal 2048 UTF-8-Bytes lange Cursor ist eine Base64url-kodierte JSON-Struktur mit
Version=1, StoreId, Listenart, SessionId bei sitzungsbezogenen Listen und letztem vollständigem
Sortierschlüssel. Parsing ist strikt und kulturunabhängig. Unbekannte Version, ungültiges
Encoding/Schlüssel/Größe, andere Listenart, fremder Store oder falsche Session ergeben
InvalidInput ohne Teilresultat. Ein Cursor ist kein Berechtigungsnachweis und enthält keine
Secrets/Inhalte. Die Store-/Hostzugriffskontrollen bleiben unabhängig erforderlich.

Keysetfortsetzung liest strikt hinter dem letzten Schlüssel, höchstens pageSize Elemente;
ein intern gelesenes zusätzliches Element entscheidet über NextCursor. Am Ende ist dieser
null. Löschen des zuletzt gelesenen Elements invalidiert die Fortsetzung nicht. Mehrere
Seiten sind eine **Livesicht**, keine Snapshottransaktion: neu eingefügte Elemente hinter
dem Cursor können erscheinen, davor erst bei neuem Scan. Zustände können sich zwischen
Reads ändern; zurückgegebene DTOs selbst sind unveränderliche Snapshots. So werden weder
Snapshotisolation noch vollständige Auffindbarkeit durch nur einen parallelen Scan behauptet.

## 5. Beleggebundener Fortsetzungskopf (T116)

Für `RequiresOperationAwareRecovery` verwendet RecoveryService den optionalen Port
`IExecutionRecoveryProvider` statt des gewöhnlichen Reopen. Ein providerdefinierter neuer
Kopf muss aus der bekannten ungewissen Operation verifiziert werden. Ohne notwendigen
Bezug oder passenden Port bleiben Fall und Reservierung gesperrt. Die vorhandenen
Identitäts-/Adapter-/Versions- und CAS-Regeln gelten unverändert.

Ein erfolgreich bestätigtes `SessionEstablished` darf die zuvor leere SessionReference
im RecoveryCase.Binding konkretisieren. Dies veröffentlicht weder einen neuen bestätigten
Kopf noch das Binding im SessionSnapshot. Der Fall bewahrt die bekannte native Identität
für explizite Klärung; der Service erhält diese bekannte Fallbindung, und der Store prüft
sie beim gemeinsamen Entscheidungs-/Bindingcommit. Kein beobachteter Bezug wird durch
Neueröffnen ersetzt. Produktive Persistenz dieser Projektion gehört zu T060–T062.

## 6. Atomare Sperre bei unbrauchbarer Fortsetzung (T117)

CommitCompletionAsync speichert bei `ContinuationAvailable=false` oder einer bekannten
ProviderSessionExpired-Kategorie den Blockgrund ProviderUnavailable zusammen mit Result,
History, bestätigtem Binding und zulässiger Reservierungsfreigabe. Null/true alleine ändern
keine bestehende Policy. RecoveryRequired wird nicht herabgestuft. Eine ungewisse Antwort
läuft weiterhin über den separaten Unsicherheitscommit und bleibt reserviert. Die Sitzung
nimmt mit bekanntem Fortsetzungsblocker keine weiteren Requests an; bereits Queued bleiben
für explizite Close-/Stop-/Klärung sichtbar. Kein neuer nativer Bezug und keine automatische
Wiederholung. Produktive Transaktionen müssen dieselbe atomare Grenze erfüllen.

### Autoritativer Snapshot bei bestätigter Eröffnung

CommitSessionBindingAsync liefert den in derselben Transaktion bestätigten
SessionSnapshot. CreateSessionAsync gibt diesen zurück. SettingsRevision und CreatedUtc
bleiben unverändert; UpdatedUtc bezeichnet die letzte gespeicherte Sessionmutation.
ClosedUtc ist vor tatsächlichem Closed null, auch bei Closing mit ungeklärter Arbeit.
Die chronologischen lokalen UTC-Zeitpunkte ersetzen keine monotonen Ausführungsfristen.

### Lokale Konfigurationssperre vor Workerstart

CommitConfigurationAvailabilityAsync wendet ausschließlich eine lokale Availability-
entscheidung atomar an. SessionSnapshot.ConfigurationUnavailable markiert deren Herkunft.
Wieder gültige Konfiguration darf nur eine so markierte ProviderUnavailable-Sperre lösen;
RecoveryRequired und nicht markierte native Fortsetzungsrestriktionen bleiben bestehen.
Registry-/Binding-/Executionkind-/Workspaceprüfung erfolgt nach Bootstrap vor Workers.
Opaque Modell-/Settingsdaten bleiben unverändert; native Auth-/Modellverfügbarkeit wird
nicht durch einen erfundenen lokalen Featurekatalog vorgetäuscht.

## 5. Atomare lokale Cancel-/Close-Facetten

ISessionStore erbt die schmalen Facetten IRequestCancellationStore und ISessionClosureStore.
RequestCancellationAsync speichert bei Running nur den ersten CancellationRequestedUtc;
Queued wird atomar Cancelled/NotStarted samt Result/History. Finale Ergebnisse bleiben
unverändert. CancelQueuedSessionAsync verarbeitet die Menge noch wartender Einträge einer
lokalen Session in einem Commit mit begrenzten Sequence-Keysetseiten; eine leere Treffer-
menge ist ein erfolgreicher leerer Sammelabbruch. Dieser Storeport ist kein öffentlicher
Close oder Ersatz für IRequests.CancelRequestAsync mit dessen NotFound-Vertrag.

BeginSessionCloseAsync sperrt Annahme, finalisiert Queued und speichert Running-Intent
zusammen. RefreshSessionCloseAsync prüft aktive/ungewisse Requests, unfreigegebene
Reservierungen und offene Fälle. Closed benötigt die Abwesenheit aller dieser Hindernisse;
ClosedUtc wird einmal gesetzt. Opening bekommt einen eigenen Fall. Ein später Binding-
Commit darf Closing/Closed nicht wieder öffnen. Der SessionClosure-Kern beobachtet den
Fortschritt begrenzt; Completion beziehungsweise Recovery aktualisieren Closing auch ohne
wartenden Caller. Keine dieser Transaktionen ruft einen Provider auf oder löscht dessen Daten.
