# Belegtes externes Ende nach Cancel oder Deadline

Gemessen 2026-10-29T12:48:17Z, Ausgang 0f605eb.
RequestDispatcher.cs SHA256 8f85a658392e83735c8394256238bb60b222157cfab03144ece7191c9a3a7637;
DispatchObservationSink.cs 96a13a437555df4cc193788f0c00c5f676675547a500d00e8dd86533eed2f83a;
RecoveryService.cs 99f23df816ae10bc21f7d886e5372a0ad8c868a135f3790baf5cb0cd0e924749;
GeminiRecoveryAccess.cs 5e500368df54498bc9d5d60a75a57f3352d66752d437ab444afef6f6e4c02a27.

## Untersuchte Grenzen

T071/T077/T082. Dispatcher besitzt jetzt Deadline/Intenttoken, hält aber nach jedem
lokalen Tokenende pauschal Unknown. ISessionProvider.CancelAsync unterscheidet laut Vertrag
Abbruchwunsch und tatsächliches Ende. Send kann kooperativ mit explizitem ConfirmedEnded
zurückkehren; dieses Ergebnis darf nach Tokenende noch beobachtet werden. Sink acked opaque
OperationAccepted dauerhaft, speichert die Operation bisher nicht als auslesbare lokale
Eigenschaft. OpenAI/Gemini Cancel pollt native Endzustände und prüft Operations-ID; Reply
trägt dort kein Binding. Bei Gemini reicht das alte Chainhead allein nicht: Capability
RequiresOperationAwareRecovery und vorhandener IExecutionRecoveryProvider verlangen
operationsgebundene Read-Reopenprüfung. GeminiRecoveryAccess prüft ID/Lineage/terminalen
Zustand; erster fehlgeschlagener Chainroot ohne bestätigten Head wird bewusst verweigert.

## Entscheidung vor Entwurf/Code

Eigener begrenzter Endbelegleser, keine neue Storemethode. Ein einziges monotones
Systemtimerbudget von einer Sekunde ab lokalem Tokenende gilt gemeinsam für kooperativen
Sendreply, native Cancel und gegebenenfalls operationsgebundenes Reopen. Es ist unabhängig
von bereits ausgelöster Hosttest-Deadline; es verlängert weder Requestfrist noch erneuert es
sich pro Versuch. Benannte RuntimeDefaults-Konstante. Parallel beobachtete Kandidaten werden
vollständig fehlersicher konsumiert; keine wartende Aufgabe blockiert die lokale Finalisierung.

Native Cancel nur bei deklarierter Fähigkeit und erfolgreich persistierter Operations-ID,
maximal einmal, niemals Sendwiederholung. Cancel-Reply NotStarted beweist kein Ende der
ursprünglichen Ausführung und reicht nicht. Terminales ConfirmedEnded wird durch dieselbe
Binding-/Outcomeprüfung normalisiert. Operation-aware Adapter ohne vollständiges Replybinding
müssen über den vorhandenen Recoveryport dieselbe native Operation verifizieren; fehlende
Ports/Bezüge/Fehler/fremde Identität bleiben Unknown, kein Fallback auf alten Head. Ein erstes
beobachtetes Sessionreference darf nur als bekannte opaque Identität an diesen Port gehen,
nicht als erfundene Fortsetzung. Nur dessen verifiziertes Binding wird atomar committet.

Der lokale Ausgang bleibt Cancelled bzw. TimedOut, Output leer/unvollständig; ein verifiziertes
Binding und ContinuationAvailable werden übernommen. ConfirmedEnded gibt Reservation frei,
NotStarted nur aus dem ursprünglichen Sendreply, sonst Unknown behält Case/Slot. Unbestätigte
Observation oder Storagefehler können nicht durch native Endbestätigung überschrieben werden.
Callback-/Monitorcleanup bleiben getrennt. Späte normale Antworten ändern nie ein finalisiertes
Ergebnis. Die Bestätigung des Endes ist kein nachträglich behaupteter Erfolg.

Tests zuerst: kooperativer expliziter Endreply/native Operation für Cancel und Deadline;
Unknown/NotStarted-Abbruchbestätigung sowie fremdes Binding halten Reservation; ignorierte
Canceltoken bleiben begrenzt. Operationsgebundene Bindingprüfung mit erfolgreichem und
verweigertem Port, fehlende ID null native Aufrufe. Echter SQLruntimefall bestätigt Ende
unter 2s und freigegebenen Slot. Vor Green tatsächliche Redfälle, danach alle bisherigen
Callback-/Recovery-/Crashregressionen plus Full-Guard/Coverage. Keine unklare Architekturfrage
für diesen Schnitt; Stop/Close und vollständige Storyaudit bleiben T072/T078/T079/T085.
