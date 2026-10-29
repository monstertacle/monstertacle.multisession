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

## T071/T077/T082: belegtes Ende statt bloßer Cancel-Bestätigung

Architektur vor Code architecture/2026-10-29-cancel-end-evidence.md, Ausgang 0f605eb.
native-cancel-red: 6/6 fachlich rot, explizites Ende des ursprünglichen Sends wurde
verworfen und nativer Cancel nie aufgerufen. cancel-continuation-red nach korrigierter
Fixturekomposition: 3/4 fachlich rot (gültige Fortsetzung nicht übernommen, geforderter
operationsgebundener Port nie aufgerufen); fehlender Port schon sicher blockiert/grün.
Erste unkompilierbare Fassung mit nicht verlinkten Unitfixtures ist kein Rednachweis.
native-cancel-sql-red: 1/2 fachlich rot, natives bestätigtes Ende blieb Unknown;
bestehender unbestätigter SQLfall weiter grün.

Green: 15 relevante Unitfälle, 9 Operation-/Cancelverträge und 2 echte Runtime-SQLfälle.
DispatchTermination beobachtet ursprünglichen Send und höchstens einen nativen Cancel
unter einem gemeinsamen 1-s-Systemtimerbudget. Nur acknowledged Operations-ID ist Ziel;
Abbruchack NotStarted/Unknown reicht nicht zum Freigeben ursprünglicher Arbeit. Outcome
bleibt lokaler Cancel/Timeout. Bekannte Endbelege und geprüfte Fortsetzung committen atomar.
Bei operationsgebundener Capability wird vorhandener Recoveryport benutzt, falls Reply
kein Binding liefert; fehlender/fremder/fehlerhafter Port hält Unknown, kein alter Headfallback.
Sink stellt ausschließlich erfolgreich gespeicherte Operations-ID lokal bereit. Cleanup
ändert die bestehende geschlossene Callbackgrenze nicht. Native APIs/Geheimnisse bleiben
in ihren Adaptern, keine I/O unter SQL-Lock, keine Sendwiederholung.

Drei zusätzliche sofort grüne Regressionen prüfen unkooperativen nativen Cancel, fremdes
Nativebinding und fehlende acknowledged ID. Sie sind keine weiteren Redzyklen. Die finalen
9 NativeCancellationTests ergänzen den bestehenden Unitumfang. Bisherige Deadline-,
Callback-, Operation-Recovery- und echte Crashproben vollständig regressiert.

Vollständig grün artifacts/quality/linux/d60e6e62218b48b4b7f7794e218cdd3a,
SourceHash 10f1b5b67fb53660ce22610f691625a6964b52ec534c266e7908e5d7327424c5.
619 Tests (283 Unit,199 Contract,136 Integration,1 Plattform), null Fehler/Skips;
2116/2189 Produktzeilen = 96,6651 %. Codemetr 280 Dateien/286 Typen sauber.
Monotone echte SQLruntime-Messung aus Integration-TRX: 108,499 ms bei kontrolliertem
bestätigtem Nativeende und freiem Slot, 1103,991 ms bei unkooperativem offenen Send ohne
Endnachweis mit erhaltenem Slot. Beide nach Wiederöffnung identisch, genau ein Result/
Historieneintrag. Keine Livehersteller-Abnahme behauptet. Befehle run-capped.sh plus
Format/Guard/Invoke-Quality, artifacts/tdd/native-cancel-{red,green,quality}.log,
cancel-continuation-red.log und native-cancel-sql-red.log.
Vollständige T070/T071/T077-Abnahme braucht weiterhin Matrixaudit; T072/T078/T079
implementieren Stop/Close, T085 dokumentiert vollständige SC-004-Messung. Keine vorzeitige
Storyfreigabe. Vorheriger Commit 0f605eb Windows/Ubuntu-CI grün: quality 40876309454,
codemetr-guard 40876309584.

## T072/T078/T082: kontrollierter Stop mit dauerhaftem Queueabschluss

