# T114 und lokale Annahmefrist: Commitgewissheit getrennt von Ausführung

Created: 2026-10-29T06:56:44Z
Owner: ausführende Implementierungssitzung auf `feature/reserved-dispatch-contract`.
Basis `aa15e97`, Orchestrierungsvertrag §3, lokale Admissionteile von T076.

Vor Entwurf untersucht: RequestAdmission validiert Session/Input/Timeout/Workspace und
übergibt nur SubmitRequest an CommitAcceptanceAsync. Die RequestId wird bisher erst im
Double erzeugt; bei verlorenem Warten fehlt ein Nachschlageschlüssel. PublicError kennt nur
ExecutionCertainty, Caller-Cancel läuft als rohe OperationCanceledException. Der Commit
wird ohne eigenen AdmissionTimeout abgewartet. Bestehende Tests sichern Commitbarriere,
Receipt trotz Caller-Cancel nach bekanntem Commit, Queuekapazität, unveränderte Eingabe
und separate Sessionsequenzen. Read-/Listfacetten sind seit T115 implementiert.

SHA256: RequestAdmission `a0d7ca25a57525710c405fcf223c57c2bee718445bcc6c38914f9bdf72f6c00f`,
ISessionStore `f5b8034dcdd748e630f198320ef5de574cc4464704988ddcd0643730992dc6f6`,
PublicError `2e804cb72c7bf6a729948214676bf5af51406233bb60d7ad765de4e6a9302a9f`,
RequestReceipt `25f9afdee48607855eedd24942f355456e9f9b0eef7b39b86fdb302b7cfeb832`,
InMemorySessionStore `81d310c60d5edd29f4991a027f2f71f4d54b73b4b767be826bc518a54c94830a`.
Produktdateien unter Common, Double unter UnitTests/Fixtures. Keine SQL-Implementierung
betroffen. Die Signaturänderung ist vor Veröffentlichung, keine persistierte Migration.

Entscheidung vor Umsetzung: genau eine Kandidaten-ID vor dem Commit erzeugen und in
RequestAcceptance mit der unveränderten Submission übergeben. Store vergibt nur Sequence
und Kapazität, übernimmt die Kandidatenkennung und weist Kollisionen ab. PublicError
bekommt optionales AdmissionStatus (NotAccepted/Committed/Unknown), Caller-Cancel eine
AdmissionCancellationException mit diesen bereinigten Metadaten. Ein bekannter Receipt
ist Committed und gewinnt gegen verspäteten Timer/Caller-Cancel. Unknown enthält Kandidat,
aber keine bestätigte Annahme und kein sicheres externes NotStarted; keine Wiederholung.

Annahmefrist beginnt am Submit-Eintritt und umfasst Read/Validierung/Commitwarten. Sie nutzt
injizierbaren TimeProvider und begrenzt auch einen nicht kooperativen Storewait; Token bis
zum Store weitergeben. Abbruch vor Commit ist sicher NotAccepted, nach Commitbeginn nur
bei ausdrücklichem Store-Rollbackbeleg, sonst Unknown. Ein noch nicht auffindbarer Kandidat
bei laufendem Commit beweist keine Nichtannahme; späterer Commit bleibt per ID auffindbar.
Validationregeln bei wachsendem Umfang als eigene fachliche Verantwortung extrahieren,
aber nicht duplizieren. RequestTimeout/Queuewartezeit/Stop bleiben separate Fristen.

Zuerst Red-Fälle: lokale Ablehnung und Pre-Cancel NotAccepted; Storefehler/Rollbackstatus;
Caller-Cancel während nicht kooperativem Commit mit Kandidat, späterer Commit und Lookup
unter derselben ID; fünfsekündiger AdmissionTimeout; bekannter Commit trotz Cancel; null
automatische Resubmits und unveränderte Queue-/Sequenzautorität. Minimale Stubs/Signaturen
zuerst ausführbar machen, kein Compilefehler als Red. Kontrollierte Ignorier-/Ackhooks
bilden ungewisse Transaktionsgrenzen ab, keine SQLite-Haltbarkeit. T058/T076/T082 behalten
ihre zusätzlichen nativen Persistenz-/Cancel-/Zeitnachweise. Keine offene Architekturfrage.
