# Gespeicherte Einreichungen vor Recovery

Ausgang78cff08, Quellprüfung2026-10-29T15:46:30Z: RequestSubmissionRules.Validate prüft
Lifecycle, UTF8-Bytelimit, gemeinsame ExecutionDeadline und WriteIntent-/Workspace-
bindung. Vor Start wurden SubmissionJson nur bei schon finalisierten Historyjoins gelesen;
Queued-/Runningdaten können weiterhin malformed oder fremd sein. Persistierte Timeoutwerte
sind pro Anfrage eingefroren; Queuedzeit verbraucht keine Ausführungsfrist. Storeoptionen
können bei späterem explizitem OpenExisting andere Eingabelimits tragen, ursprüngliche
angenommene Texte bleiben dabei erhalten und dürfen nicht neu gekürzt/abgewiesen werden.

Vor Erweiterung: bestehende gemeinsame Regel in ValidateFrozen(request,context,maxBytes)
aufteilen; normale Annahme behält vorhandene Lifecycle-/Blockprüfung und Limits.
Readonly SubmissionStoreConsistency streamt alle Requests mit zugehörigem gefrorenen
Sessioncontext und verwendet gemeinsame Input-/Timeout-/Workspacegrundregeln mit int.MaxValue
statt neuem Hostlimit. Neue Ownershipprüfung request.SessionId==context.SessionId schützt
gegen fremden Context. Kein Revalidieren aktuellen Lifecycle für historische Submission,
keine neuen Netzwerk-/Provideraktionen, keine Frist- oder Annahmesemantikänderung.

TDD zuerst: Queuedsubmissionowner, leer/null/malformedInput, nicht-endliche/negative/Null-
Timeouts und fremder WriteIntentworkspace gegen Bestand rot; frischer Reopen mit kleinerem
Hostlimit muss alte vollständige Eingabe weiterhin reservieren können (positive Regression).
Alle Fehler vor Recoverywrite, DBhash unverändert. FrozenDispatch-/Reservation-/Recovery-
casegraph bleiben weitere konkret benannte T065-Schritte, keine Gesamtfreigabe behaupten.
Messung2026-10-29T15:51:14Z: RequestSubmissionRules1261cb51ca288c3b8e8019bb3b59ba83bb4ac0f48b5c892d70a960ef95347ff1,
ExecutionDeadlinef9989bd608ca6014b9788b9a82c9e0c0a27834e38d2e255d64ddb3e90ca7eb89,
RecoveryTransactionsfed44a71126eae7116bb33bafbf7abed7f7b6b534f6bc4921cea35a5c0897074.
Quellenprüfung korrigiert eine zunächst zu starke Beschreibung: Utf8MessageLimits verwendet
Encoding.UTF8 mit Defaultreplacement, keine strikt werfende Kodierung. Unpaarige UTF16-
Surrogate benötigen eine eigene echte Redprobe und ausdrückliche Fehlerbehandlung vor
Annahme/Ausgabe. Dieser übernommene Befund bekommt konkreten Folgeauftrag T119, Owner
aktuelle Gesamtimplementierung; erst die Probe entscheidet, kein künstlicher Red.
