# T110: Freigabematrix

Aktueller technischer Kandidat nach T125/T126: **fb1a167 / Source d51bf9f9...**,
native Gesamt-/Paket-/Guard-/Auditprüfungen erfolgreich. Verbindlicher aktueller
Index mit vollständigem Sourcehash, ausgewähltem Paketartefakt, Laufnummern und
Abnahmegrenzen: [delivery-candidate.md](delivery-candidate.md),
[build-native.json](build-native.json), [canonical-package-final.json](canonical-package-final.json).
Die älteren Zahlen/Quellstände in den folgenden Abschnitten bleiben historische
Checkpoints; sie ersetzen diesen aktuellen Bericht nicht. Die reale finale Stunde
und fehlenden externen Freigaben bleiben separat ausgewiesen.

Erstellt 2026-10-29T22:50:59Z, gemessene UTC; ausführende Sitzung als Selbstprüfer.
**KEINE PRODUKTFREIGABE.** Bewertung durch Projektverantwortlichen/benannten Reviewer
steht aus. T110 bleibt offen. Technischer Kandidat 7489450 / Sourcehash
`dba419f35bf40fd7282a5b2f3bfe7c685576e2c62ceb570d5fec2d0f4f84498b`.
Finale native Rohbelege werden unten ergänzt; ältere Checkpoints ausdrücklich getrennt.

Gemeinsame Befehlsbasis: `scripts/Invoke-Quality.ps1 -Platform linux|windows`,
`dotnet tool run codemetr-csharp -- check --format json`,
`eng/PackageSmoke.ps1`, `eng/Invoke-ValidationScenarios.ps1`,
separater Hostaufruf `--scenario soak` und `eng/Test-SoakProof.ps1`.
Framework-dependent Publish RID linux-x64 beziehungsweise win-x64.
Plattformen: Ubuntu24.04 und WindowsServer2025 nativ; Windows11 25H2 **NOT RUN**.
Lokales Debian ist ergänzender Linuxbeleg. Konkrete Testnamen und Red/Greenzyklen
stehen in acceptance-audit.md und tdd-us1/2/3/4/5.md.

„Technisch PASS“ gilt für den konkret getesteten Vertrag, nicht für fehlende
Herstellerkonten/Hosts oder ein abschließendes Eigentümerurteil.

| Anforderung | Implementierung / Nachweis | Urteil und Grenze |
| --- | --- | --- |
| FR-001 | Sechs lokale NuGets, fremder PackageReference-Consumer; packages.md/package-consumer.md | Technisch PASS, kein Publish |
| FR-002 | ParallelSessionsTests, zwei native HTTPverträge in einer Instanz | Kontrolliert PASS, Live T105 offen |
| FR-003 | ProviderConfigurationTests, ModelAvailabilitytests, gefrorene Einstellungen | Technisch PASS |
| FR-004 | Öffentliche Konfiguration/Katalog, PageQuery-/SqlitePageTests | Technisch PASS |
| FR-005 | SessionOpening/Lifecycle/ClosureRuntimeTests | Technisch PASS |
| FR-006 | Routing-/100-Request-Zuordnung, persistierte Session-/Request-IDs | Technisch PASS |
| FR-007 | NativeContext-/Binding-/Continuationtests, nur neue Eingabe | Kontrolliert PASS, Live T105 offen |
| FR-008 | Scheduling/Admission, unabhängige Überlappung/Slots | Technisch PASS |
| FR-009 | RequestState-/UncertainOutcome-/PersistenceTests | Technisch PASS |
| FR-010 | RequestCancellation/NativeCancellation/LifecyclePersistenceTests | Technisch PASS, native externe Enden nur nach Beleg |
| FR-011 | FailureIsolation/ProviderFailurePolicy/PersistenceTests | Technisch PASS |
| FR-012 | Unknown-/Reservation-/Crash-/Callbacktests, keine POSTretrys | Technisch PASS |
| FR-013 | RuntimeLifecycle/StartupDispose/StopCallbackDeadlineTests | Technisch PASS |
| FR-014 | Native Quality/Publish auf Ubuntu/Server | Teilbeleg; Win11 fehlt |
| FR-015 | DiagnosticsPrivacy/Metrics/Gauge-/RuntimeStatisticsTests | Technisch PASS; keine Inhalte geloggt |
| FR-016 | Workspace-/Scheduling-/Unknownreservationtests | Technisch PASS im gemeinsamen Instanztrustscope |
| FR-017 | README, embedding, operations, quickstart | Dokumentation geprüft; SC006 Fremdprüfung fehlt |
| FR-018 | PersistenceRecovery/CrashBoundary/StartupNativeAvailabilityTests | Technisch PASS auf verfügbaren nativen Hosts |
| FR-019 | AcceptancePersistence/Failure/DiskFull-/Graphconsistencytests | Technisch PASS |
| FR-020 | RecoveryDecision/Integrity/OperationRecovery/Closuretests | Technisch PASS; keine automatische Unknownfreigabe |
| FR-021 | Retention-/LocalDeletiontests, atomare Graphlöschung | Technisch PASS; keine physische/Provider-/Backuplöschzusage |
| FR-022 | OpenAI/Gemini sowie hosteigener dritter Adapter | Kontrolliert PASS; Live T105 fehlt |
| FR-023 | Common ohne native Referenzen, echte getrennte native Leases | Technisch PASS, komplette Zielmatrix offen |
| FR-024 | NativeContext/Operation-/Callbacktests, kein Agentloop | Kontrolliert PASS |
| FR-025 | Keine Kürzung/Compaction; OpenAI/generischer Kontextfehler geprüft | Teilbeleg; eindeutiger Gemini-Kontextfehler T034 offen |
| FR-026 | InstanceIsolationTests und zwei getrennte Stores/Registrierungen | Technisch PASS; Hostauth ausdrücklich Hostpflicht |
| FR-027 | Öffentlicher fremder Adapter im NuGetconsumer, SC011 | Technisch PASS der Einbettung; keine SharpClaw-Migration |

