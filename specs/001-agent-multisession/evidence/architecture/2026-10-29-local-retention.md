# Explizite lokale Retention und sichere Löschung

Gemessen 2026-10-29T13:39:30Z, Ausgang6b0ed03; vor Entwurf/Code.
SessionCatalog.cs SHA25600d45696d217d281f72036c348c8ccb66fbe6abb0f6b9a80c9f6de3404591858;
SessionCloseProjection.cs b8d7ae223b83db450a5c99ae4e3d74c3da8f4bea544c3c24b212f36d7809ad42;
SchemaV1.sql c3191afadd3bb63bdaa3c20fd443cbb16814b16c8e08e9738a5e3fe5bef8cbfb;
Contracts/RetentionPolicy.cs b01e1bfc7e548c571d481a531657acc010d61278ac09478b6671c5d210073415.

## Bestand und Quellen

T054/T064, FR-021, US5.6; spec.md345–349 und534–537: Integrator löst Löschung aus,
während der Aufbewahrung bleiben Inhalte verfügbar, keine Garantie für externe Daten/
Backups. RuntimeOptions verlangt explizit Frist oder RetainUntilExplicitDeletion; T118
verhindert nun auseinanderlaufende Factory-/Runtimepolicy. DeleteSessionAsync ist ein
Catalogstub, kein Retentionport/SQLcode vorhanden. SessionCloseProjection liefert aktive
und ungewisse Arbeit einschließlich Unknown ohne Reservation; wiederverwenden. ClosedUtc
kommt aus gemeinsamer Closureregel. Schema hat keine Cascades: Decisions→Cases→Reservation/
Request und History/Results→Request→Session; Metadata bleibt unabhängig. Falsche Reihenfolge
oder fehlender Schutz darf nicht teilweise löschen. Keine Providerlöschmethode existiert.

## Entscheidung vor Umsetzung

Kleine ISessionRetentionStore-Facette für autoritativen atomaren Delete. Catalog delegiert
über SessionDeletion mit endlichem Caller-/10-s-Operationsbudget, ohne Provider-I/O.
Gemeinsame reine SessionRetentionPolicy (eindeutiger Name statt Konflikt mit dem bestehenden
Contracts.RetentionPolicy) prüft Closed, ClosedUtc, keine aktive/ungewisse Arbeit und die
explizite Hostregel. RetainUntilExplicitDeletion erlaubt ausdrückliche Löschung sofort;
positive endliche Frist erlaubt sie erst inklusive ClosedUtc+Duration. Keine automatische
Hintergrundlöschung; Host initiiert weiterhin Delete. Subtraktion zweier Zeitpunkte statt
Addition potentiell großer Duration verhindert Overflow. UTCinstant, keine Kulturstrings.

SQLite lädt Session, prüft dieselbe Projection/Policy mit seinen bestätigten Factoryoptionen
und löscht den lokalen Teilgraph in einer Transaktion: Decisions der Sessionfälle, Cases,
History, Results, Reservations, Requests, Session. Parametrisiert, andere Sessions/Metadata
unverändert. Aktive/ungeklärte Arbeit und nicht abgelaufene Retention SessionBlocked,
unbekannte ID NotFound. Fehler oder Callerabbruch vor Commit rollt alles zurück. Keine
externe/Backup-/physische Seitensäuberung zugesichert. Eine aufgelöste Fallhistorie wird
nur zusammen mit ihrer explizit gelöschten Session entfernt, keine verwaisten Entscheidungen.
Kontrollierte Projektion benötigt ausdrücklich gesetzte Runtimeoptions, keinen stillen
Produktdefault. T118-nativeCI vor vollständiger T064-Abnahme, unabhängige TDDarbeit zulässig.

Tests zuerst: reine inklusive Zeitgrenzen, ungültige/fehlende Policy, ClosedUtc und alle
Lifecycle-/Aktiv-/Unknown-Kombinationen; echte SQLlöschung kompletter historischer Graphen
mit entschiedenen Recoveryfällen und unabhängiger Session, Reopen, unbekannt/zweiter Delete,
kein neues Open/Send. Nicht Closed/Unknown, laufende Reservation und offene Fälle bleiben
unverändert. Frist7Tage verweigert frischen Close; ausdrücklicher Modus und bereits abgelaufene
kurze Frist erlauben. Fehltrigger nach Teildelete lässt alle Tabellen erhalten, Caller-Cancel
vor Commit ohne Änderung. Zuerst ausführbare Stubs und tatsächliches Red, dann minimaler
Code, gesamte Persistenz-/Cancel-/Close-/Crashregression, Full-Guard/Coverage und nativeCI.
