# Einzelabgleich implementierter Aufgaben

Gemessen 2026-10-29T22:03:25Z. Ausdrückliche Selbstprüfung der ausführenden
Gesamtimplementierung. Kandidat:451ce8c, Sourcehash
95780bfa75c745cd0adb804e0a1ac3dfcea6474169900c8ab402990248e40f5b.
Lokales vollständiges Tor2ffa6348a39647bda7047e699f77377e:1036Tests,97.5796 %;
Fullguard440Dateien/458Typen ohne Befund. Native Neubestätigung läuft.

Checkboxen bestätigen die benannten Implementierungs-/Berichtsaufgaben. Sie ersetzen
keine fehlenden Zielsystem-/Live-/Integrator-/historischen TDD-Freigaben. Tatsächlich
bereits grüne Zusatzregressionen bleiben als solche dokumentiert, nie rückwirkend Red.
Frühere offene Teilschnitte in den chronologischen TDD-Berichten bleiben historische
Belege; die folgenden späteren Implementierungen lösen ihre dortigen Folgeaufträge.

## US2: Sitzungen, native Adapter, Auswahl

| Aufgaben | Aktuelle Umsetzung und konkrete Tests | Beleg |
| --- | --- | --- |
| T031/T037 | SessionLifecycleTests: Settingsrevision/UTC/Opening; SessionOpeningTests: Identität vor I/O/Unknown; SessionClosureTests/SessionClosureRuntimeTests: Idle/Queued/Running/Caller-Cancel/Closed; PageQueryTests/SqlitePageTests: begrenzter Katalog/History | tdd-us2.md: Metadaten; tdd-us3.md: acht tatsächliche Close-Rotfälle und spätere Closure nach Callerabbruch |
| T040 | OpenAi/GeminiOperation, gemeinsamer NativeHttpTransport/ReadRetry und ObservationAccess: endliche Header-/Gesamt-/Poll-/Cancelpfade, sichere GET-Retries, kein POSTretry, Redirectschutz, durable Callback vor Fortsetzung | Native Send-/Observation-/TransportBoundary-/ReadRetrytests, chronologische Adapterzyklen tdd-us2.md |
| T042 | ProviderReplyPolicy/DispatchReplyPolicy plus native Parser: vollständiger finaler Text, inklusive UTF8-Grenze, Überlauf ohne gekürzten Erfolg, echte Gewissheit; strikte Zeichenintegrität nach T119 | ProviderReplyBoundaryTests, OpenAi/GeminiResultTests, Utf8IntegrityTests/Utf8PersistenceIntegrityTests, CompletionPersistenceTests |
| T043 | SessionBindingPolicy und nativer Reopen/operation-aware Recovery: Versions-/Identitätsprüfung, bekannte unbrauchbare Fortsetzung blockiert; Startup prüft identisches Binding und optional Modell-/Credentialzugang vor Workerfreigabe | BindingAvailabilityTests, Continuation-/OperationRecoverytests, StartupNativeAvailabilityTests, ModelAvailabilitytests; T121 native Folgeabnahme bleibt separat |
| T044 | Zehn US2-Kriterien über öffentliche Facetten zusammengesetzt geprüft: Identität, erste/Folgeanfrage, unbekannte Auswahl, Closed-Ablehnung, kompatibler Endpoint, dritter Vertrag, getrennte Profile/Settings bei gleichem Modell, kein Replay, Kontextfehler ohne Inhaltsänderung, passive native Compaction | SessionRouting/Capability/ProviderConfiguration/Contexttests; ProviderExtensibilityTests; SessionClosureRuntimeTests; NativeContextTests; CompletionPersistenceTests.PreservesExistingHistoryWhenProviderContextFails |
| T045 | Native JSON-/Bindingregeln bleiben pro Protokoll, tatsächlich gleiche Credential-/HTTP-/Deadline-/Body-/ReadRetryregeln sind gemeinsam; keine zusätzliche Agent-/Kontextschicht. Aktueller Code-/Regressions-/Guardabgleich verlangt keinen weiteren kosmetischen Umbau | Gemeinsamer HTTPschnitt und kontrollierte grüne Refactorregression tdd-us2.md, vollständiges aktuelles Tor |
| T047 | Vorhandene Red-/Green-/Sourcehashzyklen und aktuelle Ergänzungen vollständig im US2-Bericht zusammengeführt; synthetische native HTTPfixtures und hosteigener dritter Vertrag klar von Live getrennt | tdd-us2.md, tdd-foundation.md; Q04 bleibt T105 |

