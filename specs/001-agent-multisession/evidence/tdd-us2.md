# US2 — vorbereitender Ausgabegrenzennachweis

**Stand 2026-10-28, keine abgeschlossene Story oder Produktfreigabe.**
Bezug FR-021, Teilfälle T034/T042. Keine vollständigen HTTP-/Providerverträge aus
T032–T035, kein nativer Adapter, kein Dispatcher und keine Liveabnahme.
Vorabanalyse: `architecture/2026-10-28-provider-output-boundary.md`.

## Tatsächliche Red–Green-Reihenfolge

1. `ProviderReplyPolicy.NormalizeOutput` als verhaltenslose Passthrough-Signatur
   angelegt; neun ausführbare Assertions in `ProviderReplyBoundaryTests` geschrieben.
2. Tatsächlich ausgeführt:
   `dotnet test tests/Monstertacle.Multisession.ContractTests/Monstertacle.Multisession.ContractTests.csproj -c Release --no-restore --filter FullyQualifiedName~ProviderReplyBoundaryTests --logger trx --results-directory artifacts/tdd/2026-10-28-output-boundary-red`.
   **9 entdeckt, 9 fachlich fehlgeschlagen, Exit 1**. Übergroße/leere/unvollständige
   Erfolgsantwort blieb Succeeded statt Failed; expliziter Limitfehler behielt Preview;
   ungültiges Limit behielt ProviderUnavailable statt InvalidConfiguration.
3. Erst danach gemeinsame `Utf8MessageLimits.ValidateOutput` verwendet und diese
   Abweisungen implementiert. Keine neue Bytezählung, keine Zustandsmaschine.
4. Green mit demselben Testprojekt/Befehlsoptionen, ohne Filter, Ergebnisablage
   `artifacts/tdd/2026-10-28-output-boundary-green`: **10/10 bestanden, Exit 0**
   (neun Vertragsfälle plus ein Infrastrukturtest).
5. Sieben zusätzliche **bereits grüne Regressionen**, nicht als Red ausgeben:
   exakte Emoji-/Mehrbyte-/Whitespacegrenze, unverändertes Binding, legitime
   ContextLimit-/Cancel-/Timeoutfehler ohne Text und erhaltene Fehlerkorrelation.
   `artifacts/tdd/2026-10-28-output-boundary-regression`: **17/17 bestanden, Exit 0**.

Alle drei genannten Verzeichnisse enthalten TRX und vor dem jeweiligen Lauf ermittelte
`sources.json` mit SHA256 der Produkt-/Testdateien; Red und Green zusätzlich gemeinsame
Byteprüfung und Vorabanalyse. Keine erfundene Git-Commit-ID. Die früher dokumentierte
initiale Foundation-Manifestlücke wird dadurch nicht nachträglich geschlossen.

## Selbstprüfung und Grenzen

Reine Ausgabeprüfung ohne I/O, Herstellernamen, Modellkatalog, Ersatztext oder Replay.
Originale immutable DTOs bleiben unverändert; keine Historienbearbeitung, keine native
Bindingfortschreibung und keine Reservierungsfreigabe. Zu großer Text wird leer/false,
Gewissheit bleibt unverändert. Unvollständiger Erfolg wird ExecutionFailed, nicht ohne
Beleg OutputLimitExceeded. Vollständiger Text wird weder getrimmt noch normalisiert.
Andere Zustands-/Certainty-Kombinationen bleiben Aufgabe der späteren Zustandsmaschine.
Die Policy ist derzeit nur direkt über den öffentlichen Kernvertrag getestet; die
produktive Pipeline existiert noch nicht und ist nicht durch diesen Test belegt.

Vollständige Windowsregression:
`pwsh -NoProfile -File scripts/Invoke-Quality.ps1 -Platform windows`:
129 Unit-, 17 Vertrags-, je ein Integrations-/Windows-Infrastrukturtest bestanden,
gesamt **148 .NET-Tests**, keine übersprungenen Tests; 11 Coverage-Gate-Smokes bestanden.
Locked-Restore, Release-Build (0 Warnungen/Fehler), Format und Audit bestanden.
Separater Full-Check `dotnet tool run codemetr-csharp -- check --format json`:
**65 Dateien/Typen**, 0 Verstöße/Parsefehler/übersprungene Typen, **Exit 0**;
Tool 0.1.0 und unveränderte Standardregeln, keine Baseline/Ausschlüsse.

Geprüfter Sourcehash: `b4f7b2df75e280b83e64ea110eafc1ccf47af1f630cfad555bbf91f0bfdcb611`.
Qualitätsartefakte: `artifacts/quality/windows/ad8b75557062446d98187fa301b7fbfd/`.
Coverage **203/235 = 86,3830 %**, daher vollständiger Runner **FAIL / Exit 1**.
Keine Freigabe; Linux-/CI-/Provider-/Paket-/Zielhostabnahmen bleiben offen.

## T115: begrenzte Listen und Cursor (2026-10-29)

Owner: ausführende Implementierungssitzung; Branch `feature/reserved-dispatch-contract`,
Basis `f36bda7`. Vorabanalyse: `architecture/2026-10-29-paging.md`.
Common-Cursor und kontrollierte Projektionen lokal umgesetzt; keine produktive SQLitequery.

Testbefehle über run-capped:
`dotnet test tests/Monstertacle.Multisession.UnitTests/Monstertacle.Multisession.UnitTests.csproj -c Release --no-restore --filter <Filter> --logger trx --results-directory artifacts/tdd/<Ordner>`.
Source-Manifeste sources.sha256 vor den Läufen erfasst, TRX unverändert lokal/ignoriert.