Architektur vor Code architecture/2026-10-29-controlled-stop.md, Ausgang 7dd8bec.
controlled-stop-red: 2/2 fachlich rot (102 wartende Einträge blieben Queued, realer
Finalisierungsschreibfehler wurde ohne Fehlerantwort übergangen). stop-cleanup-red:
2/2 fachlich rot (Cleanup ignorierte 200-ms-Frist bis zur 2-s-Testgrenze, parallele Stops
riefen Storecleanup zweimal auf). Minimales Green danach 8 Ownership/Startup/Cleanup-
Verträge und 2 echte SQLfälle. Weitere tatsächliche Redregression repeated-failed-stop-red:
ein wiederholter Stop nach fehlgeschlagenem dauerhaften Abschluss behauptete Erfolg.

RuntimeShutdown nutzt vorhandene begrenzte Session-/Requestseiten und atomaren Cancel;
keine neue SQLfachregel/Storefacette. Stop sperrt vor erstem Await die Runtimeannahme,
stoppt die Storeannahme und aktive Worker, finalisiert Queued unabhängig von ihren
Konfigurations-/Recoveryblockierungen. Gemeinsame Stop-Serialisierung und Cleanupaufgabe,
eigenes Callerbudget auch beim Warten/Cleanup. Fehler wird auch nach Ressourcenschluss
gespeichert wiedergegeben. Lease erst nach Storeabschluss frei, keine implizite Löschung.
Dispose nach bereits geschlossenem fehlerhaftem Stop bleibt Ressourcenoperation.

Vollregression 3da07397cf89458ca2afbbc172b19dcf: ein Availabilitytest benutzte Runtime-Dispose
als Queued-erhaltenden Crash, wodurch die jetzt korrekt abgebrochene Anfrage fälschlich
Succeeded werden sollte. An den Vertragsquellen geprüft und in zwei Anforderungen aufgeteilt:
Storegrenze ohne Runtime-Stop erhält Queued und dessen ID; kontrollierter Stop erhält Cancelled
und setzt ausschließlich eine neue explizite Folgeanfrage ab. Keine Änderung der Startupregel
und keine native Neueröffnung. Echte Prozesskill-Matrix weiter unverändert grün.
Zwei sofort grüne neue Stopregressionen mit offenem Send/queued Followern ergänzen bereits
zuvor rote Cancelpfade; sie sind keine zusätzlichen Redzyklen.

Vollständig grün artifacts/quality/linux/3a17979ae310492b86e671119298bc1a,
SourceHash be0a2622e88367de3f33724f86e5532c5c796f932e81a51ee13a5d2dd6525aee.
626 Tests (283 Unit,201 Contract,141 Integration,1 Plattform), null Fehler/Skips;
2162/2236 Produktzeilen = 96,6905 %. Codemetr 283 Dateien/289 Typen sauber.
Monotone echte Stopmessungen im Integration-TRX: 102 Queued über zwei Requestseiten und
zwei Sessions 535,000 ms; offener Send mit bestätigtem Ende 33,066 ms; unbestätigt
1004,673 ms. Jeweils unter Default10s, ursprüngliche IDs und Resultate nach Reopen erhalten;
Unknown behält genau seinen Slot, Follower sind NotStarted und wurden nie gesendet.
Kontrollierter Cleanup über 200-ms-Callerbudget meldet StorageUnavailable, Lease bleibt
bis tatsächlichem Storeabschluss gehalten. Befehle run-capped.sh plus Format/Guard/
Invoke-Quality, artifacts/tdd/controlled-stop-{red,final-green,quality}.log und
stop-cleanup-red.log. Qualitätslog enthält zusätzlich gezielten 9-Testvorlauf, der nicht
zur vollständigen Testzahl addiert wird. Vorheriger 7dd8bec Windows/Ubuntu-CI grün:
quality 40877481208, codemetr-guard 40877480850.
T072/T078 bleiben bis abschließendem Lifecycleaudit und Close-Abnahme T079 offen;
T085/T104 behalten vollständige finale SC-/Plattformfreigabe, keine Produktfreigabe.

## T031/T037/T072/T079/T082: öffentlicher Session-Close

Architektur vor Code architecture/2026-10-29-session-close.md, Ausgang42fc164.
session-close-red: 5 ausführbare Fälle fachlich rot am Stub; erweiterter Satz
session-close-final-red: 8/8 fachlich rot einschließlich Idle/Queued, Unknown, fehlend/
Caller-Cancel, Opening/lateBinding, echter Teilwritefehler und bestätigtes/unbestätigtes
Running bei unabhängiger Arbeit. Minimales Green zunächst5/8; drei fehlerhafte neue
Testvergleiche hielten deserialisierte Dictionaryreferenzen für recordgleiche Werte.
Die Persistenzassertion vergleicht jetzt unveränderte gespeicherte Snapshotdaten und
ClosedUtc, kein Produktfehler als Red erfunden. Acht Fälle danach grün.

