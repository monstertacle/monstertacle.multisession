# Ergebnis- und Historygraph vor Recovery

Ausgang943dce0, Messung2026-10-29T15:41:23Z: ResultTransactions SHA256
536e019014160b9495c5e888f1f18323f585df9d95dcac5c6ee32be20f58da64,
RecoveryDecisionTransactions3331d8b29e077b7576461df10edb93d80bf0969b648b6d4fa71775c04e3da943,
StoreSerializationbf571442de20180295187df168a4eb5f9e089c0bb2604f5e348a00406bf91c6e,
RecoveryTransactionsfb2a6c9ed08e17f07e63bf8cb1c9d8f6513aefc54b7454cea27f579bed58c01b.
T065. Request-/Sessionprojektionen jetzt vor Startwrite geschützt. ResultTransactions
schreibt je lokal terminalem Request genau einen Results- und Historyeintrag. ConfirmEnded
ändert Request.Result/Results.ReplyJson ausschließlich um Gewissheit/Error.Certainty,
History bleibt ausdrücklich unverändert. EndedUtc/CompletedUtc sind lokale Endzeit,
nicht Zeitpunkt späterer externer Klärung. HistoryOrdinal ist Finalisierungsreihenfolge,
NICHT Request.Sequence: wartende Cancels können vor laufendem Kopf finalisiert werden.

Nächster begrenzter Schnitt: zuerst SQLrelationen prüfen: Queued/Running ohne Results/
History, jeder Terminalrequest genau ein Result und ein Historyeintrag. FKs bereits
vorher geprüft; pro Sitzung eindeutige positive HistoryOrdinal ohne Lücke (max=count).
Danach streaming Terminaljoin mit SubmissionJson/RequestSnapshot/Results.ReplyJson/
HistorySnapshot: Eigentümer/Request/Sequence/HistoryId/Ordinal stimmen mit Spalten,
Input exakt mit ursprünglicher Submission, CurrentResult exakt mit Results, CompletedUtc
exakt mit EndedUtc. HistoryResult entweder identisch oder einzig erlaubte Fortschreibung
Unknown→ConfirmedEnded gleicher Outcome/Output/Binding/ErrorCode/IDs; historisches Paar
muss bereits zulässig sein. Keine nachträgliche Succeeded-Reparatur und keine Umordnung.
Keine neuen Writes/Tabellen/Migrationen oder versteckter Historienreplay.

TDD: fehlende/zusätzliche Result-/Historyzeilen, malformed/abweichende Replies/Inputs/
Identitäten, falsche Completionzeit und Ordinalgaps gegen Bestand rot; anschließend
Neustart nach legitimer ConfirmEnded-Klärung als unveränderte positive Regression.
DBhash nach Ablehnung gleich. Noch offen: vollständige Submission-/ReservedDispatch-/
Reservation-/Recoverycasegraphprüfung einschließlich aktiver Unknownlinks; dieser Schnitt
ersetzt weder native Leaseprüfung noch kryptographischen Manipulationsschutz.