| Zyklus | Tatsächliches Red | Green |
| --- | --- | --- |
| PageCursorTests/PageQueryTests | `paging-red`: 25 fachliche Fehler, 2 bereits grüne gültige Größen; fehlende Kodierung/Validierung, List-/History-/Requeststubs | `paging-green`: 27/27 |
| UsesHostClockForSessionCreationTimestamp | `paging-clock-red`: erwartet Unixepoch plus 7 s, tatsächlich Defaultdatum; Clockkontrakt noch Stub | `paging-clock-green`: gesamte damalige Unit-Suite 223/223 |
| RejectsInvalidDecodedCursor | `paging-required-red`: 1 Fehler/8 Regressionen grün, fehlende Listenart wurde als Enumstandard Sessions angenommen | `paging-final-green`: 225 Unitfälle inklusive weiterer bereits grüner Recoverypagingregression; Kind ist explizites JSON-Pflichtfeld |

Manifest-SHA256 in Reihenfolge Red/Green der drei Zeilen:
`6a53a9e027655b28e91d1c2a84ee80724a53672bea6df599090340631ede9e25`,
`e0030291ae71bf202b270661306be911d2d535137a4ccd0b138c4b6185db8a1f`,
`a996fedcac3f6219b3ea30bcb5acafb5ca4f4af35fbc4a7fe82e045bca5079c0`,
`1fbde92cb5e1eacd7b823eeec1afb1a8398735bd78821b109940c1b0f506d597`,
`f5a64602dbad9c20a645eb9625a8c2eb8f11f82e24d202e164beaa7da6aa935d`,
`8e04ad7df42526a3824d27be2ef4262ae527e6bebf77c88d8f6d12348d3fb789`.

PageLimits und strikter PageCursor werden von öffentlichen SessionQueries/RequestQueries
und den kontrollierten Storeprojektionen verwendet. Cursor prüft Version, vollständigen
Schlüssel, UTC, Store-/List-/Sessionbindung, doppelte/unbekannte JSON-Felder, Base64url und
2048-Byte-Grenze. Größe 1–100 bleibt explizit; kein stilles Kappen. Geprüfte Livesicht:
Löschen des letzten Schlüssels bleibt fortsetzbar, frühere neue Datensätze erst bei neuem
Scan, spätere neue Datensätze auf Folgeseiten. Alte DTOs bleiben unveränderte Records.

HistoryEntry trägt nun HistoryOrdinal/HistoryId, SessionSnapshot CreatedUtc. Der Konstruktor
bleibt quellkompatibel, Recordgleichheit schließt Metadaten ein. Eine vorhandene T112-
Regression erwartete noch beide Defaultwerte und scheiterte deshalb nach der gewollten
Metadatenerweiterung. Der erwartete vollständige Record wurde um Ordinal 1 und nachgewiesene
nicht leere HistoryId ergänzt, keine Felder aus Gleichheit/Prüfung entfernt. Zweiter neuer
Historyeintrag beweist höhere Ordinalzahl und verschiedene UUID. SessionCatalog erhält
CreationUTC aus TimeProvider. Keine Paket-/Dateiformatmigration, da noch nicht veröffentlicht.

Vollständiger Linuxrunner: Exit 0, Locked-Restore/Release-Build/Format/Analyzer/Audit bestanden;
263 .NET-Tests maschinell aus TRX gezählt (225 Unit, 36 Contract, je 1 Integration/Linux-
Infrastruktur), 0 fehlgeschlagen/nicht ausgeführt; 13 Gateverhaltensfälle. Ein erster
Formatlauf meldete die private Feldbenennung; eine unvollständige Umbenennung verursachte
anschließend einen Compilefehler. Beides vor dem gültigen vollständigen Lauf korrigiert;
kein fachlicher Red-Nachweis und keine unterdrückte Regel.

Gesamtprodukt-Coverage 515/536 = 96,0821 %, Mindestwert 87 % erfüllt.
Sourcehash `0538afec205cece85f73a7dec19e66edf97defe97daa564f2e082c354f86f970`;
Berichte `artifacts/quality/linux/6b78ef22a5224848b60d44e5c419cbbe/`.
Full-Codemetr 0.1.0: 110 Dateien/112 Typen, 0 Verstöße/Parsefehler/übersprungene Typen,
Exit 0. Keine Pin-/Scope-/Baseline-/Schwellenänderung. Debian GNU/Linux 13, SDK 10.0.403,
PowerShell 7.6.9; kein Windows-/SQLite-/native Zielsystem-/Live-/Paketnachweis für diesen Stand.

Befundabgleich: Fehlende Cursor-/Grenzregeln und JSON-Defaultlücke durch genannte Red–Green-
Zyklen geschlossen. Historyerwartung bewusst auf neuen Vertrag aktualisiert und alle
36 Contractfälle grün. Produktive Keyset-SQL/Retention bleiben T063/T064 (Owner US5,
native persistierte Listen-/Löschabnahme); Runtimekomposition T026 (Owner laufende
Gesamtimplementierung, öffentliche Konsumtests). Kein offener lokaler T115-Abnahmeblocker.

## T030/T036: generische Konfiguration, erster Schnitt