ISessionClosureStore hält Schließabsicht und neue Prüfung getrennt. Neue Annahme und spätes
Binding nach Closing/Closed abgewiesen; Queued und Running-Intent atomar. Gemeinsame
SessionClosurePolicy und SQLprojektion prüfen aktive Arbeit, jede Unknown-Anfrage,
unfreigegebene Reservation und offene Fälle. Completion kann Closure nach Freigabe selbst
abschließen. Recovery benutzt dieselbe Regel ohne historischen Erfolg nachzutragen.
Callerabbruch nach gesichertem Intent bleibt ohne Rücknahme; zusätzlicher sofort grüner
Runtimefall bestätigt automatisches Closed nach späterem Native-Endbeleg trotz verlassenem
Caller. Keine Löschung oder externe Close-/Deleteaktion. Kontrollierte Storefacetten nach
grünen283 Unit-/201 Contract-/21 relevanten SQLfällen ausgerichtet, gemeinsame Cancelregel
wiederverwendet. Codemetr meldete danach Komplexität26>25 in ControlledRecoveryState.Resolve;
33 relevante Verträge vor/nach reiner Zerlegung grün, Grenze unverändert. Ursprünglicher
Guardbefund artifacts/tdd/session-close-codemetr-red.json.

## T078/T082: tatsächlicher Windowsbefund und atomarer Sammelabbruch

CI40879343516 auf42fc164: Windows-Stop mit102 Queued überschreitet seine10s-Frist und
meldet StorageUnavailable; keine native Freigabe für diesen Kopf. Protokoll
artifacts/tdd/stop-ci-failure.log. 27s Gesamtfall sind kein isolierter Stopmesswert;
keine ungeprüfte Hardwareursache behauptet. Architektur ergänzt vor Änderung:
architecture/2026-10-29-controlled-stop.md. Minimaler Port in bestehender Cancellation-
Facette, ein SQLitecommit je Sitzung statt je Input, intern bounded Sequence-Keysetseiten.
Dieselbe Fachregel und derselbe Code auch für Close/Recovery-Close; Cancellation wird pro
Seite/Eintrag ausgewertet, kein Provider-I/O im Lock. Zehn-Sekunden-, Journal- und
synchronous-Garantien unverändert. Vorhandene echte Windowsregression ist der Rednachweis,
kein künstlicher lokaler Timingfehler. Zusätzliche sofort grüne Regression verschiebt den
Fehler auf den zweiten Queueeintrag: auch dessen bereits bearbeiteter Vorgänger bleibt
nach Rollback Queued, kein Result-/Historienpräfix wurde committet.

Vollständig lokal grün artifacts/quality/linux/e0d078cd30aa42df8c04d1742ec0c926,
SourceHash103248be6ab7a1175652fcb4ca600a149a7ae5c1fe2be652e79baed5096f7e4d.
635 Tests (283 Unit,201 Contract,150 Integration,1 Plattform), null Fehler/Skips;
2230/2303 Produktzeilen =96,8302 %. Codemetr292 Dateien/298 Typen, null Verstöße/
Parsefehler/Skips. Reale Stopmessung im Integration-TRX:102 Queued27,322ms, aktiver
bestätigter Send21,803ms, unbestätigt1002,896ms. NativeCI muss den neuen Stand noch
bestätigen; lokaler Wert ersetzt den Windowsnachweis nicht. Logs session-close-quality.log
und stop-batch-green unter artifacts/tdd, kompletter Format/Guard/Invoke-Quality-Lauf über
run-capped.sh. Vorherige lokale Zwischenläufe0f5f2e7d4a60460f9b3f2b1d89430521 und
22508e3f33d74af3bf978a5268e2507a bleiben nachvollziehbar, sind kein finaler Quellstand.

Befundabgleich: Close-Lücken und lateBind sind mit Änderung/Tests behoben; Windows-Stop
lokal korrigiert, native Nachprüfung gehört weiterhin T078/T082/T104. Bei Retentionanalyse
übernommener Optionsbefund hat konkreten bereiten Folgeauftrag T118 mit Owner, Quellen,
Schritten und überprüfbarer Abnahme in tasks.md: Factory-/Runtimeoptions dürfen vor T064
nicht unabhängig wirksam sein. T076 behält den vollständigen Frist-/Dispatchaudit;
T070–T085 bleiben bis Story-/TDD-/Diagnostikabnahme offen. Kein Produktabschluss behauptet.

