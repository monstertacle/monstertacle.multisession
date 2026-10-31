# SQLiteformat, private Dateien und einzelner Arbeitskanal

Gemessen 2026-10-29T10:11:16Z, Ausgang1ab4726. T049/T054/T056/T057,
Format-/Transaktionsregeln in data-model.md und Constitution II/IV/V.
Quellen: Storage.Sqlite.csproj SHA256bdbe9cb33d9a06575c3d48b620e105653686ef86f346e95e5a8a5b64cea60245;
IStateDirectoryLease.cs b764fae94b2497c6688bfd08cd42eb683efaa291d79c25c55f7e61330ecb538f;
ISessionStore.cs29c429211153c696405f305fc29bc5779678e987548b094734681eb083a0f681.

## Geprüfter Bestand

Storage.Sqlite enthält ausschließlich Projekt/Lockfile, keine C#- oder SQLimplementierung.
Microsoft.Data.Sqlite10.0.14 ist bereits gepinnt und restauriert, kein neues Paket nötig.
ISessionStore trennt Writes, Queries, Bootstrap und Disposal. SessionRuntime prüft gehaltene
passende Lease vor Store.Start; Cleanup schließt Store vor Lease. RequestAdmission kennt
NotAccepted/Unknown und wartet höchstens bis AdmissionTimeout, übernimmt rechtzeitig
bekannten Commit trotz später Cancel. RequestDispatcher bestätigt ReservedDispatch vor
Provider-I/O; keine SQLiteannahme durch Doubles belegt. RuntimeOptions enthält bereits
DBqueue256/Busy1s und strengere Shutdownfrist. Session/Reply/Recovery/Pagingverträge sind
vorhanden; die Metadaten aus1ab4726 werden unverändert übernommen.

Einzige Leaseimplementierung ist bislang kontrolliertes Testdouble. Der Port kann derzeit
keine neue DBdatei mit nativen privaten Rechten vorbereiten. Automatisches SQLitecreate
würde diese Verantwortung unzulässig in Storage verschieben oder Rechte vom Prozessumask
abhängig machen. Daher explizites PrepareFileAsync am Leaseport: nur einzelner relativer
Dateiname, CreateNew exklusiv ohne Überschreiben, OpenExisting prüft vorhandene reguläre
private Datei ohne Rechtekorrektur. Native Implementierungen folgen T091/T092. Bestehendes
Double verweigert Dateioperationen ausdrücklich; US5 verwendet eigenes isoliertes
Dateifixture mit privaten Dateien, ohne Besitzer-/Alias-/Killgarantie vorzutäuschen.

## Primärquellen und konkrete Konsequenzen

Microsoft dokumentiert synchrone ADO.NETasyncmethoden und fehlende Concurrent-Nutzbarkeit:
https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/async und
https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/database-errors.
Tatsächlich heruntergeladene gepinnte Quelle, NuGetrepositorycommit0427b9abbead3fa64c6655eb4290c537397131fa:
https://github.com/dotnet/dotnet/blob/0427b9abbead3fa64c6655eb4290c537397131fa/src/efcore/src/Microsoft.Data.Sqlite.Core/SqliteCommand.cs .
Cancel() ist dort No-op, CommandTimeout zählt Sekunden und Prepare-Busyretry schläft150ms.
Deshalb kein angeblich sofortiger nativer SQLabbruch: ein separater begrenzter Worker,
DefaultTimeout/CommandTimeout1s statt30s und Cancellation vor Arbeitsbeginn/Commit prüfen.
Eine bereits laufende SQLoperation wird bis zu ihrem wirklichen Ausgang nachverfolgt.

SQLiteheader/Hotjournal/URI:
https://www.sqlite.org/fileformat.html , https://www.sqlite.org/lockingv3.html ,
https://www.sqlite.org/uri.html und https://www.sqlite.org/pragma.html .
Ein Hotjournal ist Teil des DBzustands. ReadOnly allein kann notwendiges Rollback nicht
leisten; immutable=1 würde Journal ignorieren und ist kein gültiger Crashsnapshot.

## Entscheidung vor Code