Updated: 2026-10-29T07:47:13Z
Basis 99b1ffd; Vorabanalyse architecture/2026-10-29-provider-configuration.md.
`dotnet test tests/Monstertacle.Multisession.UnitTests/Monstertacle.Multisession.UnitTests.csproj -c Release --no-restore --filter FullyQualifiedName~ProviderConfigurationTests`
über run-capped: 14 fachliche Fehler/1 vorhandene Positivregression, danach 15/15 grün.
Red: fehlende typisierte Ablehnung leerer IDs/Adapter/Version/Referenz/Profilwerte sowie
Originalwert durch spätere Hostmutation ersetzt. Duplicate-ArgumentException ist ein
beobachteter falscher öffentlicher Fehlertyp, kein Compiler-/Infrastrukturfehler.
Manifest SHA256 Red `47352b3e26a04034af4675e0639c26ff2b9741754907ff0d92db7231d174b9e3`,
Green `438f76d241f9117049ee5a63427c79740908b552221c674d5a71eff4bbc48805`.

ProviderCatalog validiert Registrierungen/Profile vor Native-I/O, friert Nestedsettings
ordinal ein und bietet readonly Konfigurationslisten. SessionCatalog und Runtime nutzen
nun dieselbe Validierung, keine zwei unterschiedlichen Snapshotregeln. Opaque Modelle
bleiben unverändert; zwei Profile/ein Modell und case-sensitive Kennungen sind grün.
Keine HTTP-Eigenschaft ist für nicht-HTTP-Adapter vorgeschrieben.

Erste Gesamtsuite fand zwei absichtlich früher greifende Validierungen in alten Tests:
fehlende Providerreferenz und undefinierter ExecutionKind scheitern jetzt schon im
Konstruktor. Die alten Capabilitytests prüfen weiterhin null Opens und typed NotStarted;
sie umfassen jetzt Konstruktion plus Create in ihrer Fehlerassertion. Undefinierte Enum
ist InvalidConfiguration, bekannter aber nicht unterstützter Kind bleibt
UnsupportedProviderAction. Neue T030-Fälle fordern ausdrücklich den früheren Zeitpunkt.

Vollständige lokale Qualität Exit 0, artifacts/quality/linux/14f1e69e460b409b83e9067101dfb6de/:
Sourcehash `270ea9b394acda09d8d011da5b1f936cab902ead933a49cd577d6df2a077140f`.
294 Tests maschinell aus vier TRX, 240 Unit/52 Contract/je 1 Infrastrukturfall, 0 Fehler.
697/730 Produktzeilen = 95,4795 Prozent; Locked-Restore, Build/Format/Analyzer/Audit,
13 Coveragegatefälle bestanden. Full-Guard 131 Dateien/133 Typen, 0 Verstöße/Parsefehler/
Skips, Exit 0. Pins und Scope unverändert. Anschließende reine Runtime-Testbarrierekorrektur
an 4d5158e hat 52/52 Contractfälle und Full-Guard erneut bestanden; keine Produktänderung.

T030/T036 bleiben insgesamt offen: native HTTPS-/Loopback-/CredentialReference-Prüfung
und frische Credentialauflösung benötigen T032–T040-Fixtures, Owner dieselbe ausführende
Gesamtimplementierungssitzung. Nächste Schritte: OpenAI-Fixture für Open/Reopen und Send,
dann sichere Transportgrenzen/Credentials/Fehler und Gemini; Abnahme gemäß vorhandenen
T030–T040. SettingsRevision im Sitzungssnapshot bleibt T031/T037. Kein Live-/Freigabenachweis.

## T032/T038: nativer Conversations-/Responses-Adapter

Updated: 2026-10-29T08:07:53Z
Vorabanalyse und inkrementelle Deep-Dives:
architecture/2026-10-29-openai-adapter.md; Basis 0a76b51. Kein vorbestehender Adapter
behauptet. Offizielle Dokumentation tatsächlich abgerufen, Index-/Webgrößenfehler bei
Referenzseiten dokumentiert. Nur enges eigenes Fixtureprotokoll, keine Livezugänge.

Befehlsmuster jeweils über run-capped:
`dotnet test tests/Monstertacle.Multisession.ContractTests/Monstertacle.Multisession.ContractTests.csproj -c Release --no-restore --filter FullyQualifiedName~<Testklasse>`.
Nach Green wurde mit `FullyQualifiedName~OpenAi` die gesamte Adaptersuite wiederholt.

| Zyklus/Testklasse | Beobachtetes fachliches Red | Danach grüne Adapterregression |
| --- | --- | --- |
| OpenAiProviderContractTests | 2 Stubs liefern keine native Conversation/kein Reopen | 2/2 |
| OpenAiConfigurationTests + OpenAiOpeningFailureTests | 25 Fehler, 1 vorher gültiger Slash-Endpoint; ungültige Konfiguration, fehlende stabile Identität und rohe HTTP-/JSONfehler | 28/28 |
| OpenAiSendTests | 3 fehlende Sends/awaitbare Operationsacknowledgements | 31/31 |
| OpenAiResultTests | 11 Fehler, 1 vorher grüne inklusive UTF8grenze; Status/Context/Leertext/Action/ID/Overlimit falsch | 43/43 |
| OpenAiTransportBoundaryTests | 4 Fehler: Header/Gesamtfrist/Callback nicht begrenzt, Rohkörperlimit ignoriert | 47/47 |
| OpenAiObservationTests | 6 Inspect-/Cancel-Stubs liefern keine native Evidenz | 53/53 |
| OpenAiReadRetryTests | 2 fehlende sichere GET-Retry-After-Waits, POST-Retryverbot schon grün | 56/56 |
| OpenAiProtocolSafetyTests | 5 Fehler: HTTP400-Kontextcode, whitespace ID, widersprüchliche Conversation, direkte ungültige Calllimits | 61/61 |

