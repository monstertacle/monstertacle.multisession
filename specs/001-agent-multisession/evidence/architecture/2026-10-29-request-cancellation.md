# Dauerhafte Abbruchabsicht und wartender Abbruch

Gemessen 2026-10-29T12:33:29Z, Ausgang b2e6435.
RequestAdmission.cs SHA256 deee93746f1f6905263267731730c336a4940ddf07c6854bfb4955ce87f4faed;
QueuedCancellationTransactions.cs 5b63c105c92df9673a18f90a7f804a81c644c45287da588bdb97fff867ec3e0f;
StoredDispatchAccess.cs 697a392b0946a2203eb614c71de30457b0da255427fcd565143ac515672534c1;
CompletionTransactions.cs d819dd453dff8963a32babdd80a3561ec274271bc46e09a02d67af5486dc1a5c.

## Bestand

T070/T071/T075/T077. Öffentlicher IRequests.CancelRequestAsync endet in einem Stub in
RequestAdmission; RuntimeRequests delegiert nach Lifecycleprüfung dorthin. ISessionStore
hat bereits zehn schreibende Methoden; keine davon kann Abbruchabsicht speichern.
SQL QueuedCancellationTransactions erzeugt bereits Cancelled/NotStarted im Recovery-Close
mit Result/History/Intent innerhalb einer Transaktion. RequestStateMachine schützt die
Finalisierung. SQLWorker serialisiert Cancel und Reservation/Completion. RequestSnapshot
hat CancellationRequestedUtc; SQL dieselbe Spalte. Keine Migration notwendig.
StoredDispatchAccess verweigert Sendberechtigung bei Cancel-Intent; Completion verwendet
denselben Reader und muss dennoch einen bekannten Abschluss committen dürfen.

## Entscheidung vor Code

Kleiner IRequestCancellationStore-Port als verpflichtende geerbte Storefacette, nicht eine
elfte Methode in ISessionStore. Public RequestCancellation delegiert ausschließlich den
atomaren lokalen Intent, kein Provider-I/O. SQLite-Readdelegationsbasis implementiert die
neue Facette; eigener CancellationTransactions hält Fachtransaktion, keine SQLobjekte im Kern.
Queued wird sofort endgültig Cancelled/NotStarted, mit unverändertem Input/Sequence und
genau einem Historieneintrag. Running erhält ausschließlich ersten Intentzeitpunkt;
State/Certainty/Reservation bleiben unverändert. Terminaler Aufruf ist idempotent und
ändert kein Ergebnis. Fehlende ID NotFound, Caller-Cancel vor Transaktion ohne Änderung.
Bestehenden Queued-Einzelfinalisierer aus Session-Cancel wiederverwenden. Completion liest
auch bei Intent den vorhandenen Running-Snapshot, bleibt durch den bekannten Endnachweis
und Einmalfinalisierung geschützt. Native Beobachtungs-/Ausführungsunterbrechung wird im
anschließenden T071/T077-Zyklus ergänzt, hier keine 2-s-Laufend-Abnahme behaupten.

Tests vor Umsetzung am öffentlichen RequestAdmission und realem SQLite: Queued wird nie
reserviert, wiederholter Cancel und Reopen verändern Inhalt nicht; Running bleibt InProgress
mit Intent und belegtem Slot, bestätigter Abschluss darf gewinnen; Unknown bleibt ungeklärt;
fehlend/Caller-Cancel und Cancel-vs-Reservation/Completion behalten genau ein Ergebnis.
Kontrollierte Unitstorefacette wird nach demselben Vertrag ergänzt, ohne produktive native
Semantik vorzutäuschen. Bestehende Fälle bleiben Regression, tatsächlich neue rote Fälle
werden getrennt protokolliert. Full-Guard/Format/Coverage unverändert. Keine offene Frage
für diesen lokalen Schnitt; T071/T077/T078/T079 besitzen den restlichen Ausführungsauftrag.

Zusatz vor Änderung, gemessen 2026-10-29T12:35:44Z: ObservationTransactions.Commit
verwendet ebenfalls den Sendberechtigungsreader (SHA256
2f68fb16a8fa58f8f2d43736bf06675167b3f763ae7eceb4ee4fc1051638ad21). Tatsächlicher
Redfall cancel-observation-red: nach Intent wird späte OperationAccepted mit RevisionConflict
verworfen, obwohl Request weiterhin Running und reserviert ist. Entscheidung: wie Completion
mit includeCancellation lesen, nur Beobachtung speichern, kein neuer Send/Headfortschritt;
nach Terminalisierung verhindert der Running-Filter weiterhin späte Änderungen. Test prüft
persistierte Operations-ID im Unknown-Recoveryfall und unveränderte Slotbelegung.