## T074/T081 erster Diagnoseschnitt

Architektur vor Code: architecture/2026-10-29-diagnostics.md mit aktuellen Quellenhashes.
artifacts/tdd/diagnostics-red.log zeigt tatsächlich ausführbare3/3Fehler gegen No-op:
fehlende strukturierte Ereignisse, fehlende Activity, fehlende Loggerfehlerzählung.
Vorherige Compile-/Analyzerfehler waren kein Red. Nach Positivlistenimplementierung3/3grün.
Separater diagnostics-hooks-red.log zeigt2/2 echte Runtimefehler wegen fehlender Hooks;
nach Hookimplementierung5/5grün (diagnostics-hooks-green.log). Fünf weitere unmittelbar
grüne Regressionen prüfen Listenerausfall, ungültige Enumwerte/disposed Source und drei
Storebestätigungsgrenzen; kein nachträglich erfundener Red. diagnostics-regression.log10/10.

Runtimeannahme meldet erst nach Receipt, Dispatcher erst nach bestätigt abgeschlossenem
CommitCompletion oder MarkUncertain. Verlorene Bestätigung produziert keinen geratenen
Ergebnislog. Monotone Stopwatchdauer ab Annahmebeginn beziehungsweise neuer Reservierung.
Keine Providerantwort-/Prompt-/Workspace-/Bindingdaten, keine Exceptionobjekte an ILogger.
Explizit hosteigenes optionales SessionDiagnostics; Host entsorgt nach Runtimeende.
Listener- und Loggerausfälle getrennt abgefangen und gezählt. Gleichzeitige Tests filtern
Activitysampling nach eigener Instanz, um fremde Runtimeprüfungen nicht zu beeinflussen.
Codemetr forderte Zerlegung der Diagnose-Testklasse wegen Kopplung: Datenschutztests,
Runtime-/Storegrenzentests und Loggerfixture getrennt, Grenzwert unverändert.

BCL-Metriken, Queue-/Reservierungsprojektion und weitere Lifecycleübergänge fehlen noch;
T074/T081 bleiben ausdrücklich offen. Volltor a9fcc13f43c84e61b7e3388fd5883066 hatte
alle Tests grün, verwarf seinen Gesamtnachweis aber korrekt wegen zwischenzeitlicher
Quelländerung. Es ist kein finaler grüner Qualitätslauf; unveränderten Stand erneut prüfen.

## T074/T081 Metriken und lokale Statistik

Architektur vor Erweiterung im selben Diagnosenachweis, Quellhashes und primäre BCLquelle
angegeben. Tatsächlich ausführbare Redläufe (Exit1): metrics-red2/2 mangels Histogrammen/
Zähler, statistics-red2/2 wegen falscher Nullbelegung und ignorierter Cancelreads,
gauges-red1/1 wegen fehlender Cacheinstrumente, statistics-monitor-red3/3 wegen fehlendem
Sampleaufruf. Vorherige Analyzer-/Compilefehler sind kein fachliches Red.
Green: metrics-green12/12Diagnosefälle, statistics-green2/2SQLfälle,
statistics-monitor-green16/16Diagnose-/Monitorfälle. statistics-runtime-green3/3 ist
der echte SQLitewiederanlauf mit Queue1/Occupied1/Unknown1, zusätzlich sofort grüne
Regression, kein rückwirkend behauptetes Red. Der erste SQLgreen-Versuch verfehlte
ConfirmEnded wegen eines fehlenden Bindungsbelegs im Test; Test nutzt nun ausdrücklich
bestätigte ursprüngliche Bindung, keine Aufweichung der Recoveryregel.

Readonly ISessionStatisticsStore mit einem atomaren SELECT, ReleasedUtcISNULL und
Request.CertaintyUnknown statt Recoveryfallanzahl. RuntimeStatisticsMonitor hat einen
beobachteten eigenen Task, initiales Sample und1sPause. Datenlesefehler isoliert mit
FailureCount/Cachewithdrawal; Cleanup cancelt/awaitet vor Store-/Leasefreigabe. Kein I/O
in Observablecallback und keine unendliche Stopabhängigkeit von tokenignorierendem Read.
Metriksnapshot ist Diagnose, niemals Scheduling-/Kapazitätsautorität.