Der erste Send-Testversuch scheiterte an CA1861, ein Retry-Green-Versuch an CS1003;
beides sind ausdrücklich keine fachlichen Reds. Die ausführbaren Redstände tragen
`openai-send-red-compiled` beziehungsweise der erfolgreiche Green `openai-retry-green-compiled`.
Erster Gesamtqualitätversuch beanstandete ausschließlich die private Konstantenbenennung;
nach Umbenennung vollständige Qualität erneut, keine Gateänderung.

Source-Manifeste unter artifacts/tdd/<Zyklus>/sources.sha256 (nicht versionierte Originale).
Folgende Zeiten stammen ausdrücklich von LastWriteTimeUtc dieser unmittelbar vor dem
Test erzeugten Manifestdateien; jede Redfassung liegt vor ihrer Greenfassung:

| Zyklus | UTC der Manifestdatei | SHA256 des Manifests |
| --- | --- | --- |
| openai-open-red | 2026-10-29T07:49:26.7907321Z | 6cb88d684ab64fca71389c904ae0e93f0f564a8c1dee6890c205b268df4fea2f |
| openai-open-green | 2026-10-29T07:49:56.6966144Z | 017b20be0b6e9e61dadab34c7081dc496757319ba862319c0c73b399818d0dd7 |
| openai-validation-red | 2026-10-29T07:50:52.8201458Z | b25539c7c2eab183db17b8cfbdbfb47669ec87778d305eec18b49b68a84bda44 |
| openai-validation-green | 2026-10-29T07:51:37.3309456Z | b53ae0df453bc89b1b12d536e8805af50066ac551f34e32063961099990ea45f |
| openai-send-red-compiled | 2026-10-29T07:53:32.4301825Z | b79fd3e4ddca490fb772455ce03305b51b20f7034db9e315bf82130872d3dfa3 |
| openai-send-green | 2026-10-29T07:54:19.5971470Z | 33d56f2b5c37c3291173b7bac220e59088f92d56a82ee3adb3ed8bc077761e3f |
| openai-result-red | 2026-10-29T07:55:20.5009740Z | 5681e96e079e6d5358cc44cdc3beb6f0b828cebcefaa80d338e4f8ac35d1ed82 |
| openai-result-green | 2026-10-29T07:56:17.9205813Z | 192f5914ee2adce7b63beec9f441278085e5d36ca7e30cd905126b7099d90851 |
| openai-transport-red | 2026-10-29T07:58:16.5760337Z | 18c8eee76ad674a6394247ecf15ba414fd13776817d9db82a4f6c5ecb036694a |
| openai-transport-green | 2026-10-29T07:59:10.1633986Z | 4229024d309e79dcef5cc15f831cde0fa69f0aaea6c49decfc53c84c7506af90 |
| openai-observation-red | 2026-10-29T08:00:00.7145725Z | e391d5251d0211825442fcadaa497fdeb5a7f9552ce069951c73e02a28076a72 |
| openai-observation-green | 2026-10-29T08:00:49.3696269Z | 1b9edc8b3c9ce4f30c9f9b65d24ef56cd56031a0701caf9f209f92ded942a222 |
| openai-retry-red | 2026-10-29T08:01:57.9699331Z | 95d19749f512a0098a2e2f1ea511800b3d514cf786b41d92cc592cf93e0cbbff |
| openai-retry-green-compiled | 2026-10-29T08:03:21.4031696Z | 454f52973078e69c497b054bf966d83fa079ef81359e64c7c806abb52c7e92b0 |
| openai-safety-red | 2026-10-29T08:04:50.2167432Z | ec3a80345fa94e7bf12110e4b5602821ce75e899f6fcb26319e2ff9b9a6c6dde |
| openai-safety-green | 2026-10-29T08:05:39.3678275Z | 1347c7fd26d6159fb93a42c87aaafd668f8feee1c79c06269ec0fa00a6da5366 |

Zusätzliches synthetisches JSONfixture openai-v1.json, SHA256
`e0252d214d806397ddb0e35411d9786d9feb03f41f8b36828b250aee479a4fe7`.
Die Gesamtqualität manifestiert auch JSON- und Projekteinstellungen.

### Implementierung und finale lokale Prüfung

Native Sessionproviderfassade, separater Transport/Körperlimit/Read-Retry/Requestprotokoll/
Responseinterpreter/Operation/Observationzugriff. Nur neue Eingabe, eingefrorene instructions,
Conversation stabil, keine previous_response_id oder eigene Tools. Frische Credentials
je HTTP-Aufruf. OperationId vor Poll/Erfolg awaitbar gesichert, Callbackfehler ist Unknown.
Native terminale Status unterscheiden sich von lokalen Fristen, überlange Ausgabe wird
nicht abgeschnitten. Inspect/Cancel sind read/evidence-only gegenüber lokalem Store.

Kompletter Qualitätseinstieg Exit 0 auf Debian 13:
`artifacts/quality/linux/4bc35df4ca77479bbdee9c78f5790b58/`, Sourcehash
`2d7e5e9a55fcf67782407a71a093b0704055c2a64bf0a089c6e3a3c3cbdc605d`.
355/355 .NET-Tests aus vier TRX maschinell gezählt: 240 Unit, 113 Contract,
je 1 Integration-/Plattform-Infrastrukturfall; 0 Fehler/Skips. 13 Coveragegatefälle,
Locked-Restore, Release-Build, Format/Analyzer und transitiver Audit grün.
912/951 Produktzeilen = 95,8991 Prozent im gesamten nativen Scope. Keine Scope-/Pinänderung.
Full-Guard Exit 0, 155 Dateien/157 Typen, 0 Verstöße/Parsefehler/übersprungene Typen.
Originalbericht artifacts/tdd/openai-codemetr-final.json; keine Baseline/Limitlockerung.