Factorymethoden CreateNewAsync/OpenExistingAsync verlangen gehaltene Lease, expliziten
nichtleeren Vertrauensbereich und validierte RuntimeOptions mit passendem Verzeichnis.
Kein Create-if-missing: Lease bereitet Datei vor, SQLite öffnet ausschließlich ReadWrite
oder ReadOnly, niemals ReadWriteCreate. Pooling=false bindet Ressourcen an Storelebensdauer.
Factoryfehler lässt Lease beim Host; Storedisposal schließt SQL vor hostseitigem Leaseende.

Initialisierung ausschließlich neuer Datei: versioniertes eingebettetes SchemaV1.sql,
Metadata mit StoreId/Trustscope/Schema-/Migrations-/Bindingencodingversion/UTCzeit,
application_id/user_version, alle DDL/Metadaten in einer Transaktion. Snapshotjson bleibt
reines Datenformat, keine ausführbaren Typnamen. Tabellen und Constraints entsprechen
Metadata/Sessions/Requests/Results/History/Reservations/RecoveryCases/RecoveryDecisions;
aktive Session/Workspacebelegung über partielle Uniqueindizes. Keine Migrationsengine.

Vor schreibendem Öffnen bestehender DB: Header identifizieren, dann reine ReadOnlyprüfung
von Metadaten, tatsächlichem sqlite_schema und Integrität. Erwartetes Schema aus derselben
eingebetteten SQLquelle in Memory erzeugen/vergleichen, keine doppelte Schemabeschreibung.
Falls ein Journal existiert: DB+Journal zuerst in eigene über die Lease privat vorbereitete
Sondendateien kopieren, ausschließlich die Kopie durch SQLite physisch zurückrollen lassen
und vollständig prüfen. Erst nach erfolgreicher Format-/Trustprüfung Original ReadWrite
öffnen. Bei Ablehnung bleiben Original und Originaljournal bytegleich. Sonden im finally
entfernen; ein Prozesscrash kann private Sonden hinterlassen, keine automatische Übernahme
als Ersatzstore. Überreste vor Freigabe in T093/T108 prüfen, nicht Fremddateien löschen.
Kosten: nur Journalpfad kopiert DB; benötigter freier Speicher und Abbruchfehler sichtbar.
Kein selbst implementiertes Journalformat oder immutable-Crashfallback.

DBworker serialisiert alle SQLobjekte über bounded Channel. Überfüllung oder Cancel vor
Beginn ist sichere Nichtannahme; während einer tatsächlichen Transaktion entscheidet
nur deren Commitergebnis. Dispose stoppt weitere Aufnahme, überspringt verbliebene Arbeit,
wartet aktive Arbeit und schließt erst dann Verbindung. Kein Provider-I/O im Worker.
DELETE/synchronousEXTRA/foreign_keys/busy_timeout1000 explizit setzen und Werte prüfen.
Schema-/Clock-/Queueklassen getrennt von fachlichen Transaktionen. Factory gibt
ISessionStore zurück; erste Formatvertikale darf unimplementierte fachliche Storeports
noch nicht als Runtime-/Persistenzprodukt ausgeben. T058–T066 bleiben offen.

## Tests vor Green

StoreFormatTests über öffentliche Factory und eigenes echtes SQLitefixture: ausdrückliche
Initialisierung, persistente eindeutige Metadaten, bekannte Wiederöffnung, kein Ersetzen/
Überschreiben, ungültige Lease/Scope/Optionen/Cancel, fremde/ältere/neue/korrupt/abweichende
Schemata und Bindingencoding unverändert abweisen, veränderte Metadaten/Schema erkennen,
Store ohne fachliche Runtimebehauptung disposen, private Dateien und gespeicherte PRAGMAs
beobachten. Hotjournal-/Killprobe folgt T048/T051/T055 am echten Prozess; kein Fakeersatz.
Queue-/Busy-/Admissionbeweise folgen vor ihren Implementierungsschritten T050/T054/T057.
SQLressource in Qualitäts-Sourcehash aufnehmen; bisher zählt das Skript keine .sql-Dateien.
Kein Gate-/Scopeabbau, keine neue Abhängigkeit. Voller Guard/Format/Build/Coverage/CI je Schnitt.
