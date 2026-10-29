# Providerfehlerisolation und öffentliche Kategorien

Ausgang903338d, Messung2026-10-29T15:12:15Z. RequestDispatcher SHA256
8664be9ca741d67b6ea3256b016a5cfd3c876316a6985ae8ea2813bc6f36c669,
DispatchReplyPolicy e40593d85f113c9769e0c8c2d06b05cf0380e82d33340599dc85942ad2c285b4,
ProviderReplyPolicy96ab0748603a0c8e04f2a13f4188f6d71b4347fa921a8029414c30796524600c.
T073/T074/T080. Dispatcher behandelt jeden nicht-cancel Exception bisher als ExecutionFailed;
typisierte Auth-/Rate-/Kontextfehler verlieren dabei ihre Kategorie. Normale Replies dürfen
bisher beliebige ErrorCodewerte, fremde lokale Kennungen, abweichende Error.Certainty und
AdmissionStatus tragen. PublicError hat keine Freitextnachricht; neue Kategorieprüfung
begrenzt außerdem Herkunft/Metadaten. SQLiteCompletion und kontrollierte Completion nutzen
bereits dieselbe DispatchReplyPolicy. Observation-Speicherfehler werden separat erkannt;
deren lokale StorageUnavailable-Kategorie darf nicht durch Providerfilter verschwinden.

Entscheidung vor Erweiterung: ProviderFailurePolicy erlaubt ausschließlich InvalidConfiguration,
InvalidInput, InputLimitExceeded, OutputLimitExceeded, AuthenticationFailed,
ProviderRateLimited, ProviderUnavailable, ProviderSessionExpired, ProviderContextLimit,
UnsupportedProviderAction, ExecutionFailed, Cancelled, TimedOut, Interrupted.
Andere/undefinierte Codes werden ExecutionFailed, insbesondere keine lokalen Queue-/Store-/
Ownership-/Admission-/CASkategorien aus Providerantworten. PublicError von Replies wird aus
Code und Reply.Certainty neu aufgebaut, ohne fremde IDs/AdmissionStatus. Bindung/Outcome/
Ausgabe werden weiterhin nur durch vorhandene Regeln geprüft, Gewissheit nie aus Errorobjekt.
Typisierte Providerexception liefert nur erlaubte Kategorie, niemals sichere Endbestätigung:
Unknown bleibt belegt und RequestFailurePolicy ordnet lokales Failed/Cancelled/TimedOut zu.
Rohe Exceptions bleiben ExecutionFailed. Bestätigte Sinkfehler behalten lokale Herkunft.

TDD: neue Matrix für alle Enumwerte plus undefinierte Codes zuerst gegen Bestand rot;
Metadaten-/Gewissheitsspoofing und typisierte Exceptionkategorien rot. Fünf unabhängige
bestätigte Fehler/fünf Erfolge sowie alle belegten Unknownslots als ehrliche sofort grüne
Regression erwarten, kein künstlicher Fehler. Kein Retry/Herstellerwechsel/neue Konfiguration.
Alle Unit-/Vertrags-/SQL-/native Providerregressionen, Coverage und Codemetr nach Green;
Dispatcher derzeit Kopplung39, höchstens eine neue Policyabhängigkeit (Grenze40 unverändert).