### Befunddisposition und Folgearbeit

| Gegenstand | Disposition, Owner und konkrete Abnahme |
| --- | --- |
| Fehlender Conversations-/Responses-Adapter | T032/T038 lokal umgesetzt, 61 Adapterfälle plus vollständige Qualität oben; reale Herstellerabnahme T105 bleibt offen |
| Endpunkte/ID-/Status-/Frist-/Output-/Callbacklücken der ersten Minimalzyklen | Jeweils obiges fachliches Red vor Green, kein unverifizierter Minimalstand als fertig veröffentlicht |
| Zweiter nativer Vertrag | Ausführende Gesamtimplementierungssitzung, T033/T039: stabiles Gemini /v1, Lazy-ID/Chainhead, neue Nachricht/Settings, native Status-/Fehler-/Cancel-Fixtures |
| Zusätzliche Transport-/Datenschutz-/öffentliche Integrationsabnahme | Dieselbe Sitzung, bestehende T034/T035/T040/T044/T074: echte Loopback-Redirect-/Credentialgrenze und weitere Cancel-/Body-/Failureproben, öffentliche Auswahl mit beiden Adaptern; diese ganzen Tasks bleiben offen |
| Persistenz, vollständiger Lifecycle, native Besitz-/Konsum-/Releaseabnahme | Dieselbe Gesamtimplementierungssitzung, bestehende US5/US3/US4-/Polishtasks mit deren Red-/Crash-/Zielhost-/Freigabekriterien |

Kein Live-, Zielhost-, Packaging- oder Gesamtabnahmepass. OpenAI unterstützt ModelResponse,
keine externe AgentTask-Laufzeit. API-Betrieb braucht hostseitig freigegebenen Zugang und
beim Livegate ausdrücklich bestätigte Kosten-/Speicher-/Testkontobedingungen.
Die Gesamtimplementierung wird mit Gemini fortgesetzt; kein Ende nach diesem Teilschnitt.

## Gemeinsame native HTTP-Infrastruktur vor zweitem Adapter

Updated: 2026-10-29T08:28:01Z
Vorabanalyse architecture/2026-10-29-shared-native-http.md, Basis b795a6d.
Reines Refactoring ab grünem 362-Teststand, kein künstlicher Red. Header-/Body-/GET-Retry- /
Endpointregeln liegen jetzt als interne Compile-Link-Quellen unter src/Shared/NativeHttp.
Noch ausschließlich im OpenAI-Projekt eingebunden. Kein zusätzlicher Paket-/Assemblytyp,
keine HTTPreferenz im Commonkern. Verbindung enthält Headername/Scheme; native HTTP400-
Interpretation bleibt explizit injizierter Providerparser. Native Operationen und Bindings
bleiben in ihren jeweiligen Paketen. Gleiche Poll-/Header-/Bodydefaults zentralisiert.

240 Unit- und 120 Contractfälle vor/nach Refactor grün, darunter alle 61 nativen Adapterfälle.
Manifest SHA256 `31ba5e63f245671c110b3e157a9c3916354d0f1f0a566d0c484e9cdbbfcad97f`.
Vollständige lokale Qualität Exit 0, artifacts/quality/linux/30a8ee20478641c98a41f3d93e06d7fc/,
Sourcehash `5cd543a9825bfc03b9434894c4bebae97b34460ce58a2e452cbc2df4e457d61d`.
362/362 Tests aus vier TRX gezählt, 0 Fehler/Skips; alle 13 Coveragegatefälle und
Locked-Restore/Build/Format/Analyzer/Audit grün. 924/962 Produktzeilen = 96,0499 Prozent.
Voller Guard Exit 0, 163 Dateien/165 Typen, 0 Verstöße/Parsefehler/übersprungene Typen.
Keine Gates/Pin-/Scopeänderung. T045 bleibt bis vollständiger US2-Regression offen.
Gemini wird denselben internen Transport nach eigenem Red konsumieren und seine darin
kompilierten Quellen im normalen Coverageinventar vollständig mitmessen.

## T033/T039: stabile native Interaktionskette

Updated: 2026-10-29T08:51:12Z
Basis fd3e7e3, Vorabanalyse architecture/2026-10-29-gemini-adapter.md.
Offizielle stabile /v1-Referenz tatsächlich gelesen; eigenes Fixture verwendet
steps/model_output/content/text. Keine Übernahme des älteren v1beta-/outputs-Vertrags.

Befehlsmuster jeweils über run-capped:
`dotnet test tests/Monstertacle.Multisession.ContractTests/Monstertacle.Multisession.ContractTests.csproj -c Release --no-restore --filter FullyQualifiedName~<Testklasse>`.
Greenregression jeweils `FullyQualifiedName~Gemini`, danach gesamte Suite.

