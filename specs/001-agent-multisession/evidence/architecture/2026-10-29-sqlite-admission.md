# Dauerhafte Annahme und begrenzte DBarbeit vor Green

Gemessen2026-10-29T10:36:11Z. Ausgang457e901. T050/T054/T057/T058 sowie
vorbereitende Serialisierungsfälle T067. Aktueller Deep-Dive:
SqliteSessionStore.cs SHA2567111faa5ae87f714a5beb267732fd35f831a9872024dab762e151ab78f94cfce;
SqliteQueryStore.cs c3ed68298fcdb06e173c5d1b195c45bc7e404e4dc0fd7e2f75f1e50b75c2dde8;
SqliteStoreFactory.cs60342ab265c371c26c441c2d73a637e1ff575fc6397149ebf16511ecfc8d51c5;
RequestAdmission.cs deee93746f1f6905263267731730c336a4940ddf07c6854bfb4955ce87f4faed;
RequestSubmissionRules.cs8dd7090b61c420699d87f567b7a3bace243af119adc09bcaf22961d97fad22f3;
ProviderSessionContext.cs ca2862deacc7c312a5bc5a8741d007047ecf295fbcdd327d4dd760ec36bcd64c;
RequestSnapshot.cs4112fd437027ec85d9116d75515324f438736cd2d13ef173ee134e2f762fdc80.

## Bestand und Kontrollfluss

Factory initialisiert/prüft echten SQLitebestand, hält eine Verbindung ohne Pooling und
gibt ISessionStore zurück. Domain-/Bootstrapports sind noch NotImplemented-Gerüste.
Querybasis disposet Verbindung über eigenen Task. Kein DBworker, keine fachliche
SQLannahme, keine persistierten Sitzungen/Requests vorhanden. Schema enthält dafür
Scalarordnungsfelder, JSONsnapshots, NextSequence und eindeutige SessionId/Sequence.
Private Dateivorbereitung und echter Hotjournal-Killpfad sind grün, NativeLease offen.

Common SessionCatalog speichert Opening vor Open, bestätigt Binding und gibt den
committeten Snapshot zurück. RequestAdmission validiert über RequestSubmissionRules,
vergibt Kandidaten-ID, wartet mit monotoner endlicher Frist und unterscheidet sichere
Nichtannahme von Unknown; bekannt erfolgreicher Commit gewinnt späte Cancellation.
ProviderSessionContext friert Settings ordinal ein. Pagingcursor sind bereichs-/versions-
gebunden. Reale DBqueries/JSONroundtrips fehlen noch. Bloße DTOrecord-Gleichheit von
Dictionaryreferenzen ist kein persistenter Wertevergleich; Tests vergleichen Inhalte.

## Gewählter Schnitt

Ein eigener bounded Channel mit genau einem Task führt alle synchronen SQLiteobjekte
sequenziell aus. TryWrite statt unbegrenzter Aufruferwarteschlange. Queuevoll/Disposal/
Cancel vor Beginn wird NotAccepted; Cancellationregistrierung kann wartenden Job
sofort sicher beenden, selbst wenn früherer SQLjob noch Busy wartet. Sobald Job aktiv
ist, entscheidet Transaktionscode über Rollback/Commit; keine Taskabtrennung als Ende.
Dispose stoppt neue Arbeit, verwirft wartende Jobs, wartet aktive und schließt Verbindung.
Lease wird nicht vom Store freigegeben. Pinned SQLite Cancel() bleibt No-op; DefaultTimeout
1s begrenzt Vendorbusyretry, keine angebliche native Sofortcancelgarantie.

Storezustand hält Connectionqueue, ursprüngliche Lease, immutable Optionen und Started/
Accepting nur im Worker. Start verlangt dieselbe gehaltene Lease und darf nur einmal
scharfstellen. StopAcceptance sperrt neue Sitzungs-/Requestannahme. Bereits vorhandene
Opening/Running bleiben bis T061 konservativ nicht startbar, niemals still wiederholen.
Query vor Bootstrap darf lesen; fachliche Writes benötigen gestarteten Store.

SessionTransactions erzeugt Opening mit vollständigem immutable Snapshot, bestätigt
Binding atomar mit UpdatedUtc/Revision. QueryTransactions liest Session/Request und
später getrennte Pagequeries. StoreSerialization nutzt ausschließlich typisierte BCL-
JSONdaten, keine Typauflösung; UTCscalarzeitpunkte explizit invariant formatieren.
RequestSnapshot erhält AcceptedUtc sowie nullable Start-/End-/Cancelzeitfelder, bestehender
Positionskonstruktor bleibt kompatibel. Output-/Dispatchpfade bleiben ihre eigenen Tasks.

AcceptanceTransactions prüft innerhalb derselben Transaktion Session/Fachregeln,
Kandidaten-ID, globales Queuelimit und NextSequence; schreibt Eingabe/Snapshot/AcceptedUtc
und erhöht NextSequence genau einmal. Common RequestSubmissionRules wird als benannte
öffentliche reine Regel für Storage wiederverwendbar, statt Regeln zu kopieren.
SQL immer parametrisiert. Kein Provider-I/O, keine automatische Deduplizierung.
Receipt ausschließlich nach Commit. Exception vor Commit nur nach belegtem Rollback
NotAccepted; während Commitversuch konservativ Unknown, erfolgreiche Rückkehr gewinnt
nachträgliche Tokenänderung. PublicError enthält keine SQLiteexception/SQL/Inhalte.