T034 bleibt offen für die präzise native Gemini-Kontextfehlerzuordnung. Der generische
Kern und OpenAI behandeln ProviderContextLimit konkret; Gemini weist unbekannte Fehler
sicher als ExecutionFailed ab. Die am2026-10-29 erneut geprüfte offizielle
[Interactions-Fehlerreferenz](https://ai.google.dev/gemini-api/docs/api-errors) benennt
keinen eindeutigen Kontextgrenzencode. Keine erfundene Zuordnung aus raw message oder
mehrdeutigem invalid_request/out_of_range. Owner aktuelle Implementierung; nächster
Schritt nach T105-Freigabe: bereinigten tatsächlichen code/native Primärbeleg erfassen,
eindeutigen Fall zuerst rot absichern oder explizite Eigentümerentscheidung zur
akzeptierten Adaptergrenze einholen. Abnahme: zuordenbare unveränderte Originalanfrage,
kein Replay/Kürzen/Compaction und nachvollziehbare öffentliche Kategorie. Q04/T034
werden bis dahin nicht als erledigt ausgegeben.

## US5: Persistenz und konservative Wiederaufnahme

| Aufgaben | Aktuelle Umsetzung und konkrete Tests | Beleg |
| --- | --- | --- |
| T049/T056 | Explizites CreateNew/OpenExisting, SchemaV1/Metadaten/Trustscope/Bindingversion, Readonly-/Hotjournalpreflight vor Mutation, kein fehlender Ersatzstore oder Migration | StoreFormatTests, Session-/Request-/Dispatch-/Case-/DecisionStorageConsistencytests, 28 ursprüngliche Format-Rotfälle in tdd-us5.md |
| T050 | Receipt nach atomarem Input/ID/Sequence/Queuecommit; echter Konkurrenz-/Rollback-/Ackverlust, vor/nach Commit und Wecksignal wirklich gekillte Prozesse | AcceptancePersistenceTests/AcceptanceFailureTests/CrashBoundaryTests, tdd-us5.md Annahmezyklen/Crashbarrieren |
| T051 | Persistierter ReservedDispatch vor Send; acknowledged Observation/Operations-ID, Abschluss/History/Binding/Freigabe atomar, CAS ohne Teilcommit und kein SQLlock über Provider-I/O | ReservationPersistenceTests, DispatchCallbackTests, CompletionPersistenceTests, ReservedSnapshotIntegrityTests, DispatchStorageConsistencyTests, CrashBoundaryTests |
| T052 | Opening ohne Dummyrequest; lesbare Targets/Revision, rein lesender Inspect, konkurrierender CAS/DecisionId, Closing hält Unknown bis bestätigter Klärung | RecoveryDecisionTests/RecoveryPersistenceTests/RecoveryIntegrityTests und Common RecoveryCase-/OperationRecoverytests; zwölf SQL-Recovery-Rotfälle plus spätere Graphregressionen |
| T053 | Ein lokales Result, Historyordinal/Fremdschlüssel/UTC/Gewissheit, UTF8-inklusive/-überschritten, frühere Inhalte unverändert, kein gekürzter Erfolg | CompletionPersistenceTests, ResultStorageConsistencyTests, TerminalRequestStorageConsistencyTests, Utf8PersistenceIntegrityTests, SerializationCultureTests |
| T054/T065 | Explizite Retention nur Closed ohne Konflikte; Readonly/echter voller Datenträger/BUSY/Queue-/Commitfehler, korruptes Schema und kompletter persistierter Graph werden sicher behandelt | RetentionTests, ReadOnlyStorageTests, DiskFullStorageTests, StorageFailureTests, sämtliche StorageConsistencygruppen; tdd-us5.md echte Diskfull-/Journalbelege |
| T055/T066 | Zehn echte SC009-Kills mit exakten A/B-Originaldaten, sieben feine Übergabe-/Commitpunkte, erhaltene Unknownreservation und null B-Replay; Registrierung/Binding/Modellzugang vor Start blockiert nur Betroffene | PersistenceRecoveryTests, CrashBoundaryTests, RecoveryBootstrapTests, StartupAvailabilityTests/StartupNativeAvailabilityTests; Scope der nativen Zielsystemmatrix bleibt T093 |
| T068 | SqliteSessionStore bleibt schmaler Domainport, einzelne Transaktionen besitzen atomare Verantwortung, ein begrenzter Queueworker allein besitzt Verbindung; reine gemeinsame Zustands-/Binding-/Retentionregeln statt Kopien | Tatsächlich grüne Regression vor bisherigen Zerlegungen dokumentiert in tdd-us5.md; aktueller Fullguard/gesamte Crash-/Persistenzsuite |
| T069 | Chronologische TDD-, Kultur-, Retention- und exakte SC009-Nachweise liegen vor; keine Rückdatierung der sofort grünen Prozessregressionen | tdd-us5.md und recovery.md; vollständiges Q03 bleibt offen |

Die einzelne Windows-SC009-Wächterverletzung in40960641218 ist mit unveränderten
Fristen durch Probenisolation/letzte Zustandsdiagnose nachgezogen. Native Bestätigung
gehört weiter T099/T104. Ein lokaler Aufgabenabschluss behauptet keinen verschwundenen
CI-Fehler und keinen finalen identischen Plattformnachweis.

## US3: Zustände, Abbruch, Shutdown und Diagnose

| Aufgaben | Aktuelle Umsetzung und konkrete Tests | Beleg |
| --- | --- | --- |
| T070/T075 | Gemeinsame gültige Zustands-/Gewissheitsmatrix und einmalige SQLfinalisierung, Cancel/Completionrace, Unknown behält Slot, späte Antwort kein Erfolg | RequestStateTests/RequestStatePersistenceTests/UncertainOutcomeTests, neun ursprüngliche Unit-Rotfälle und ein SQL-Rotfall in tdd-us3.md |
| T071/T076 | Admission ab Submit inklusive unbekanntem Commit; Dispatchfrist startet erst beim Send, Queue ohne TTL; TimeProvider und kontrollierte Timer, unkooperativer Send lokal begrenzt | CancellationDeadlineTests, DispatchDeadlineTests, ControlledTimerTests, AdmissionOutcomeContractTests, BeforeSendCancellationTests; echte Redzyklen tdd-us3.md |
| T072/T078 | Start/Stop/Dispose, Annahmesperre ab Stop, atomarer Queueabschluss, dauerhaftes ConfirmedEnded oder Unknown, Storefehler sichtbar, Start/Dispose-Race und blockierende CTcallbacks begrenzt | RuntimeLifecycleTests, RuntimeStartup/Ownership/CleanupTests, StartupDisposeTests, StopCallbackDeadlineTests, RecoveryStopOwnershipTests; tatsächliche jüngste Redfälle |
| T073/T080 | Erlaubte bereinigte Kategorien, fünf bestätigte Fehler/fünf Erfolge, volle Unknownslots halten weitere Arbeit Queued, unabhängige Kapazität ohne Herstellerwechsel/Retry | FailureIsolationTests, ProviderFailurePolicyTests, ProviderFailurePersistenceTests;16 tatsächliche Filter-Rotfälle und ausdrücklich bereits grüne Lastregressionen |
| T074/T081 | Contentfreie korrelierte Logs/Activities, Annahme-/Ausführungsdauer, Ergebniszähler, Queue/Occupied/Unknown-Gauges, endliche Labels ohne lokale IDs, Fehlerisolation/Cachewithdrawal | DiagnosticsPrivacyTests/DiagnosticsMetricsTests/DiagnosticsGaugeTests/RuntimeDiagnosticsTests/RuntimeStatisticsMonitorTests, RuntimeStatisticsTests; gesonderte Redzyklen tdd-us3.md |
| T077 | Queued Cancelled/NotStarted ohne Send; Running speichert Absicht, begrenzter Monitor/cancel/inspect und bestätigtes Ende oder Unknown; Cancelantwort allein kein Beweis | RequestCancellationTests, NativeCancellationTests, CancelContinuationTests, LifecyclePersistenceTests; Running-/Bestätigungs-Rotzyklen |
| T079 | Closing sperrt neue Annahme, queued atomar abgebrochen, native Endbelege oder RecoveryRequired; Closed erst konfliktfrei, auch nach verlassenem Callerwait | SessionClosureTests/SessionClosureRuntimeTests, LocalDeletionContractTests, gemeinsame Closurepolicy; acht ursprüngliche Close-Rotfälle |
| T082 | Reale SQLitepersistenz für bestätigten/unbestätigten Cancel/Timeout/Stop, Reopen und monotone Beobachtbarkeit | LifecyclePersistenceTests/RuntimeLifecycleTests/UncertainOutcomeTests/SessionClosureRuntimeTests; gemessene originale Fristen in ihren Logs, keine Testbudgetanhebung |
| T083/T085 | RequestCancellation bleibt Intentport, Dispatcher verantwortet tatsächliche Endbeobachtung, Lifecycle besitzt Worker-/Store-/Leasegrenzen, Shutdown drainiert Operationen/Callbacks; Nachweise zusammengeführt | Architekturen/Red-/Green-/Refactorregressionen tdd-us3.md, alle aktuellen Suiten und Fullguard |

Diagnoseumfang: bestätigte Annahme und bestätigte beobachtete Dispatchabschlüsse.
Direkte Storecancels und historische Ergebnisinventur erzeugen keine erfundenen
Dispatchereignisse. Kein Task verlangt separate Start-/Stop-/Sessionereignisse an
jedem Lebenszykluspunkt; der frühere offene Vermerk hierzu war kein zusätzlicher
Produktauftrag. Die Betriebsdokumentation benennt diesen tatsächlichen Umfang.

## US4: Konsum und klare externe Grenzen

| Aufgaben | Aktueller Nachweis | Grenze |
| --- | --- | --- |
| T089/T094 | Eigener öffentlicher Consumer, zwei überlappende erste/Folgeanfragen, Status/Cancel/Stop; Argumente/StateRoot/Exitcodes und17erkannte Szenarionamen,14endliche Implementierungen plus echter Soak und konservative Liveblockade | EmbeddedConsumerTests/ValidationHostContractTests/ValidationStateRootTests/ValidationScenarioTests; Liveausführung bleibt ausdrücklich T105 |
| T098 | Native Lease-/Security-/Identityverantwortlichkeiten und ScenarioRunner-Routing am aktuellen Code geprüft; ABIwerte/Modi und Pfadmarker benannt, keine weitere kosmetische Schicht begründet | Fullguard einschließlich beider nativer Quellbestände; tatsächliche Zielsystemabnahme bleibt T099 |
| T101 | Common ohne Plattform, Instanztrennung, öffentlicher Source-/NuGetconsumer und SC011 zusammengeführt | tdd-us4.md/packages.md/package-consumer.md; fremder Integrator/Win11 bleiben offen; keine SharpClawmigration |
| T122 | Echter dup-Rotfall, explizites LOCK_UN, idempotentes Dispose schützt Nachfolger; tatsächliche native Gesamt-CI9554d22 besteht beidseitig40959980657, Guard40959980422 | Quellen und Belege tdd-us4.md; finale Sourcekohorte weiterhin separat |

T087/T091/T093/T099/T100 bleiben wegen vollständiger Zielsystem-/Stundenmatrix offen.
T088/T092 behalten die ausdrücklich ungeprüfte native Mount-/Netzwerkfähigkeitsmatrix;
Eigentümer-/Modus-/Symlink-/Hardlink-/Prozess-/Killfälle sind tatsächlich geprüft.
Ein fehlender Zielhost oder echter Netzwerkfilesystem-Test wird nicht simuliert.
T095/T096 werden für den endgültigen Quellstand im nativen Paket-/Consumerlauf erneuert.