Metriken: multisession.admission.duration und .execution.duration inSekunden,
multisession.requests.completed als Zähler bestätigter beobachteter Abschlüsse;
multisession.requests.queued, multisession.reservations.occupied/.uncertain als Gauges.
Unknown ist Teilmenge von occupied. Vor erstem Sample, bei Readfehler und nach Stop keine
Messung, kein falsches Null. Labels nur state/certainty/error. Meter.Scope unterscheidet
lokale Instrumentinstanzen ohne ID-Messlabels. Pro Runtime ein eigenes hosteigenes
SessionDiagnostics; zwei getrennte Scopes erhalten ihre eigenen Cachewerte.
Zähler beschreibt beobachtete bestätigte Abschlüsse, keine rückwirkende Inventur aller
historischen Ergebnisse. Direkt im Store aufgelöste/abgebrochene Fälle erzeugen keine
nachträglich erfundenen Dispatcherereignisse. Vollständige Übergangs-/Observabilitymatrix
und T074/T081-Abschluss bleiben im laufenden Auftrag offen.

## T073/T074/T080 öffentliche Providerfehler und Restkapazität

Architektur vor Erweiterung: architecture/2026-10-29-provider-failure-isolation.md.
provider-failure-red.log: tatsächlich31Tests ausgeführt,16rot/15grün. Rote Assertions:
lokale/undefinierte Kategorien passieren ungefiltert, typisierte Auth-/Rate-/Kontextfehler
werden ExecutionFailed, fremde Kennungen/AdmissionStatus und widersprüchliche Gewissheit
bleiben stehen. Keine Compiler-/Analyzerfehler als Red. Danach provider-failure-green31/31.
Explizite erlaubte Kategorien aus Architektur umgesetzt; typisierte Exceptions bleiben
Unknown mit belegter Reservierung, Kategorie liefert keinen externen Endbeleg.
DispatchReplyPolicy wird auch durch direkte SQLitecompletion verwendet. Bestätigte lokale
Observationfehler umgehen den Providerfilter bewusst, damit StorageUnavailable echt lokal
bleibt. Keine Provider-/Exceptiontexte in PublicError. Herstellerwahl, Input und Bindung
bleiben unverändert.

failure-isolation-green.log33/33 einschließlich zweier neuer unmittelbar grüner Regressionen:
fünf bestätigte unterschiedliche Fehler/fünf unabhängige Erfolge jeweils ein Send/Result,
volle zwei Unknownslots lassen unabhängige Arbeit Queued; explizite ConfirmEnded-Klärung
freigibt genau einen Slot, ursprüngliches Failed bleibt Failed. Diese beiden Fachregeln
waren bereits vorhanden, kein künstliches Red für T073. provider-failure-sql-green.log3/3
zusätzliche sofort grüne Persistenz-/Neustart-/Historyfälle für erlaubte, lokale und
undefinierte Kategorien. T073/T074/T080 werden erst nach gesamter Story-/TDDprüfung
abgeschlossen, nicht allein aus diesem Schnitt.

## T119 Zeichenintegrität statt stiller UTF8-Ersetzung

Architektur/Primärquellen/Hashes zuerst in architecture/2026-10-29-utf8-integrity.md.
utf8-integrity-red.log11Fälle ausgeführt,10rot/1grün: ungültige hohe/niedrige UTF16-
Surrogate wurden akzeptiert, ungültige Ausgabe als Succeeded gespeichert. Gültige Emoji/
Whitespacegrenze7Byte schon korrekt. Kein ungültiges Literal in Source/InlineDatametadaten;
Surrogate nur im Testprozess aus Codepoint erzeugt, keine Rohinhalte in Fehlern.
Danach strikter gemeinsamer UTF8Encoding(false,true)-ByteCount mit bereinigtem
EncoderFallbackfehler. InvalidInput vor Kandidat/Commit/Send; fehlerhafte Ausgabe
ExecutionFailed/leer/OutputCompletefalse, vorherige Gewissheit unverändert. Legitimer
leerer Auth-/anderer Fehlerreply wird nicht pauschal zur anderen Kategorie umgeschrieben.
utf8-integrity-green28/28bestehende/neue UTF8fälle. Zusätzliche sofort grüne Regressionen
bestätigen echte gültige U+FFFD-/Emoji-Scalars und direkte SQLiteannahme/-Completion/
Neustart/History. Gültige Benutzereingaben werden nicht heuristisch bereinigt oder getrimmt.
Keine rückwirkende Rekonstruktion/Änderung historischer Inhalte. T119 wartet noch auf
passende vollständige lokale/native Lieferstandnachweise; andere offene Aufgaben bleiben
bestehen.

