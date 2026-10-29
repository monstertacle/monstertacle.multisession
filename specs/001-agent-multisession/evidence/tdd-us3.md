# US3: laufender Annahme-/Lebenszyklusnachweis

## T114 und lokale Admissionfrist (2026-10-29)

Owner: ausführende Implementierungssitzung, Branch `feature/reserved-dispatch-contract`,
Basis `aa15e97`. Vorabanalyse: `architecture/2026-10-29-admission-certainty.md`.
T114 lokal abgeschlossen. Die Admissionteile der Fristaufgabe T076 sind vorgezogen;
keine SQLite-/vollständige Cancel-/Stopabnahme und keine abgeschlossene US3.

Testbefehl über run-capped:
`dotnet test tests/Monstertacle.Multisession.ContractTests/Monstertacle.Multisession.ContractTests.csproj -c Release --no-restore --filter FullyQualifiedName~AdmissionOutcomeContractTests --logger trx --results-directory artifacts/tdd/<Ordner>`.
`admission-certainty-red`: sieben tatsächlich ausgeführte fachliche Fehler; lokale
Ablehnung ohne AdmissionStatus, rohe Cancellation, leere Kandidatenkennung, roher Storefehler,
kein Admissiontimer und unbegrenztes Warten auf nicht kooperativen Commit (5-s-Testwächter).
`admission-certainty-green`: dieselben sieben Fälle bestanden. Anschließend zusätzlicher
bereits grüner Kollisionsfall (kein neuer Red behauptet); gesamte alte Unit-Suite 225/225 grün.

SHA256 der vorab erfassten sources.sha256-Manifeste Red/Green:
`da1e61c31571d60e9ca9b81fe10dd5b1257af4390c9f745e80d3df3091d206f7`,
`5a0518044530f0d591217e41c86a027cd5f94d1cdfd5a59fdf2db79fa55e5e9e`.
Originale TRX/Manifeste lokal und ignoriert, keine rekonstruierte historische Ausführung.

RequestAdmission erzeugt genau eine Kandidaten-ID vor dem Storecommit und übergibt
RequestAcceptance mit unverändertem SubmitRequest. Der Store übernimmt diese ID; Sequence
und Queueplatz bleiben Storeautorität. Leere/kollidierende Kandidaten werden im Double
abgewiesen, nie mit bestehenden Inhalten zusammengeführt. Der Legacy-Submit-Overload ist
nur Testsetup, nicht der produktive ISessionStore-Vertrag.

AdmissionStatus trennt NotAccepted/Committed/Unknown von ExecutionCertainty. Ein actual
Receipt bedeutet bekannten Commit und gewinnt gegen verspäteten Caller-Cancel. Vor dem
Commit bekannte Ablehnung ist NotAccepted; nach Commitbeginn nur bei ausdrücklichem
Rollbackbeleg, sonst Unknown mit Kandidatenkennung und unbekannter externer Gewissheit.
Caller-Cancel erhält die OperationCanceledException-Unterklasse AdmissionCancellationException.
Fehlertext ist bereinigt; kein Rohfehler als InnerException. Ein erfolgreicher Receipt mit
falscher Kennung/Sequence wird nicht als Annahme dieses Kandidaten ausgegeben.

AdmissionTimeout beginnt beim Submit-Eintritt und begrenzt auch einen unkooperativen
Storewait. Ein kontrollierter späterer Commit nach Cancel/Deadline bleibt per derselben
Kandidaten-ID auffindbar. Vorläufiger Lookup ohne Datensatz beweist keine Nichtannahme.
Der Test zählt genau einen Commitversuch; kein automatischer Resubmit/Receipt-Push.
RequestSubmissionRules hält die bisherigen Fachregeln getrennt vom Wait-/Fehlerablauf;
keine duplizierte Eingabe-/Timeout-/Workspacepolicy. Die externe Ausführungsfrist und
Queuewartezeit wurden dadurch nicht zusammengelegt.