| Zyklus | Beobachtetes fachliches Red | Danach grüne Gemini-Fälle |
| --- | --- | --- |
| GeminiProviderContractTests | 4 Lazy-/Reopen-/Head-Stubs | 4 |
| GeminiSendTests | 4 fehlende erste/Folgesends und ID-Callbackgrenzen | 8 |
| GeminiResultTests | 11 Fehler/1 vorhandene inklusive Positivgrenze; Status, Textlimit, Head-/Lineage- und erste Fehlausführung | 20 |
| GeminiRecoveryTests | 12 nicht implementierte native operationsgebundene Klärungen | 32 |
| GeminiObservationTests | 6 Inspect-/Cancel-Stubs | 38 |
| GeminiConfigurationTests + GeminiCallValidationTests + TransportBoundary/ReadRetry | 3 Fehler für v1beta und direkte ungültige Calllimits; 22 bereits grüne Konfigurations-/Transport-/Frist-/Retryregressionen | 63 |

Die letzten 22 Fälle sind ausdrücklich Regressionen, keine erfundenen neuen Reds.
Insbesondere die isolierten Gemini-Call-/Callbackfristen wurden nach dem Send-Green
zusätzlich geprüft; die Sendimplementierung hatte die bereits verwendete endliche
Operationsstruktur übernommen. Dieser zeitliche Umfang wird bei T108/Q-06 mitbewertet;
kein nachträglicher Lauf wird als früheres fachliches Red ausgegeben. Die gemeinsamen
Header-/Body-/GET-Retryregeln hatten ihre tatsächlichen Reds vor dem Sharedrefactor.

Unmittelbar vor Test erzeugte Manifeste unter artifacts/tdd/<Zyklus>/sources.sha256;
UTC stammt ausdrücklich aus LastWriteTimeUtc dieser Dateien, nicht aus geschätzten Zeiten:

| Stand | UTC der Manifestdatei | SHA256 |
| --- | --- | --- |
| gemini-open-red | 2026-10-29T08:31:08.7672862Z | 5bf588605a99d6ccb64f81ebeff2c6e9de1e93d42440d96c612e93acf1a8d28a |
| gemini-open-green | 2026-10-29T08:32:18.5396508Z | 3fcdb465cd5d9f46bef3ac115e344773ba5093e1e566fba7d438779815597fde |
| gemini-send-red | 2026-10-29T08:33:50.8214243Z | 1196a4ae167d7377c184a89ff7b78ebb6f926f7793003ce89baae193c6591532 |
| gemini-send-green | 2026-10-29T08:34:42.2366415Z | ac1dd6f3bf6570e480bbbe028ef81eb2be44c847397359749b95d510475e3cda |
| gemini-result-red | 2026-10-29T08:36:11.7062403Z | 3984afc212eff5be1596ef3b4181a6676b172cdf927574218c646aa0c94fb57b |
| gemini-result-green | 2026-10-29T08:37:36.1915278Z | 2ff5929750d6becfd1a1a69824223c222491f53c7110264284620340e1e4fc8c |
| gemini-recovery-red | 2026-10-29T08:39:12.8215759Z | f43e4d4fd5ef0041cde1796a837fc9c40523e44c229c2fdcbe8d50bbfc9bca16 |
| gemini-recovery-green | 2026-10-29T08:40:33.9426536Z | 869defa970f9345864b456606006f22a96dd7c6cbd21b73607fada0ee8f4c938 |
| gemini-observation-red | 2026-10-29T08:42:19.0652342Z | e464c972d17b7ced40fb9e5e0d63e82cb1ed6b0589b403b7e5f4fe1a540bbba6 |
| gemini-observation-green | 2026-10-29T08:42:51.7992835Z | 373b9c74d42a90a51becdcbc803504d3818764e1d518bb596c8ca9a57efde2f1 |
| gemini-validation-red | 2026-10-29T08:44:18.7927296Z | 06f3097909921383f4e9e405e6922ec5cc6d0346c064807ccedee28ec4120e35 |
| gemini-validation-green | 2026-10-29T08:45:30.7452302Z | fbc6fb9e9f3565460ad907641fb396869210958123f09b871740ec18b6933259 |

Zusätzliches eigenes gemini-v1.json-Fixture SHA256
`99dbc9ce0ef62b658e41d72ba06f51e239afb8b4386d59dad9aade68edfacfca`.
Gesamtqualität manifestiert JSON-/Projekteinstellungen zusätzlich zu den C#-Manifesten.

### Implementierung und lokale Gesamtprüfung

Eigene native Facade/Options/Bindingpolicy/Protocol/Response/Operation/Recovery/Observation,
gemeinsamer interner HTTPtransport ohne Herstellerabhängigkeit zwischen Paketen. Root,
letzter completed-Kopf und unbestätigte neue Operation sind getrennt. Erste ID und jede
Operations-ID werden awaitbar bestätigt. Native Settings und Input bleiben unverändert.
Resultgrenzen verwenden die vorhandene ProviderReplyPolicy; kein abgeschnittener Erfolg.
Ein neuer nativer completed-Kopf bleibt auch bei lokaler Textaufnahmefehlerausgabe erhalten.

Vollständige native Qualitätsprüfung lokal Exit 0:
`artifacts/quality/linux/15dee12688c647229cddfa1b39c74b59/`, Sourcehash
`620ed98eb01d039910bb53072c534d72fc4a73361d7fdb759d45d61df35d84d3`.
425/425 .NET-Tests aus vier TRX gezählt, 240 Unit/183 Contract/je 1 Infrastrukturtest,
0 Fehler/Skips. 13 Coveragegatefälle, Locked-Restore, Build, Format/Analyzer, Audit grün.
1158/1213 Produktzeilen = 95,4658 Prozent, gemeinsame HTTPquellen in den jeweiligen
Produktassemblies mitgemessen. Full-Guard Exit 0, 181 Dateien/183 Typen,
0 Verstöße/Parsefehler/übersprungene Typen. Keine Pin-/Grenzwert-/Scopeänderung.

