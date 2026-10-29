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