Vollständiger nativer Linuxrunner über run-capped: Exit 0, Locked-Restore/Release-Build/
Format/Analyzer/direkter und transitiver Audit bestanden, 0 Buildwarnungen/-fehler.
Maschinell aus TRX: 271 Tests bestanden (225 Unit, 44 Contract, je 1 Integration/Linux-
Infrastruktur), 0 fehlgeschlagen/übersprungen; zusätzlich 13 Coverage-Gateverhaltensfälle.
Gesamtprodukt-Coverage 538/559 = 96,2433 %, Mindestwert 87 % erfüllt.
Sourcehash `91858291ae386c6338bc0aec5af1186324d4bf81315c669875d5d5df544c8b9e`;
Artefakte `artifacts/quality/linux/67c4e2ada79541f78d3dad9edc66272f/`.
Full-Codemetr 0.1.0: 115 Dateien/117 Typen, 0 Verstöße/Parsefehler/übersprungene Typen,
Exit 0. Keine Pins/Gates/Scope/Baseline verändert. Debian 13/PowerShell 7.6.9/SDK 10.0.403;
kein neuer Windows-/SQLite-/native Zielsystem-/Live-/Paketnachweis dieses Abschnitts.

Befundabgleich: fehlende Annahmegewissheit, Kandidat und begrenzter Wait durch die sieben
Red–Green-Fälle abgedeckt; Kollisionsregression schützt unveränderte Inhalte. Reale atomare
Annahme mit Crash-/Wiederanlaufgrenzen bleibt T050/T058 (Owner US5, persistierte Kandidaten-
und Sequenzabnahme). Vollständige Cancellation-/Stop-/Fristpersistenz bleibt T070–T082
(Owner laufende Gesamtimplementierung, native Zustands-/Zeit-/Stoptests). Kein offener
lokaler T114-Abnahmeblocker. Folgearbeit läuft weiter, kein Ende des Gesamtauftrags.

## T070/T075: gültige Kombinationen und einmalige lokale Finalisierung

Architektur vor Umsetzung: architecture/2026-10-29-request-state.md, Ausgang cc1546a.
Ausführbare verhaltenslose RequestStateMachine: request-state-red-final, 10 Tests,
9 fachlich fehlgeschlagen (gültige Kombination/Terminalität/6 Übergänge und tatsächlich
akzeptiertes Interrupted/NotStarted), einer bereits grün; vorausgehender Analyzerfehler
wegen fehlender Schleifenklammern ist kein Red. SQL request-state-sql-red: 1/1 fachlich rot,
CommitCompletion warf keinen Fehler und nahm den unmöglichen Endzustand an.

Green: dieselben 10 Unitfälle und SQL-Abweisung, bestehende Completion/Recoverytests.
Zentrale reine Regel statt unabhängiger Outcome-/Gewissheitsprüfung; SQL prüft Übergang
vor Result/Historyschreiben und vor Recovery-Gewissheitsänderung. Kein nachträglicher Erfolg.
Zwei zusätzliche konkurrierende Known/Unknown-Finalisierungen mit beiden Einreichreihenfolgen
waren sofort grüne Regressionen, kein neuer Rednachweis. Genau ein Result/History und
passende Reservationsbelegung bleiben nach Wiederöffnung erhalten.

Vollständig grün: artifacts/quality/linux/b9adac26d0fa45a5875878f4aa400dcc,
SourceHash b071b32651753302fbda299c18d4021ac2cdafc9164e29236b6dd2e96c868db1.
590 Tests (271 Unit, 195 Contract, 123 Integration, 1 Plattform), null Fehler/Skips,
2001/2071 Produktzeilen = 96,6200 %. Codemetr 0.1.0: 267 Dateien/273 Typen,
null Verstöße/Parsefehler/Skips. Befehle: request-state-red-final.log,
request-state-sql-red.log, request-state-green.log und request-state-quality.log unter
artifacts/tdd; vollständiger Format/Build/Test/Coverage-/Guardlauf über run-capped.sh.
T070/T075 bleiben für ihre vollständigen Cancel-/Intent-/Finalisierungszusagen offen;
T071/T077/T078/T079 tragen die nächsten konkreten Schritte mit unveränderter Abnahme.