T030/T036 generische plus beide native Konfigurationen lokal umgesetzt (15 generische
Konfigurationsfälle, native HTTPS/Loopback/Credential-/Profil-/Versionsfälle und frische
Auflösung in beiden Adaptern). T033/T039 lokale native Implementierung umgesetzt.
T034/T035/T037/T040–T048 und die übrigen Stories bleiben nach ihrem Gesamtumfang offen.

| Offener Gegenstand | Konkrete Folgearbeit / Owner und Abnahme |
| --- | --- |
| Öffentliche Auswahl/Erweiterung/native Kontextgrenze | Ausführende Gesamtimplementierungssitzung, T035/T041/T044: beide native Adapter plus dritten Hostadapter ohne Kernänderung öffentlich konsumieren; drei geordnete Eingaben, zwei Hersteller überlappen, keine eigene Kontextaktion/Historyänderung |
| Native Fehler-/Transportabnahme | Dieselbe Sitzung, T034/T040/T074: reale Loopback-Redirect-/Credentialgrenze, weitere negative Bodies/Cancelbelege, keine Rohdaten in Fehlern; dokumentierten spezifischen Gemini-Kontextcode oder freigegebenen Beleg beschaffen, keine message-Heuristik |
| Unbrauchbare erste native Bindung | Dieselbe Sitzung, T043/T037: native Sendverweigerung existiert, zusätzlich ProviderUnavailable-Sitzungssperre vor weiterer Annahme/Reservierung und passende Wiederaufnahmeprüfung zuerst rot integrieren |
| TDD-Freigabeumfang | T108/Q-06, Owner ausführende Abschlusssitzung/Projektverantwortlicher: tatsächlich beobachtete Reds und offen dokumentierte zeitliche Grenzen prüfen; keine nachträgliche Rekonstruktion oder stille Ausnahme |
| Persistenz/Lifecycle/Plattform/Live/Konsum | Bestehende US5/US3/US4-/Polishaufträge mit ihren expliziten Abnahmen; keine Fake-, lokale Debian- oder Fixtureprüfung ersetzt diese |

Vorheriger Sharedstand fd3e7e3 auf Windows/Ubuntu-CI grün (quality 40842526292,
Guard 40842526304); T116 b795a6d ebenfalls grün (quality 40841624863, Guard 40841624856).
CI am neuen nativen Stand folgt nach Push. Keine Produkt-/Livefreigabe; Gesamtauftrag läuft weiter.

## T035/T041: öffentliche native und hosteigene Providerintegration

Updated: 2026-10-29T09:10:22Z
Vorabanalyse architecture/2026-10-29-public-provider-integration.md, Basis 057c33c.
NativeContextTests zuerst fachlich rot: Succeeded erwartet, Failed beobachtet, weil ein
passives compaction-Item als Clientaktion galt. Nach enger Parserkorrektur 1/1 grün.
Keine neue Kontextfunktion: opaque Item wird nur toleriert, nicht interpretiert/kopiert
oder neu angefordert. Zwei Sends enthalten weiterhin ausschließlich die neue Eingabe;
kein context_management/tools-Feld und kein eigener Compact-/Historycall.

ProviderExtensibilityTests konsumiert die öffentliche Runtime mit beiden echten nativen
Adapterklassen unter synthetischen unabhängigen Wirezuständen plus ThirdContractProvider.
Der dritte Adapter ist hosteigenes AgentTask-/Workspace-Protokoll mit Bindingversion7,
ohne Endpoint oder Herstellerbedingung im Commonkern. Je drei Eingaben/Session, neun
unverwechselbare RequestIds, FIFO/History/Inhalt/Modell/Root und neuer Head passen.
TCS-Barrieren nach tatsächlichem Eintritt belegen drei laufende Sends gleichzeitig.
Fünf Conversation-HTTPcalls (Open, drei Sends, ein GET), vier Interaction-HTTPcalls;
keine zusätzlichen Kontext-/Tooloperationen, keine Recoveryfälle. Die Drittadapterfähigkeit
war bereits im Kernel vorhanden: dieser neue Integrationsfall wurde unmittelbar grün,
kein künstliches Red oder neue Kernelimplementierung dafür behauptet.

Befehle über run-capped, ContractTests Release/no-restore:
`--filter FullyQualifiedName~NativeContextTests`, danach
`--filter 'FullyQualifiedName~ProviderExtensibilityTests|FullyQualifiedName~NativeContextTests'`.
Unmittelbar vor Test erzeugte C#-/Projektmanifeste, UTC ausdrücklich LastWriteTimeUtc:

| Stand | UTC | SHA256 |
| --- | --- | --- |
| native-context-red | 2026-10-29T09:00:41.5221951Z | d3bd1d1f67b314edb7abcf92934c89963d92ec72d92f2c81a849f185535ae3af |
| native-context-green | 2026-10-29T09:00:54.3549982Z | e947bffed86b5752181aec3a7ef93e50618b70eecf9d885eb5323836b21511b3 |
| provider-integration | 2026-10-29T09:03:36.1251228Z | 3ee514feb70b17cffc0cad50d6cfb68dd99354969eba1cd09f7c3e9e23b1df8c |

