# Gesamtimplementierung

Created: 2026-10-29T06:32:23Z
Owner: ausführende Implementierungssitzung.
Branch: `feature/reserved-dispatch-contract`.

Auftrag des Eigentümers: „Bitte autonom mit speckit implementieren. Alles“.
Dieser Auftrag ersetzt die frühere Begrenzung auf einzelne Übergabetasks. Alle offenen
Implementierungstasks aus tasks.md werden nach Abhängigkeiten bearbeitet; nach einzelnen
Tasks nicht mit einer bloßen nächsten Empfehlung aufhören. Speckit-implement und der
projektlokale codemetr-guard sind geladen. Keine parallelen Subagenten beauftragt.

## Aktueller Stand

- T112 und T025 bereits implementiert/committet/gepusht; letzter technischer Anker `67a6d3f`.
  Linux-/Windows-Qualität und Codemetr für diesen Anker auf GitHub erfolgreich.
- T113 lokal abgeschlossen: zehn zusätzliche Recovery-Vertragsfälle nach beobachtetem Red grün;
  Falltargets, lesbare Revision, CAS und Reopen außerhalb der Storetransaktion vorhanden.
  233 .NET-Tests im vollständigen Linuxlauf grün, 95,4936 % Produktcoverage.
  Full-Codemetr 98 Dateien/99 Typen sauber. Nachweis: tdd-us5.md.
- T115 lokal abgeschlossen: 263 .NET-Tests, 96,0821 % Coverage, 110 Dateien/112 Typen
  im sauberen Full-Guard. T113 ist auch auf Linux/Windows-CI grün.
- T114 lokal abgeschlossen: 271 .NET-Tests, 96,2433 % Coverage, 115 Dateien/117 Typen
  im sauberen Full-Guard. Pagingstand aa15e97 auch auf Linux/Windows-CI grün.
- Als Nächstes T026/T027 (öffentliche Runtimekomposition),
  US2-Adapter, US5-SQLite/Recovery, US3-Lebenszyklus/Cancel/Diagnose, US4-Plattform-/
  Paket-/Konsumabnahme und schließlich gesamte Polish-/Freigabematrix.
- T114 muss vor dauerhafter Annahme T058 und zugehörigen Fristen T076 umgesetzt werden.
  Aktuelle Architekturbelege vor jedem neuen Schnitt fortschreiben oder neu anlegen.

## Verbindliche Arbeitsregeln

TDD: Signaturen/Stubs → ausführbarer fachlicher Red → minimales Green → grüne Regression
und Codemetr. Kein Python. SDK 10.0.403 / Codemetr 0.1.0 / 87-Prozent-Scope nicht ändern.
Schwere Läufe über /home/user1/dev/scripts/run-capped.sh. Tempdateien unter .agenttemp.
Erledigte Tasks nur nach tatsächlichen Ergebnissen markieren, Fortschritt committen/pushen.
Keine Rohartefakte/Secrets versionieren; nur eigene überprüfbare Nachweise festhalten.

## Externe Grenzen

Vollständige Implementierung ist beauftragt. Echte kostenpflichtige Herstellerläufe T105
benötigen gemäß Task und Constitution zusätzliche ausdrückliche Kosten-/Speicher-/
Testkontofreigabe, die hier nicht erfunden wird. T106 benötigt einen unabhängigen Integrator.
Windows 11/Server-2025/Ubuntu-24.04-Rechte-/Crash-/Soak-Abnahmen brauchen die tatsächlichen
Hosts. GitHub Windows-/Ubuntu-CI ist verfügbar, aber kein Nachweis aller genannten Hosts.
Branchchecks/finale Freigabe bleiben gesondert zu prüfen. Kein NuGet-Push, Deployment,
SharpClaw-Produktwechsel oder stilles Merge aus dem Implementierungsauftrag ableiten.
Offene externe Gates erst nach konkreter vorbereiteter Abnahme auflösen, während unabhängige
Implementierungsarbeit weitergeht. Die Gesamtimplementierung ist noch nicht abgeschlossen.

- T026/T027 lokal abgeschlossen: 279 Tests, 95,4674 % Produktcoverage; Runtimekomposition, öffentliche Parallelvertikale, konservativer kontrollierter Storestart. Vollständiger Stop/Cancel bleibt offen. Nächste Arbeit T030/T036. Nachweis tdd-us1.md.
- Generischer T030/T036-Schnitt umgesetzt: validierter, unveränderlicher ProviderCatalog,
  15 neue Konfigurationsfälle; gesamte lokale Qualität 294 Tests/95,4795 Prozent sauber.
  HTTP-Konfiguration/Credentials bleiben beim folgenden Adapterzyklus, daher Tasks offen.
- Windows-CI fand ein Rennen ausschließlich in der Paralleltestbarriere; mit 4d5158e
  korrigiert, 52 Contractfälle/Full-Guard grün, neue CI folgt. Siehe tdd-us1.md.