Minimale erste Queries für Session-/Requestlookup; vollständige Paging-/Recovery-/History-
queries werden nach eigenen Redfällen integriert, keine leeren Ersatzlisten als Erfolg.
Keine automatische Providerregistrierung oder Mutation gefrorener Modelldaten.
T061/T062/T064/T070+ bleiben offen. Ganze Story-/Crashabnahme nicht vorziehen.

## Tests vor Implementation

Öffentliche Factory/ISessionStore/Commonfacaden, echter SQLitebestand, eigenes Dateifixture:
Opening vor nativem Open auffindbar; bestätigter Snapshot/Settingsrevision erhalten;
Receipt/Originaleingabe und UTCzeitpunkte dauerhaft; gleiche Nachrichten bleiben getrennte
Annahmen; Wiederöffnung hält Kandidaten/Sequence und nächste Nummer; 20 konkurrierende
Commits bei Queuelimit3 ergeben genau3 Annahmen; ungültige/duplizierte Kandidaten und
Inputgrenzen ohne Teilwirkung; StopAcceptance und precancelled Commit ohne neue Daten.
Zusätzliche Culturefälle de-DE/en-US/tr-TR bewahren Unicode/opaque IDs/UTC und Settings.
Worker-/Busy-/Queuevoll-/Cancelproben T054 folgen vor deren jeweiligem Green. Falls eine
untergeordnete Eigenschaft bereits grün ist, als ergänzende Regression dokumentieren,
kein nachträglich fingiertes Red. Voller Guard/Qualität und echte CI je abgeschlossenem Schnitt.

## Nachprüfung vor Refactoring

Gemessen2026-10-29T10:51:42Z: alle50 Integrationfälle grün. Zwei ursprüngliche
Testannahmen waren unzutreffend: nichtleere Whitespaceeingabe ist nach bereits vorhandenem
Utf8MessageLimitsTests.EnforcesInclusiveInputBytes ausdrücklich erlaubt; die öffentliche
Exception enthält im Stacktrace den Namespace Storage.Sqlite, ohne eine rohe SQLite-
Exception zu transportieren. Erwartung auf Message/InnerException und Whitespaceannahme
korrigiert, keine Produktregel geändert. Zusätzlicher echter Redfall zeigte veraltetes
UpdatedUtc nach NextSequenceänderung; jetzt zusammen mit Request und Sequenz gespeichert.
Culturetests vergleichen deshalb den nach Annahme gültigen Timestamp mit AcceptedUtc.

Vor struktureller Bereinigung grüne50er-Suite: atomare Workstatus0/1/2 durch benannte
Waiting/Running/Finishedkonstanten ersetzen, Logik unverändert. Voller Guard/Regression folgt.

Implementierungsrelevante Konfigurationsnachprüfung: RuntimeOptions erlaubt positive
DBbusyzeiten allgemein, der erste native SQLitevertrag setzt ausdrücklich feste1s
(DefaultTimeout und PRAGMA1000). Abweichende Werte dürfen nicht still ignoriert werden.
Factory muss deshalb bisher nicht implementierte abweichende Busywerte ausdrücklich als
InvalidConfiguration vor Datei-I/O abweisen; generic RuntimeOptions bleibt für andere
Storeimplementierungen unverändert. Neue negative Factoryregression zuerst tatsächlich
rot, dann explizite native Einschränkung dokumentieren. Unterstützung weiterer nativer
Busyzeiten braucht eigene Deadline-/Vendorretrytests statt geratenen Subsekundenversprechen.

## Echte SQLfehler als zusätzliche Regression

Gemessen2026-10-29T10:57:05Z, zuvor vollständige Qualität grün mit500 Tests.
SqliteTransactions.cs SHA256bcdff74394f836f5d1c8171183b2ec92df273353b996a2968fb6da362964c261;
StorageFailurePolicy.cs339fb6a3c9211628cb4f3bbd16f448154ff4bb9e7d5bba939e90bd303e6fbaa8.
Run setzt committing erst unmittelbar vor Commit; vorheriger Fehler braucht erfolgreichen
expliziten Rollback für NotAccepted, Commitfehler bleibt Unknown. Policy entfernt rohe
Exceptions/Inhalte. Bisher prüfen InvalidInput/Queuefull Rollback vor Writes und Busy vor
Begin; wirkliche Teilwrites/Commitfehler sind noch nicht gesondert gemessen.
Testfixture darf Schema innerhalb eigener geöffneten Ablage gezielt ändern: AFTER UPDATE-
Trigger wirft erst nach Requestinsert; deferred ForeignKeyfehler wird erst bei COMMIT
sichtbar. Assertions: kein Receipt, Row/Sequenz rollback, sichere Nichtannahme nur beim
belegten Rollback; bei Commitversuch konservativ Unknown mit Kandidaten-ID über öffentliche
RequestAdmission, keine SQL/Markertexte in Exception. Keine neue Produktfunktion entwerfen;
falls unmittelbar grün, zusätzliche Regression dokumentieren statt Red zu behaupten.
Die Fixtures sind keine Fremd-/Produktdaten. Danach ganze Qualität/Guard am selben Stand.