## T070/T077: öffentlicher wartender Cancel und dauerhafte Absicht

Architektur vor Erweiterung: architecture/2026-10-29-request-cancellation.md, Ausgang b2e6435.
request-cancellation-red: drei ausführbare Tests fachlich rot an den verhaltenslosen
Ports (Queued bleibt ohne Abbruch, Running ohne Intent, fehlende ID ohne definierten Fehler).
Minimales Green: verpflichtende IRequestCancellationStore-Facette, öffentliche Delegation,
reine gemeinsame Intentregel und atomarer SQLpfad. Queued ist Cancelled/NotStarted samt
Result/History und unverändertem Input; Running bleibt InProgress reserviert mit erstem
Intentzeitpunkt. Terminale Aufrufe ändern weder Ergebnis noch Gewissheit. Bekannter Abschluss
bleibt auch nach Intent commitfähig. Keine Herstelleraufrufe innerhalb einer SQLtransaktion.

Zusätzlicher echter Redzyklus cancel-observation-red: OperationAccepted nach Intent wurde
mit RevisionConflict abgewiesen. Nach erneutem Source-/Architekturabgleich Observationlesen
auch bei Intent erlaubt, während Running/Dispatchbindung weiter schützt. Green sichert native
Operations-ID im Unknown-Fall, nie Reservationsfreigabe allein durch Absicht.
Weitere sofort grüne Regressionen: Teilwritefehler am Intent rollt Result/History komplett
zurück; beide Cancel-/Reserve-Reihenfolgen bewahren atomaren Zustand, keine Sendeerlaubnis
für abgebrochene Queued. Drei initiale Canceltests plus vier Ergänzungsfälle sind integriert.

Vollständig grün: artifacts/quality/linux/6568798978784b7db0e62ecc19f147d5,
SourceHash 297e9c0769a414ab37ca92d23f890a13e843367fd12a135df2c907847b318250.
597 Tests (271 Unit,195 Contract,130 Integration,1 Plattform), null Fehler/Skips.
2027/2096 Produktzeilen = 96,7080 %, Codemetr 0.1.0: 272 Dateien/278 Typen,
null Verstöße/Parsefehler/Skips. Logs artifacts/tdd/request-cancellation-{red,green,quality}.log,
cancel-observation-red.log; Fullprüfung run-capped.sh, Format/Build/alleTests/Coverage/Guard.
T077 bleibt bis laufender Unterbrechung/native Endbestätigung/2-s-Messung offen. Der nächste
T071/T077-Zyklus muss den gespeicherten Intent im Dispatcher beobachten; diese Teiländerung
behauptet noch keine vollständige laufende Cancelgarantie. T072/T078/T079 bleiben Stop/Close.

## T071/T075/T077/T080/T082: tatsächlicher lokaler Ausgang bei Unknown

Architektur vor Code architecture/2026-10-29-uncertain-outcomes.md, Ausgang 81c0617.
uncertain-outcome-red.log: bestehende vier offene Deadline-/Caller-Cancelfälle nach
Sollkorrektur fachlich rot (Interrupted statt TimedOut/Cancelled), zwei andere grün.
SQL-Theorie drei fachlich rot (Failed/TimedOut/Cancelled erwartet, Interrupted erhalten),
Crash-Interrupted schon grün. Keine künstliche Änderung an der Produktseite für Red.
Green: dieselben 6 Unit-/4 Integrationfälle. Gemeinsame RequestFailurePolicy.Uncertain
bewahrt lokalen Ausgang und Unknown separat. SQL und kontrollierte Unitprojektion benutzen
dieselbe Regel. Neustart, historische Resultate/Historie und explizite ConfirmEnded-Klärung
für alle vier Ausgänge getestet; Unknown-Slot bleibt bis Bestätigung belegt, kein Replay.