- T032/T038 lokal umgesetzt: Conversations-/Responses-Adapter mit 61 deterministischen
  HTTP-/Callback-/Frist-/ID-/Fehlerfällen; Gesamtqualität 355 Tests, 95,8991 Prozent,
  Full-Guard 155 Dateien/157 Typen sauber. Reale Herstellerabnahme bleibt offen.
  Providerdokumentation docs/providers.md, Nachweis tdd-us2.md.
- Runtime-Barrierefix und generische Konfiguration auf Windows/Ubuntu-CI erfolgreich:
  quality 40837654007 (4d5158e) und 40837735315 (0a76b51), Guards ebenfalls grün.
  Nächste konkrete Arbeit: Gemini T033/T039, anschließend restliche US2-Abnahme und SQLite.
- Vor Gemini wurde die operationsgebundene Recoverylücke als T116 konkretisiert und lokal
  geschlossen: optionaler generischer Port, bekannte erste native Identität im Case,
  expliziter CAS statt altem-Kopf-Fallback. 362 Tests/96,0417 Prozent, Full-Guard
  160 Dateien/162 Typen sauber. Native Gemini-/SQLitebeweise folgen ihren bestehenden Tasks.
  Nachweis tdd-us5.md. d795ee3 CI auf Windows/Ubuntu grün.
- Gemeinsame interne HTTPquellen aus grünem OpenAI-Stand extrahiert, ausschließlich per
  Compile-Link im Providerpaket, kein Kerntransport/neues Paket. Vollständige Qualität
  362 Tests, 96,0499 Prozent, 163 Dateien/165 Typen sauber. Nächster Schnitt Gemini-Red.
- T030/T036 und T033/T039 lokal umgesetzt: generische plus beide native Konfigurationen,
  Gemini-Lazy-/Root-/Head-/Recovery-/Inspect-/Cancelvertrag mit 63 Adapterfällen.
  Gesamtqualität 425 Tests, 95,4658 Prozent, Full-Guard 181 Dateien/183 Typen sauber.
  Native Kontextcodes/weitere Grenzfälle und Freigabeumfang bleiben ausdrücklich offen,
  konkrete Folgetasks/Owner in tdd-us2.md. Nächste Arbeit öffentliche Integration/Erweiterung
  und Kontext-/Bindingsperren, danach SQLite. Shared-/T116-CI auf Windows/Ubuntu grün.
- T035/T041 und Providerdokumentation T046 lokal umgesetzt: öffentliche Runtime mit
  beiden nativen Wireverträgen und drittem hosteigenen Agentadapter, je drei Eingaben,
  drei überlappende Sends. Passives natives compaction-Item nach beobachtetem Red toleriert,
  ohne eigene Kontextaktion. 427 Tests/95,4658 Prozent, 189 Dateien/191 Typen sauber.
  T044 bleibt für gesamten Close-/US2-Umfang offen. Nächste konkrete Arbeit Bindungssperren.
  Native 057c33c CI unter Windows/Ubuntu erfolgreich; Nachweis tdd-us2.md.
- T117 lokal abgeschlossen: explizite generische Fortsetzungsverfügbarkeit, atomare
  Sperre im kontrollierten Abschluss, native Flags und öffentliche Queueregression.
  444 Tests/95,4992 Prozent, Full-Guard195 Dateien/197 Typen sauber. SQLite/Close folgen
  T060/T065/T037/T079. Voriger Stand6344816 CI auf Windows/Ubuntu grün.
- T031/T037-Metadatenteilschnitt lokal grün: eingefrorene Revision, autoritative
  UpdatedUtc-/ClosedUtc-Snapshots, sechs neue Redfälle. 450 Tests/95,5248 Prozent,
  Full-Guard196 Dateien/198 Typen sauber. Close und reale SQLpersistenz bleiben offen.
- SQLiteformatvertikale T049/T056 lokal umgesetzt: 28 echte Dateifälle, acht Tabellen,
  explizites Create/Open ohne Ersatz/Migration, Vorprüfung und privater Journalkopiepfad.
  Gesamt478 Tests/94,3043 Prozent, Guard206 Dateien/208 Typen sauber. Domainports/Bootstrap
  sind noch Gerüste; nächster Auftrag echter Journalprozess, dann Annahme/DBqueue.
- T048-Gerüst und echte Hotjournalregression: zwei harte Kindprozessabbrüche, bestätigter
  Bestand zurückgerollt bzw. beide Originaldateien bei unbekanntem Format unverändert.
  Portable Fixturemarkerrechte nach echtem umask022-Red. Gesamt480 Tests/94,3043 Prozent,
  Guard210 Dateien/213 Typen sauber. Fachliche Crashpunkte/SC-009 bleiben offen.
  Format1df4b05 CI auf Windows/Ubuntu grün; nächster Auftrag Annahme/DBqueue.