Vollständige lokale Qualität Exit 0:
artifacts/quality/linux/9320d94a53a74e409334fabf9afcc32d/, Sourcehash
`dbecff3895907b0b767bd89f1bf42fcf261f2631741005014b5679efafdaa85f`.
427/427 .NET-Tests gezählt, 240 Unit/185 Contract/je 1 Infrastrukturfall, 0 Fehler/Skips.
13 Coveragegatefälle, Locked-Restore/Build/Format/Analyzer/Audit grün.
1158/1213 Produktzeilen = 95,4658 Prozent. Full-Guard Exit 0, 189 Dateien/191 Typen,
0 Verstöße/Parsefehler/übersprungene Typen, kein Scope-/Pin-/Grenzwertwechsel.
Gemini-Stand 057c33c auf Windows/Ubuntu-CI grün (quality 40846039421, Guard 40846039386).

T035/T041 lokal umgesetzt. Providerdokumentation T046 beschreibt beide nativen Verträge,
Opaque-Modelle, Sicherheits-/Speicher-/Kontextgrenzen und offene Herstellerabnahme.
T044 bleibt offen für sämtliche zehn US2-Fälle einschließlich Close; dies ist dessen
native öffentliche Teilregression, kein ersetzter Close-/Cancel-/Paket-/Integratortest.
Bekannt unbrauchbare erste Bindung wird nativ vor Send verweigert; atomare vorgelagerte
Sitzungssperre bleibt konkreter nächster Auftrag T043/T037 der ausführenden Sitzung.
Echte Persistenz/Restart/Cancel/Stop/Rechte-/Liveläufe folgen ihren bestehenden Tasks.

## T117: explizite Fortsetzungsverfügbarkeit

Architektur vor Änderung: architecture/2026-10-29-continuation-availability.md.
BindingAvailabilityTests: vier fachliche Fehler im Red (Admission/Reservation trotz
bekannt fehlender Fortsetzung), drei bereits grüne Kompatibilitäts-/Unknownfälle.
Nach Green 247 Unitfälle erfolgreich. ContinuationEvidenceTests: neun fachliche
Fehler im nativen Red (fehlendes explizites Verfügbarkeitsflag), danach 194 Contractfälle
grün. Nullable ProviderReply.ContinuationAvailable lässt bisherige Produzenten unverändert;
false oder explizite Sessionexpiry sperrt mit ProviderUnavailable, Unknown bleibt
RecoveryRequired und reserviert. Kein Hersteller-/Opaque-Stringzweig im Kern.

Befehle: Release/no-restore dotnet test der UnitTests bzw. ContractTests mit Filter
FullyQualifiedName~BindingAvailabilityTests bzw. ~ContinuationEvidenceTests, anschließend
jeweils ganze Suite; alle schweren Aufrufe über run-capped.
Quellmanifestzeiten sind LastWriteTimeUtc der unmittelbar vor den Tests erzeugten
sources.sha256, nicht geschätzt:

| Stand | UTC | SHA256 |
| --- | --- | --- |
| continuation-red | 2026-10-29T09:18:33.6573052Z | b99d54ff80c4b5701e18785915e4a14c78708316a5afe01773781b2741b35128 |
| continuation-green | 2026-10-29T09:19:09.8995755Z | 778041f5c621f7d87720cac68dce9d54c6b6c86264dbfce0ed6fc3e5fcbf3d77 |
| continuation-native-red | 2026-10-29T09:22:35.5767025Z | bc63231f922ed3c9e92db4ada96dce7d63e6e9267bfd5ca8215c107547e95997 |
| continuation-native-green | 2026-10-29T09:23:34.0044180Z | 8aa6b8ef6780ff299fb6de1b42b35519b12490430ff1fecf1bb762a643d1989f |

ContinuationRuntimeTests ist zusätzliche bereits grüne öffentliche Integration der
zuvor rot belegten Komponenten: gehaltene erste Interaktion, queued Folgeanfrage,
unabhängige Session nutzt zweiten Slot, bekannt fehlgeschlagene erste Interaktion
sperrt die Session ohne Recoveryfall; null zusätzliche POSTs. Keine echte Herstellerprobe.
Vor Refactoring beide Suiten grün. Full-Guard fand Kopplung41 im kontrollierten Writer:
Abschluss-/Observationverantwortung bei identischer Gate/State in ControlledCompletionStore
extrahiert. Der öffentliche Test enthielt HTTPdraht und Runtimeassertion (Kopplung44);
Wirefixture separat komponiert. Keine Metrikgrenze oder Messumfang geändert.

Finale vollständige Qualität Exit0: artifacts/quality/linux/b2ac518def394c5791749127c849cb84,
Sourcehash afa5e8e3fcb582e0542288ed9e8f69eb99aabd2f7941a2f3752ca6baed7e3477.
TRX gezählt: 444 bestanden (247 Unit,195 Contract,je1 Integration/Plattform),0 Fehler/Skips.
13 Coveragegatefälle,Locked-Restore/Build/Format/Analyzer/Audit erfolgreich.
1167/1222 Produktzeilen=95,4992 Prozent. Full-Guard Exit0:195 Dateien/197 Typen,
0 Verstöße/Parsefehler/Skips. Die frühere continuation-quality.log blieb leer, weil
vorher der Guard fehlschlug; ausschließlich continuation-quality-final.log belegt diesen Lauf.
T117 lokal abgeschlossen; echte atomare SQLite-Persistenz folgt T060/T065, Close/Reopen
T037/T043/T079. Keine Gesamt-US2-/Freigabeabnahme. Voriger Stand6344816 auf Windows/Ubuntu
CI erfolgreich: quality40848238534,Guard40848238603.
