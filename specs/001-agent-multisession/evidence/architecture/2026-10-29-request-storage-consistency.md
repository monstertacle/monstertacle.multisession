# Requestkonsistenz vor Start-Recovery

Ausgang08a4cab, Messung2026-10-29T15:23:11Z. RecoveryTransactions SHA256
0edb436e7d3b0776280f97c2294356703d23fde7832ecf6574052c3cf091817c,
StoreSerializationbf571442de20180295187df168a4eb5f9e089c0bb2604f5e348a00406bf91c6e,
QueryTransactions5651b1a288d532ec33d22e2e336efdb49597006549e9a1bddfc0c6c26afa4f75.
T065/T070/T075. Preflight validiert Header/Schema/Metadata/integrity_check, Start zusätzlich
FK und zwei Running-/Reservationsjoins. Bereits vorhandene RequestStateMachine prüft
vollständigen State-/Certaintykatalog, RequestStateTransactions verwendet sie nur für Writes.
Bisher keine Prüfung aller vorhandenen Requestpaare, aller Snapshot-/Spaltenidentitäten
oder Result-/Zeitstempelprojektionen vor möglichem Recoverywrite. Queued/terminale Daten
werden dabei nicht zwangsläufig gelesen. JSONparser disallowsUnknownMembers, aber wird für
unberührte Zeilen erst später aufgerufen. Direkte Korruption gehört ausschließlich in
isolierte Tests; Produktionscode erhält keine Tamper-/Reflectionbackdoor.

Erster begrenzter Schnitt: streaming readonly Requestprüfung innerhalb derselben Start-
Transaktion VOR jeder Recoverymutation. Vorhandene StateMachine wiederverwenden; ID/Owner/
Sequence/State/Certainty sowie Accepted/Started/Ended/CancellationUtc zwischen Spalten und
RequestSnapshot abgleichen. Alle Zeiten UTC, keine zeitliche Größenordnung erzwingen:
Wallclock darf rückwärts springen, Fristen bleiben monoton. Terminal benötigt Result mit
gleichem Outcome/Certainty und EndedUtc; Queued/Running darf keinen finalen Result tragen.
Queued hat keinen StartedUtc/EndedUtc, Running braucht StartedUtc aber keinen EndedUtc.
Malformed/fehlende JSONinhalte oder widersprüchliche Projektion sind StorageUnavailable,
kein Ersatzstore und kein stilles Reparieren. Bounded Reader statt Loadall-Liste;
Snapshotinhalte nicht in Exceptions aufnehmen. Test-DBhash nach fehlgeschlagenem Start
unverändert, frühere bestätigte Ergebnisse bleiben lesbar nach isolierter Reparatur.

TDD zuerst: gültiges Schema mit ungültigen gespeicherten Paaren, malformed JSON, fremden
SnapshotIDs, State-/Sequence-/Zeitstempeldrift und fehlendem/abweichendem Result gegen
Bestand rot. Noch gültige Running-/Unknown-/Confirmed-/Queued-/Cancelledzustände in
bestehender Crash-/Recovery-/Lifecycle-/Retentionmatrix unverändert. Weiterer Session-/
Result-/History-/Recoverygraphabgleich bleibt nach separater Quellenprüfung in T065 offen;
dieser Schnitt behauptet keine vollständige beschädigungs- oder manipulationssichere DB.