| Erfolgskriterium | Nachweis | Urteil |
| --- | --- | --- |
| SC-001 | Zehn Sitzungen/zwei Anfragen, kontrollierte Überlappung, CLI parallel | Technisch PASS mit kontrollierten Verträgen |
| SC-002 | 100 Anfragen/zehn Sitzungen, Originalzuordnung/FIFO, CLI routing | Technisch PASS |
| SC-003 | Fünf Fehler/fünf unabhängige Erfolge, CLI failure-isolation | Technisch PASS |
| SC-004 | Monotone Cancel-/Timeout-/Stopbeobachtung, LifecyclePersistenceTests | Technisch PASS unter dokumentierten Voraussetzungen |
| SC-005 | Zwei aktive/drei wartende/fünf abgewiesene parallele Zulassungen | Technisch PASS |
| SC-006 | Unabhängige Person <=30Minuten | NOT RUN, T106 |
| SC-007 | Drei native Zielsysteme plus Stunde je System | Teilbelege; Win11 fehlt, finale Stunde separat |
| SC-008 | Zwei native Verträge, dritter Adapter, keine Kernsonderfälle | Kontrolliert PASS; Live bleibt Q04 |
| SC-009 | Zehn echte Prozesskills, AqueueFIFO/BUnknown ohne Replay | Technisch PASS auf Ubuntu/Server |
| SC-010 | Je zwei Folgeanfragen, Binding-/Headfortschritt, Kontextfehler ohne Replay | Teilbeleg; Gemini-Kontextklassifikation T034 offen |
| SC-011 | NuGetconsumer mit eigenem Provider, zwei Sessions/fünf Requests/Reopen | Technisch PASS auf beiden verfügbaren nativen Hosts |

| Gate | Urteil |
| --- | --- |
| Q-01 | Lokaler Fullguard PASS,440Dateien/458Typen/0Befunde; native finale Bestätigung ci.md |
| Q-02 | Technische lokale Prüfung PASS97,5796 %; finale native Prüfung build-audit.md, Lizenz/DEP-001-Entscheidung offen |
| Q-03 | Ubuntu/Serverbelege vorhanden; vollständige Win11-/Stundenmatrix fehlt |
| Q-04 | NOT RUN, ausdrückliche Kosten-/Speicher-/Kontofreigabe fehlt |
| Q-05 | Technischer Paket-/Adapterkonsum PASS; unabhängiger Integrator fehlt |
| Q-06 | Audit ausgeführt, historische volle Sourcearchivlücke T123 bleibt offen |

Jeder offene Punkt ist im [konkreten Folgeplan](remaining-acceptance.md) mit Owner,
begrenztem Auftrag, nächsten Schritten, Abhängigkeiten und überprüfbarer Abnahme
zugeordnet. Das delegiert keine P0/P1-Abnahme weg. Es gibt keine genehmigte
befristete Ausnahme und keine stillschweigend akzeptierte Grenze. Die vorhandene
Constitution-Datumsfrage bleibt als ursprüngliches TODO sichtbar.

Nachabgleich 2026-10-29T22:55:03Z: SC009/Q03 haben zusätzlich den offenen
intermittierenden Windows-Besitzbefund T125. Ein erfolgreicher späterer Lauf
beseitigt nicht den fehlenden Ursachen-/Regressionsnachweis; review.md nennt
fehlgeschlagenen und erfolgreichen Lauf mit identischem Sourcehash.


## Aktueller Gateabgleich nach T125/T126

Messquelle build-native.json, geprüft2026-10-29T23:52:54.0798815Z;
Codecommitfb1a167 / vollständiger Sourcehash in delivery-candidate.md.
FR-/SC-Matrixzeilen wurden gegen diesen tatsächlichen nativen Stand erneut
abgeglichen. Q01 technisch PASS442Dateien/459Typen; Q02 technische Build-/Coverage-/
Auditprüfungen PASS97,5796/97,5325 %, Distributionsentscheidung weiter offen.
Q03 hat jetzt behobene T125/T126 samt nativer Gegenprüfung; fehlendes Win11 und
vollständige Dreisystemstunde bleiben eigenständig offen. Q04 bleibt NOT RUN,
Q05 hat gleichen kanonischen Paketstand auf beiden OS, fremder Integrator fehlt.
Q06 bleibt wegen ursprünglicher voller Redquellarchivlücke offen. Keine genehmigte
Ausnahme oder Produktfreigabe. Aktueller ausgewählter Sechserarchivsatz ausschließlich
aus quality40974541005; spätere Belegcommits wählen keine anderen Paketbytes aus.
