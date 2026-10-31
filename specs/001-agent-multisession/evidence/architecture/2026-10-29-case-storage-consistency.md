# Recoverycase- und Decisiongraph vor Start

Ausgangc3acedd, Messung2026-10-29T16:17:00Z: RecoveryCases SHA256
1c7a7246f42c42d3f8c4ebd039d16ff31518f1903a1da041bb075bc1ea6c8433,
RecoveryDecisionTransactions3331d8b29e077b7576461df10edb93d80bf0969b648b6d4fa71775c04e3da943,
RecoveryTransactions8e2dda031b41510d00d4bedd1875ef3cc5365877e1ec5e493db78d80ef56cfab,
StoredDispatchc7f191c000c292c8bae56a59195a8be4f007c71da13a7b296e77146e047af457.
Case.Read prüft Identität/Revision/Status/Request-/Dispatchlinks nur bei tatsächlichem
Read; Start liest bestehende Unknowncases und Decisions bisher nicht vollständig.
Unknown.Apply baut Binding exakt aus Frozen.Session.Binding plus acknowledged SessionRef
und bewahrt acknowledged OperationRef. ConfirmEnded verändert diese Caseevidenz nicht,
nur Status/Certainty/BlockReason/Revision; Sessionbinding kann danach fortschreiten.
Openingcase kann nach verlorener Bindbestätigung auch zu LifecycleOpen gehören, nicht
nur Opening. Neue Openingzeilen ohne Case bleiben konservativ reparierbar.

Vor Code entschieden: CaseStoreConsistency streamt alle Cases, vergleicht SQLprojektion/
UTC/Owner/definierte Targets/Status. Requestcase muss zu ursprünglichem Dispatch passen,
Binding/OperationRef zu zuvor geprüfter Frozen-/Ackevidenz. Open heißt Unknown/RecoveryRequired
und behält unreleased Unknownrequest; Resolved heißt ConfirmedEnded/None und freigegebene
bestätigte Anfrage. Unknownrequest benötigt passenden Opencase; Recoveryblock außerhalb
Opening benötigt Opencase. Openingcase hat keine Dummyrequest/Dispatch-/OperationID,
TargetId ist SessionId; bei Open stimmt Binding mit Sessionbinding überein. Resolved
Openingcase darf historische Bindung behalten, keine Gleichheit alter ContinuationHeads
mit aktuellem Sessionkopf fordern. Kein Provideraufruf in Konsistenzprüfung.

DecisionStoreConsistency streamt nach Target gruppiert, prüft Row-/Snapshot-ID/Target,
definierte Kindwerte, positive ExpectedRevision unter aktueller Revision, nichtleere
Erklärung/EvidenceRef und kanonische UTC-Appliedzeit. Resolvedcase braucht letzte passende
ConfirmEnded-Entscheidung (ExpectedRevision=CaseRevision-1); Open darf keine solche letzte
Bestätigung haben. Keine Annahme chronologischer Wallclock. Historische frühere Bestätigungen
bleiben nach späterer Caseeröffnung erlaubt; kein Umschreiben von Entscheidungsbelegen.
Gruppenstatus statt kompletten Decisions-in-Memory-Liste, Relationen gruppiert statt
ungeindexierter O(n²)-Scans. Neue Startprüfungen vor allen Recoverywrites, DBhash gleich.

TDD zuerst: beide Targetarten mit JSON-/Status-/Owner-/nativeEvidence-/Zeitdrift,
fehlendem Case/Block sowie malformed/unechten/fehlenden Decisions fachlich rot.
Bestehende Close-/mehrfachClose-/Confirm-/Opening-/NativeRoot-/Crashregression unverändert.
T065 bleibt bis vollständigem Audit offen; insbesondere Startupcancel bei langen Scans
ist im vorhandenen T076-Auftrag noch zu prüfen, native Leaseabnahme weiterhin US4.