T119 nativ bestätigt zu9189574: Qualität40907585230 auf Windows/Ubuntu und Guard40907585258
vollständig grün. Damit expliziter UTF8-Integritätsauftrag umgesetzt und geprüft; Markierung
in tasks.md folgt. Allgemeine Produkt-/US3-/Plattform-/Liveabnahmen bleiben davon getrennt.

## T076 Cancellation nach Queuebeginn der Storevorbereitung

Architektur/Quellen vor Änderung: architecture/2026-10-29-startup-cancellation.md.
startup-cancellation-red.log2/2 echte rote Fälle: Token nach Übergang des Workitems zu
running im bestehenden Leasecheck ausgelöst, Start committtete dennoch Recovery und
warf keine OCE. Kein geschätzter Delay/Reflection/Produkttesthook. Danach Token von
IStoreBootstrap über State.Start in Recoverytransaction und alle sieben Rowprüfungen
weitergegeben, je Row/Recoveryeintrag und vor Commit geprüft. startup-cancellation-green
171/171bestehende/neue Storage-/Recovery-/Cancelfälle grün. Cancellierte uncommittete
Vorbereitung lässt DBhash/Running/History/Cases unverändert, Hostlease bleibt gehalten;
frischer Token darf konservative Recovery genau einmal durchführen.

Keine präemptive Unterbrechung einzelner SQLiteanweisungen/JSONrowparses behauptet;
Cancel greift an kooperativen Grenzen. Bekannter Commit wird nicht rückwirkend gelöscht,
andere fachliche Write-/Unknownregeln unverändert. Vollständiger Frist-/Lifecycleaudit
T076/T083 und die übrige US3-Abnahme bleiben offen.

## T076 Stop unmittelbar vor Send: deterministische Regression

Volltor27f597763ad14438be6e21ef915314b3 zeigte einen bestehenden Stop-Race im
Metriktest. Kontrollierter GetRequest-Readhook liefert reproduzierbares fachliches Red
`before-send-cancellation-red.log`: erwarteter Cancelled, tatsächlichStorageUnavailable.
Keine Compilerfehler als Red gewertet. Korrektur analog zum benachbarten FrozenDispatch-
Read: Caller-/StopOCE vor ProviderSend wird Cancelled/NotStarted ohne abgebrochenes
Committoken gespeichert, Reservation freigegeben; originale Kennungen erhalten.
Echte Readfehler bleiben StorageUnavailable; keine Frist aufgeweicht. Architekturbeleg
`architecture/2026-10-29-before-send-cancellation.md`. Vollständiger T076-Audit bleibt offen.

## T076/T083 Recovery-I/O beim Stop

Vorabanalyse `architecture/2026-10-29-recovery-stop-ownership.md`. Zwei fachlicheRedfälle
`recovery-stop-ownership-red.log`: nachStop sindöffentlicheInspect-/Resolveaufgabennicht
fertig, Eigentumaberfreigegeben. Beidejetzt übervorhandenesRuntimeOperationsregister;
ServicebesitztbereitsToken-WaitAsync undRevisions-CAS. KeineÄnderunganderEntscheidung
oderungeklärtenReservation. ErstesGreen schlossInspect, zeigteaberbeimResolve nocheinen
äußerenasynchronenWrapper; dessenTask war nichtdieverfolgteTask. Rückgabe jetzt direkt
aufdasselberegistrierteTask bezogen, PulseinnerhalbdieserOperation. Alle208Contracttests
grün,keineTestfristverlängerung. Case/Historygewissheit undReservationunverändert,
spätererProviderabschluss veröffentlichtkeineLateEntscheidung. Volltor/nativeCI folgen;
übrigerStart-/Close-/Fehler-/Fristaudit bleibtimvorhandenenT076/T079/T083-Auftragoffen.
