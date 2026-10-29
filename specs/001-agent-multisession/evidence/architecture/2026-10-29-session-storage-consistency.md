# Sitzungsprojektionen vor Start-Recovery

Ausgangea8ecd5, Messung2026-10-29T15:33:21Z. SessionTransactions SHA256
ccf820dbc8e399d1a0224fce59a0568df22d3f0fa9c11fdb43b66c1b35a34fad,
RecoveryTransactions9002a372c9a803546f11d7f295aa3fbc54803ff1e3008af4e193607ff298a41c,
RequestStoreConsistencyaa1b92b6e9700c7fb3571872b04c05b5c46146060f82201060668dd3d634e073,
SessionSnapshot6719a5a13d77049b1e8af4bc27b818627386ac42910dd942fc45c8d369fe591b.
T065. Requests werden jetzt vor Recoverywrite gestreamt geprüft. Sessions haben weiterhin
SQL-Projektionen für Identität, Provider/Profile/Model/Settingsrevision, Bindungsadapter/-
Version, Created/Updated/ClosedUtc, Lifecycle/BlockReason/BindingRevision und NextSequence.
QueryTransactions liest überwiegend nur JSON. Recovery liest bisher nur Openingzeilen;
ungeprüfte Open-/Closing-/Closedprojektionen könnten später andere Daten als SQL liefern.

Vor Erweiterung entschieden: eigene streaming SessionStoreConsistency im selben Start-
Transaction vor jeder Mutation. Snapshot-/Spaltenfelder ordinal/exakt abgleichen, Context
muss lokale SessionId tragen, ExecutionKind/Lifecycle/BlockReason müssen definiert sein.
Bindung/Adapter/Version konsistent, positive Formatversion ohne Festlegung auf einen
Hersteller oder Format1. Open braucht eine Bindung, native SessionReference darf bei
lazy Root weiterhin null sein. ClosedUtc genau bei LifecycleClosed, alle Zeitpunkte UTC;
keine monotone Wallclockannahme. Bindungsrevision bleibt nichtnegativ; NextSequence muss
MAX(Request.Sequence)+1 oder1 bei leerer Sitzung entsprechen. Keine stillen Korrekturen,
kein Bindungs-/Profilersatz. Keine zusätzliche Regel ClosedBlockReasonNone ohne vollständigen
Writer-/Raceaudit: diese Frage bleibt T079/T083 zugeordnet. Gemeinsamen vorhandenen
UTCtextvergleich gezielt in StoredUtcRepresentation extrahieren, keine allgemeine Utility.

TDD zuerst: Provider/Profile/Context-/Binding-/Zustands-/UTCdrift, malformed/missing JSON,
Open ohne Bindung und falsches NextSequence gegen Bestand tatsächlich rot; abgelehnter
Start darf DBbytes nicht ändern. Vorhandene Opening-/Closing-/Closed-/LazyRoot-/Crash-
Wiederöffnungen bleiben grün. Noch kein vollständiger Result-/History-/Casegraphschutz;
folgende T065-Schnitte müssen deren legitime Unterschiede bei ConfirmEnded berücksichtigen.
