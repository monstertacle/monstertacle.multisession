# Native SQLprojektion für Unknown, Bootstrap und explizite Klärung

Gemessen2026-10-29T11:42:45Z, Ausgang6072d52. T052/T061/T062 und RecoveryteilT063.
SqliteStoreState.cs SHA256b42f108c031daf5f4649fef2a00cedb6367cba1ac71062d1653b591b482f4ccc;
StoredDispatchAccess.cs a338c8c18de2a1794520917dd8671fd79f2c15e57cc9d07e507420c82891bd73;
RecoveryService.cs99f23df816ae10bc21f7d886e5372a0ad8c868a135f3790baf5cb0cd0e924749.

## Bestand und offene Ports

Echte Reservation/Envelope/Observation und bekannte Completion sind grün. MarkSessionBlocked,
MarkUncertain, ResolveRecovery und Casequeries sind noch Gerüste. Bootstrap lehnt alte
Opening/Running derzeit sicher ab. Schema hat Case/Decisiontabellen und persistierte
Envelopekennungen, keine Schemaänderung nötig. GetReservedDispatch verweigert Sendrecht
vor Bootstrap, bei nicht mehr Running oder bei Cancelintent/Release. Resultwriter fügt
History hinzu und lässt alte Einträge unverändert.

Common RecoveryService liest öffentliches Target/Revisionssnapshot, macht Reopen/Inspect
außerhalb DB und prüft stabile Adapter-/Format-/Sessionidentität. Operationsbewusster Port
ist implementiert; Closeintent ist keine Endbestätigung. Store muss Fallrevision nach
externem I/O erneut atomar prüfen. Kontrollierter Store ist nur fachliche Referenz, kein
Beleg echter SQLhärtung. Openingunsicherheit kann auch einen inzwischen Open bestätigten
Datensatz betreffen (verlorene lokale Rückbestätigung); der Markport darf dann keinen Fall
verlieren. Ein Dummyrequest bleibt verboten.

## Entscheidung vor Code

Casequeries lesen typisierte Snapshots und native bounded keysets mit Store-/Targetscope.
Unknowntransaktion setzt lokal Interrupted/Unknown, Result/EndedUtc und neue History,
blockiert Session, eröffnet RequestExecutioncase mit bekanntem erstem Nativebezug und
Operations-ID aus Envelope, lässt Reservation/Bindingrevision/confirmedHead unverändert.
Wiederholung kann keine zweite Finalisierung/History erzeugen.

MarkSessionBlocked nach unsicherer Eröffnung erzeugt SessionOpeningcase auch bei inzwischen
Open, ohne Request/Reservation. CreatedUtc bleibt für denselben Fall erhalten, revisions-
sichere Veränderungen erhöhen Revision. Keine Maskierung vorhandener Recoverypflicht
als bloße Providerunverfügbarkeit. Case-/Sessionupdate teilen Commit.

Bootstrap: Lease/Schema sind vorher geprüft, dann FK-/Running-/Reservationkonsistenz ohne
Reparatur verlorener Marker prüfen. Unter einem Commit alte Running zu Interrupted/Unknown
projizieren, Openingfälle auffindbar halten, Reservationen behalten; erst danach Annahme
scharfstellen. Bestehende Unknownfälle/History bei zweitem Start unverändert. Kein Open,
Send oder Reopen im DBworker. Provider-/Konfigurationsverfügbarkeitskoordination außerhalb
Storage bleibt konkreter restlicher T061-Auftrag vor kompletter Runtimeabnahme.

CASentscheidung: Pflichtfelder/Kind/positive Revision, vorhandener offener Fall und global
neue DecisionId. CloseSession speichert Closing/RecoveryRequired und beendet ausschließlich
noch Queued als Cancelled/NotStarted, niemals die aktive Unknownreservation. ConfirmEnded
lässt historischen Outcome unverändert, aktualisiert aktuelle Resultgewissheit, löst Case
und Reservation und bestätigt nur kompatibles Reopenbinding. Bei Closing ist kein neuer
Nativebezug nötig, Closed nur ohne weitere offene/ungewisse Arbeit. ClosedUtc ausschließlich
dann. Alte History bleibt damaliger Beobachtungsbeleg; Decisiontabelle hält spätere Klärung.
Alle Effekte einschließlich DecisionId in einem Commit. Keine Migration oder Exactly-once.

## Testplan vor Green

Case ohne Dummyrequest, auch nach bereits bestätigter Bindung; Unknown hält Slot/Head und
übernimmt erstbeobachtete native Identität; öffentliches Casepaging und unverändertes
CreatedUtc. Gleichzeitige/veraltete/doppelte Entscheidungen genau einmal; ConfirmEnded
behält Interrupted und historische History, Close allein hält Unknown, späteres Endbeleg
schließt und queued Daten gehen nicht verloren. Reopen anhand bekannter Identität über
RecoveryService ohne neue Session. Bootstrap persistierter Running-/Queued-Ausgangslage,
unabhängige freie Session arbeitet weiter, zweiter Bootstrap keine Duplikate; inkonsistente
Marker scheitern ohne leere Ersetzung. Reale fachliche Prozess-Killmatrix/SC-009 und native
Plattformleasen bleiben eigene T048/T055/T093-Aufträge, kein Dispose als Crash ausgeben.

## Verknüpfte Caseidentitäten vor Korrektur

Nach grüner Recovery-/Ownerregression erneut Casequery gelesen: Target/Session/Revision/
Status werden gegen scalarRows geprüft, RequestId/DispatchId aus JSON noch nicht.
Ein einzelner widersprüchlicher JSONbezug könnte eine Operatorentscheidung auf falsche
Request-/Reservationdaten lenken. Neue Regression vor Korrektur verändert ausschließlich
SnapshotJson.RequestId bzw.DispatchId und verlangt StorageUnavailable beim Lesen. Danach
beide skalaren Bezüge sowie Targetkindkombination atomar im Read prüfen. Kein Repair oder
leeres Ergebnis als Fehlerersatz. Quellenhash direkt beim Regressionseinstieg gemessen,
keine fachliche Fehlbeobachtung vor tatsächlich ausgeführtem Red behauptet.
