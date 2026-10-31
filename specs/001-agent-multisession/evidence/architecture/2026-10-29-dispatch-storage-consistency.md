# Dispatch- und Reservierungsgraph vor Recovery

Ausgang9189574, Quellenmessung2026-10-29T16:02:20Z: StoredDispatchAccess SHA256
697a392b0946a2203eb614c71de30457b0da255427fcd565143ac515672534c1,
ReservationTransactions30a91d5772afd18f1f042b8058ed8f525ec293b62f9cf9f7ba897e1bdd1ec54e,
RecoveryCases1c7a7246f42c42d3f8c4ebd039d16ff31518f1903a1da041bb075bc1ea6c8433,
RecoveryDecisionTransactions3331d8b29e077b7576461df10edb93d80bf0969b648b6d4fa71775c04e3da943,
RecoveryTransactionse285bcc6e2c881779f96ed4d4ade922db7a4ec55c70b82a46f81695556f1f1cf.
StoredDispatchAccess prüft Frozenlinks bisher nur beim Lesen aktiver Runningreservierungen.
Unknown- und freigegebene Reservationszeilen werden dabei nicht gelesen; Start prüft nur
Runningfehllinks und aktive Rows. SQLnullvergleiche dürfen keine fehlende DispatchId verdecken.

Entscheidung vor Erweiterung: vor Startwrite alle Reservierungen streamen, gleiche
persistierte Reservierungs-/Sequenz-/Bindingrevision-/Ownerregeln wie beim aktiven Read.
Zusätzlich eingefrorene Submission exakt mit ursprünglicher Submission vergleichen;
frozen Profil/Provider/Model/ExecutionContext/Settingsrevision und Settings ordinal mit
unveränderlichem Sessioncontext. Settings als Mengen von ordinalen Schlüsseln/Werten,
keine Serialisierungs-/Dictionaryreihenfolge als Gleichheitsbeweis. Aktuelle Bindungsrevision
darf nach bekannten Abschlüssen höher sein. Historische ContinuationHeads bleiben gültig;
stabile native Sessionidentity darf sich nicht austauschen. Ackreferenzen nicht leer und
Sessionack konsistent mit bekannter nativer Identität. Keine Herstellerwahl oder neue IDs.

Topologie: DispatchId/BindingRevision/FrozenJson vollständig zusammen und genau passende
Reservation, keine versteckte Reservation bei fehlender DispatchId. Unknown benötigt immer
unreleased Reservation, auch bei lokal terminalem Zustand. Reservierungszeit muss exakt mit
Request.StartedUtc und Frozen.Reservation übereinstimmen; UTC und freigegebener Reason
konsistent. Keine Wallclockgrößenordnung. No-dispatch ist Queued oder wartend Cancelled/
NotStarted; belegte Rows können nach NotStarted-Abbruch legitim vorhanden und released sein.
Vorhandenen aktiven Read nicht erweitern: archivierte Snapshots erteilen niemals Sendrecht.

TDD zuerst: Unknown/known-released Daten mit Drift in frozen IDs/Sequenzen/Submission/
Profil/Model/Settings/Bindingrevision, Ackrefs sowie SQLzeiten/Workspace/fehlender oder
falsch freigegebener Reservation gegen Bestand rot. DBhash nach Ablehnung gleich.
Frühere NativeRoot-/Recovery-/Late-End-/Crashfälle bleiben positive Regression. Case-/
Decisiongraph bleibt gesonderter nächster T065-Schritt, keine Gesamtfreigabe behauptet.
