# Echte Journalbarriere im Kindprozess

Gemessen 2026-10-29T10:24:46Z; Ausgangsformatvertikale1df4b05.
T048 sowie zusätzliche Crashregression zu T049/T056. Geprüft:
ValidationHost/Program.cs SHA2563d85a98fb1e750204dca7bf967ff4ccfb726e60d6f52fddf2de3684d93cf317f;
ValidationHost.csproj b948f08c0985a16c68472bc6224201f303a8f1e0ffae3eb5da98be79ae8d112f;
SqliteStorePreflight.cs c3e7f5e3a5343d1e863c808b445166d43eba0cf132d81d5ddaf5fa36a3564736;
PrivateStoreDirectory.cs ae66b7db8260faa4cd057b63ac253e505532415af2da875954292fa52aaf28fc.

## Bestand, Erweiterungspunkt und Risiko

Program ist leeres Hostgerüst; Projekt referenziert sechs Bibliotheken abhängig vom TFM.
Kein Kindprozess-/IPCgerüst vorhanden. IntegrationTests nutzt ausschließlich eigene private
Verzeichnisse und tatsächliche SQLite-Dateien. Factory prüft Journal auf einer privaten
DB-/Journalkopie und öffnet Original erst nach erfolgreicher Prüfung. Bisherige28 Fälle
haben lediglich kalte synthetische Sidecarbytes, keinen echten offenen SQLitejournalzustand.
Dadurch wäre eine reale Rollbackfähigkeit bisher unbelegt, obwohl der Kopiepfad grün ist.

Infrastrukturentscheidung: dedizierter interner Hostmodus --crash-worker mit absolutem
Testroot, punktgenauer Barriere und zufälligem Nichtsecret-Probetoken. Parent legt Token
in eigener privater Unterablage ab. Child verlangt passenden Marker, bekannten Punkt und
bestehende DB und öffnet nie Create-if-missing. Kein Live-/Modellaufruf, kein normaler
Hoststart oder allgemeine Szenarioimplementierung. Generelles Szenariorouting T089/T094
bleibt offen. Keine Produktionsablage erzeugen oder außerhalb des Testroots schreiben.

Child beginnt echte SQLite-Transaktion mit kleinem Pagecache, erzeugt uncommittete
Testtabelle/großen Blob bis Pagespill, signalisiert ausschließlich Phase/PID/UTC über
stdout und bleibt asynchron an Barriere. Parent prüft tatsächliche Hotjournalmagic und
beendet den wirklichen Prozessbaum hart; kein Dispose als angeblicher Crash. Endliche
Start-/Exitlimits, Fehlerpfad beendet ebenfalls Child. Ein gemeinsam gelinktes reines
CrashProtocol hält IPCkonstanten/Envelope für Test und Worker konsistent; kein Produktcode
oder Fremdrepo kopiert. IntegrationTests referenziert Host nur als Buildabhängigkeit,
nicht dessen Assembly. Harness findet bereits gebauten DLLpfad im Repo und startet dotnet.

Prüfplan: gültiges Format nach hartem Ende wirklich zurückrollen, StoreId erhalten,
uncommittete Tabelle verschwindet, temporäre Kopien entfernt. Mit schon vor Crash
unbekannter Metadataversion bleiben originale DB und Journal nach Ablehnung bytegleich.
Dies ist ergänzende Regression einer bereits implementierten Formatfähigkeit; wenn sie
unmittelbar grün wird, kein künstliches TDD-Red behaupten. Scheitert sie fachlich, bleibt
dieser echte Fehler vor einer Korrektur samt Digest bestehen. Keine SC-009-/NativeLease-
oder Zehnfach-Abnahme aus zwei Journalfällen ableiten. Danach vollständige Qualität/Guard.

## Portable Markerrechte vor Korrektur

Gemessen2026-10-29T10:27:40Z. Zwei echte Crashfälle bereits grün. Zusätzliche Prüfung
auf explizite0600-Markerrechte war unter gemessener lokaler umask0077 ebenfalls grün,
unter üblicher umask022 aber zweimal fachlich rot: File.WriteAllTextAsync erzeugt0644.
Die private Testablage verbirgt zwar den Marker, der Fixturevertrag soll dennoch nicht
vom aufrufenden Umask abhängen. CrashHarness.StartAsync erhält deshalb die vorhandene
Lease statt nur den Pfad und verwendet deren bereits getestete PrepareFileAsync-
CreateNewoperation vor dem Schreiben. Kein globaler Umaskwechsel im Produkt oder Harness;
die umask022 wird ausschließlich im Testprozessstarter gesetzt. Anschließend gleiche
Probe unter022 und komplette Regression/Guard. Keine Änderung an produktiver Vorprüfung.

## Windows-CI: Dateifreigabe nach hartem Ende

Gemessen2026-10-29T11:35:20Z. CrashHarness.cs SHA256
fdef88d68460cdbd8de146204b362ecd3a66d6e9c3d1414763f97462b548e73c.
CI quality40863911503 an427691b, Windows, HandlesActualHotJournalAfterForcedProcessEnd(false)
ist fachlich rot: File.ReadAllBytesAsync unmittelbar nach Kill/WaitForExitAsync meldet
IOException „being used by another process“ an Journaldatei. Rohlog lokal unter
artifacts/tdd/sqlite-pages-ci-failure.log. Welcher Prozess das verbleibende Handle hält,
ist nicht gemessen; kein spekulativer Produktbefund daraus. Ubuntu/Guard sind getrennt.

Harness wartete bisher ausschließlich auf Prozessende, nicht auf tatsächlich wieder
lesbare Crashdateien. Vor der anschließenden Snapshotprüfung wird jetzt zusätzlich
endliches Lesen von DB/Journal mit FileShare.Read verlangt. Ausschließlich Windows-
Sharing-/Lockverletzung wird kurz gepollt; andere I/Ofehler bleiben Fehler. Gemeinsames
30-s-Limit und asynchrones Delay, keine feste Pause als Freigabebehauptung. Kein Retry
von Provider-, Admission- oder Produkt-SQLoperation, keine schwächere Abnahme. Der echte
CI-Redbeleg liegt vor dieser Korrektur vor. Lokal können nur normale Crashregressionen
laufen; native Windows-Green muss neue CI bestätigen und bleibt bis dahin offen.