Vollständig grün artifacts/quality/linux/8e1963773f544d67b45f944cbf5f3e9c,
SourceHash 27954b0154dbafc5909da5f4a9e13fe529ca5742070c52729ea582e94f268059.
601 Tests (271 Unit,195 Contract,134 Integration,1 Plattform), null Fehler/Skips,
2036/2105 Produktzeilen = 96,7221 %. Codemetr 274 Dateien/280 Typen, null
Verstöße/Parsefehler/Skips. Vollbefehl unverändert run-capped.sh, Format/Guard/Invoke-Quality;
Logs artifacts/tdd/uncertain-outcome-{red,green,quality}.log. Offene 2-s-Ausführungs-/
native Cancel-/Stop-/Close-Abnahme verbleibt ausdrücklich bei T071/T077/T078/T079/T082.

## T071/T077/T082: aktiven dauerhaften Intent beobachten

Architektur vor Erweiterung architecture/2026-10-29-running-cancel.md, Ausgang da4f78a.
running-cancel-red: beide offenen kooperativen/unkooperativen Sends ignorierten gespeicherten
Intent und erreichten die 2-s-Testgrenze; Cancel zwischen Reservation/Snapshot blieb Running
statt sicherem NotStarted. Drei fachliche Redfälle. running-cancel-sql-red: echter Runtime-
SQLitefall blieb trotz bestätigtem Cancel-Intent über fünf Sekunden Running, Beobachtung
brach an der endlichen Testfrist ab. Kein Infrastrukturfehler als Red ausgegeben.

Green: lokaler begrenzter Monitor liest dauerhaften Intent, bricht Adaptertoken ab und
sichert vorhandenen Unknown-Pfad. Intent vor tatsächlichem Send wird NotStarted abgeschlossen.
16 relevante Unitfälle und ein echter Runtime-SQLfall zunächst grün. Vollregression fand
zwei bestehende Callback-Lifetime-Fälle rot: Monitorcleanup brach den Ausführungstoken nach
bekanntem Ende mit ab, daher OCE statt ObjectDisposed. Voller Fehlbeleg verbleibt als TRX in
artifacts/quality/linux/f1f8d8789177436a84fe18dcf79fc96b. Korrektur: eigener Monitorstop-Token,
getrennt vom Ausführungstoken; geschlossene Callbackgrenze unverändert. Neun gezielte
Callback-/Cancelregressionen danach grün. Eine fehlerhafte zusätzliche Messausgabe war ein
Format-/Syntaxfehler, kein fachliches Red; vor vollständiger Wiederholung korrigiert.

Vollständig grün artifacts/quality/linux/83891ed17c334cf0a3327b9770fd72c8,
SourceHash 9c3f02a7d3ddff896b730a773565ff224019f2409e279b902eb9094535f179f5.
605 Tests (274 Unit,195 Contract,135 Integration,1 Plattform), null Fehler/Skips;
2064/2136 Produktzeilen = 96,6292 %. Codemetr 277 Dateien/283 Typen, null
Verstöße/Parsefehler/Skips. Monotone echte Cancel-bis-dauerhaftes-Ende-Messung aus dem
Integration-TRX: 119,639 ms bei offenem unkooperativem Send, verfügbarem Store. Zustand
Cancelled/Unknown, keine künstliche Freigabe, nach Wiederöffnung gleiches Result und genau
ein Historieneintrag. Befehl unverändert run-capped.sh mit Format/Guard/Invoke-Quality;
running-cancel-quality.log enthält zusätzlich den vorgeschalteten gezielten Neun-Testlauf,
der nicht erneut zur vollständigen Testzahl gezählt wird. Native Endbestätigung ist der
nächste begrenzte T071/T077-Schnitt; Stop/Close verbleiben T072/T078/T079. Keine vollständige
SC-004- oder Storyabnahme behauptet.