- T057/T058 lokal umgesetzt: bounded einzelner DBworker, persistierte Session/Bindung,
  atomare Input-/Sequence-/Capacityannahme, Kulturroundtrips, Busy/Cancel/Dispose sowie
  reale Teilwrite-/Commitfehler. 502 Tests/95,2351 Prozent, Guard227 Dateien/231 Typen sauber.
  Alte Opening/Running werden vor Recoveryumsetzung sicher abgewiesen. Restliche SQLite-
  Ports/Keysetqueries und fachliche Crashpunkte bleiben offen. Nächster Auftrag T063,
  dann Dispatch/Abschluss/Recovery. Crashstand457e901 CI auf Windows/Ubuntu grün.
- T063-Session-/Requestkeysets lokal umgesetzt: elf echte SQLfälle, stabile Cursor nach
  Reopen, Kandidaten ohne verwendetes Receipt auffindbar, fremde Scopes abgewiesen.
  Gesamt513 Tests/95,5105 Prozent, Guard230 Dateien/234 Typen sauber. History/Recovery
  folgen ihren Schreibvertikalen. Nächster Auftrag T051/T059/T060 Dispatch/Abschluss.
  Annahmestand4113798 auf Windows/Ubuntu-CI grün.
- T059/T060 lokal umgesetzt: atomarer FrozenDispatch/Observation und bekannter Abschluss
  mit Result/History/Binding/Release,19 neue SQL-/Dispatcherfälle. Gesamt532 Tests/
  96,0674 Prozent, Guard242 Dateien/247 Typen sauber. T051/T053-Crashumfang bleibt offen.
  Windows-CI an427691b fand Sharingverletzung nach echtem Kill; Harness bounded Dateifreigabe
  ergänzt, native Neubestätigung folgt neuer CI. Nächster Auftrag Unknown/Start-Recovery/CAS.
- T062/T063 und SQLbootstrap-Teilschnitt lokal umgesetzt: Unknown/Openingfälle, gebundene
  native Belege, CAS/Schließabsicht/Endbeleg, vier Keysetlisten und Konsistenzprüfungen.
  548 Tests/96,6051 Prozent, Guard252 Dateien/257 Typen sauber. Native Registrykoordination
  T061, öffentliche Runtime-/echte fachliche Killabnahme T055/T066 bleiben offen.
  Dispatch6072d52 CI auf Windows/Ubuntu erfolgreich, Windows-Sharingnachzug bestätigt.
- T061 lokal umgesetzt: Availability vor Workers, persistenter Sperrgrund für lokale
  Konfiguration, nur eigene Sperre bei Wiederkehr lösen; öffentliche Runtime mit SQL
  setzt ursprünglichen Kandidaten fort. 560 Tests/96,6748 Prozent, Guard258 Dateien/
  263 Typen sauber. Recovery1bf65f1 CI auf Windows/Ubuntu grün. Nächster Auftrag echte
  fachliche Crashbarrieren und zehn SC-009-Läufe T048/T055/T066; native Authfehler T080.
- Zehn echte SC-009-Kills lokal bestanden: exakt2 fertige/3 QueuedA/1 RunningB vor Kill,
  ursprüngliche6 IDs erhalten, A-FIFO fortgesetzt, B Unknown und reserviert, kein Replay.
  Zusätzliche sofort grüne Prozessregression, kein künstliches Red. Gesamt570 Tests/
  96,6748 Prozent, Guard262 Dateien/268 Typen sauber. Feine Crashpunkte T048/T050/T051
  und echte Zielsystem-/Rechteabnahme bleiben offen. be8cf38 Windows/Ubuntu-CI grün.
- Sieben zusätzliche echte Übergabe-/Commit-Killpunkte lokal grün, gemeinsamer separater
  TRX19/19 einschließlich SC009/Journal. Keine Produktänderung oder künstliches Red,
  Grenzen vor-API gegenüber mitten-in-SQL ausdrücklich dokumentiert. Gesamt577 Tests/
  96,6748 Prozent, Guard263 Dateien/269 Typen sauber. T048 vorbereitet/ausgeführt,
  nächster Auftrag T070/T075 Zustandsmatrix, danach Cancel/Stop/Close/Retention.
  SC009-Stand9fdbda7 CI auf Windows/Ubuntu grün; übrige Abnahme bleibt offen.

### 2026-10-29T12:32:22Z: Zentrale Zustands- und Finalisierungsregel

T070/T075 vertikal ergänzt, noch keine vollständige Storyabnahme. Unmögliches
Interrupted/NotStarted zuerst am Validator und echten SQLite-Commit rot beobachtet,
dann abgewiesen. Alle zulässigen Paare und Übergänge zentral; Recovery darf nur die
Gewissheit desselben Endzustands klären. Konkurrierende Known/Unknown-Schreiber erzeugen
genau ein dauerhaftes Ergebnis/Historieneintrag, Unknown behält den Slot.
590 Tests grün, 96,6200 % Gesamtabdeckung, Codemetr 267 Dateien/273 Typen sauber.
Nachweis tdd-us3.md; Qualitätslauf b9adac26d0fa45a5875878f4aa400dcc. Offene vollständige
Cancel-/Timeout-/Stop-/Close-Abnahme bleibt den bestehenden T070–T085 zugeordnet.
