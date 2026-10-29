# US5: laufender Recovery-/Persistenznachweis

## T113: Recoveryziel und lesbare Revision (2026-10-29)

Owner: ausführende Implementierungssitzung; Branch `feature/reserved-dispatch-contract`,
Basiscommit `67a6d3f`. Vorabanalyse: `architecture/2026-10-29-recovery-contract.md`.
Kein SQLite-/Crash-/Live-Nachweis. T113 ist lokal umgesetzt, US5 insgesamt weiterhin offen.

Befehle über `bash /home/user1/dev/scripts/run-capped.sh --`:
`dotnet test tests/Monstertacle.Multisession.ContractTests/Monstertacle.Multisession.ContractTests.csproj -c Release --no-restore --filter FullyQualifiedName~<Klasse> --logger trx --results-directory artifacts/tdd/<Ordner>`.
Manifestdateien sources.sha256 vor jedem Lauf erfasst, originale TRX lokal/ignoriert.

| Zyklus | Beobachteter fachlicher Red | Green |
| --- | --- | --- |
| RecoveryCaseContractTests | `recovery-contract-red-executed`: 5 Fehler; Get liefert keine Opening-/Executionfälle, daher NotNull verletzt | `recovery-contract-green`: 5/5; Opening ohne Dummyrequest, aktuelle Read/Listrevision, Close ohne Endbeleg, ConfirmEnded, CAS und doppelte DecisionId |
| RecoveryNativeContractTests | `recovery-native-red`: 5 Fehler; Reopenbarriere nicht betreten, keine native Inspektion, Unknown-Inspect nicht als NotFound erkannt | `recovery-native-green`: gesamte Contractsuite 36/36, davon 10 neue Fälle; Reopen derselben Identität, native I/O außerhalb Gate, neuer CAS nach I/O, keine Teilpublikation |

SHA256 der jeweiligen Source-Manifeste in derselben Reihenfolge:
`68bc0134b9b1b30a504af8b038b2cc98522a9be5ca04705522a348b8dc83ade8`,
`c65199d901f1f687a4d6e7ecb2edcedc099080ccf11eb5aef88b29c46ac2fa0e`,
`8286df4b83f96069c167624f5bddf85327846654a43157fbb1dfc14b3746c006`,
`a0af604f45cf10e37eb66f11d080af1bebcf245ee90e900ce30be33d8e0c0d97`.
Erste Compileversuche scheiterten an CA1859 der Testvariablen/-fabrik, nicht am Fachverhalten;
sie sind kein Red. Bestehende 195 Unitfälle vor/nach Refactoring grün.

RecoveryService liefert targetbezogene Fälle und liest native Operations-IDs ohne Mutation.
ConfirmEnded ohne Closing führt Reopen desselben bekannten Bindings aus und reicht das
geprüfte Binding an den atomaren Store-CAS weiter. Close allein bleibt Closing/Open/Unknown.
Die kontrollierte Auflösung erhält historische Interrupted-Ergebnisse; keine Umdeutung zum
Erfolg und kein Send. Alle Fälle behalten ihre CreatedUtc, Revisionen werden erhöht.

Codemetr meldete nach Erweiterung zunächst Kopplung 42 bei Grenze 40 im Schreibdouble.
Kontrollierte Queryverantwortung und gemeinsame Datenhaltung wurden in ControlledQueryStore
und ControlledStoreState getrennt, ohne neue Baseline, Scope- oder Regeländerung. Dieselben
Queries bleiben explizite ISessionQueryStore-Implementierungen. Alle vorhandenen Barrieren
und Snapshots verwenden denselben Storegate. Full-Guard nach Refactoring: 98 Dateien/99 Typen,
0 Verstöße/Parsefehler/übersprungene Typen, CLI 0.1.0, Exit 0.

Vollständiger nativer Linuxrunner `pwsh -NoProfile -File scripts/Invoke-Quality.ps1 -Platform linux`
über run-capped: Exit 0. Locked-Restore/Build/Format/Analyzer/Audit bestanden, 0 Buildwarnungen/
-fehler. Maschinell aus TRX: 233 .NET-Tests bestanden (195 Unit, 36 Contract, je 1 Integration/
Linux-Infrastruktur), 0 fehlgeschlagen/nicht ausgeführt; zusätzlich 13 Gateverhaltensfälle.
Gesamtprodukt-Coverage 445/466 = 95,4936 %, Mindestwert 87 % erfüllt.
Sourcehash `823d5ab4da5769aa73f5b3e229d9424bddc12dab515968ceb422c7ee873c0b70`;
Originalberichte `artifacts/quality/linux/60842e25511149ccac4d2e47750a4231/`.
Debian GNU/Linux 13, PowerShell 7.6.9, SDK 10.0.403; kein Ersatz für native Zielsystem-/
SQLite-/Crash-/Liveabnahme und keine Produktfreigabe.

Befundabgleich: Fehlende Recoverytargets/-revisionen mit T113 und zehn neuen Fällen abgedeckt;
Kopplungsverstoß durch Querytrennung behoben und voll geprüft. Pagingcursor folgt in T115
(Owner nächste Implementierungsschritte, Größen-/Bereichs-/Livesichtabnahme). SQLite und
native Reopen-/Recoverypersistenz folgen den bestehenden T052/T062 (Owner US5-Umsetzung,
echte Transaktions-/Crash-/Revisionstestabnahme). Runtime und Closing mit anderen offenen
Aufträgen folgen T026/T079 (Owner US1/US3-Umsetzung, öffentliche Lebenszyklusfälle). Diese
Aufträge bleiben offen; kein T113-Befund wird als abgeschlossene Persistenz ausgegeben.

## T116: operationsgebundene Fortsetzung nach Unknown

Updated: 2026-10-29T08:21:21Z
Vorabanalyse architecture/2026-10-29-operation-recovery.md, Basis d795ee3.
Auslöser: ein lesbarer alter Interaktionskopf ist kein Beleg für den gültigen Kopf nach
ungewissem Folgeaufruf. Die generische Recovery wird vor der Gemini-Integration ergänzt.

`dotnet test tests/Monstertacle.Multisession.ContractTests/Monstertacle.Multisession.ContractTests.csproj -c Release --no-restore --filter FullyQualifiedName~OperationRecoveryTests`
über run-capped: fünf fachliche Fehler, danach alle 15 Recoveryfälle grün. Die alte
Implementierung nutzte gewöhnliches Reopen oder veröffentlichte fehlende/fehlerhafte
Operationsbelege. Neues optionales IExecutionRecoveryProvider, explizite Capability und
sichere Ablehnung bei fehlendem Port; native Prüfung erhält bekannten Operationsbezug.
Fehlender Beleg darf UnsupportedProviderAction/Unknown bleiben, kein alter-Kopf-Fallback.

Zweiter Zyklus OperationRecoveryEvidenceTests: erster Lazy-Bezug im Case fehlt (1 Red),
operationsgebundenes I/O/CAS-Race bereits grün (1 Regression). Danach 120 Contract- und
240 Unitfälle grün. Persistiertes SessionEstablished ergänzt nur die bekannte native
Caseidentität, nicht den bestätigten Sessionhead. Explizite Klärung prüft denselben Bezug
und veröffentlicht Binding/Revision/Entscheidung gemeinsam. Fremde Identität bleibt gesperrt;
konkurrierende Closeentscheidung verhindert die verspätete Kopfveröffentlichung.
Native Open/Send finden bei Klärung nicht statt, Interrupted wird kein nachträglicher Erfolg.

Manifeste unter artifacts/tdd/; Zeiten ausdrücklich LastWriteTimeUtc der vor Test erzeugten
Manifestdatei, jeweils Red vor Green:

| Stand | UTC der Manifestdatei | SHA256 |
| --- | --- | --- |
| operation-recovery-red | 2026-10-29T08:16:20.8758534Z | b70f61a659e047be448405422f60fedb090dd2c5a7e81b5ae189c9264eeb445e |
| operation-recovery-green | 2026-10-29T08:16:51.1137352Z | 39f1c77be74066d3f4007030e59cccf1f3e7798d25a17ba003df804727843993 |
| operation-evidence-red | 2026-10-29T08:18:09.4946163Z | 4729a0fde7ab02ac9c71308f8b5bc34075442a255d458b901da78efd291650cf |
| operation-evidence-green | 2026-10-29T08:18:38.6844351Z | 802aa3d0577966b88eeae51899602d53f0eb156ec9a3b8b12e5c8abe10500e3c |

Vollständige lokale Qualität Exit 0:
`artifacts/quality/linux/0c4d8cf9cfe24a118b6da03e164f6206/`, Sourcehash
`8186dabb3b1aaf6654063031407307a71c8653f70ba4c16570f8ed285c468fef`.
362/362 .NET-Tests aus vier TRX gezählt (240 Unit/120 Contract/je 1 Infrastrukturfall),
0 Fehler/Skips. 13 Coveragegatefälle, Locked-Restore, Build, Format/Analyzer, Audit grün.
922/960 Produktzeilen = 96,0417 Prozent. Full-Guard Exit 0, 160 Dateien/162 Typen,
0 Verstöße/Parsefehler/übersprungene Typen. Keine Pin-/Grenzwert-/Scopeänderung.

T116-Vertrag und kontrollierte Projektion lokal abgeschlossen. Native Gemini-Portprüfung
ist konkreter Folgeumfang T033/T039/T043, Owner ausführende Gesamtimplementierungssitzung:
zuerst GET des ungewissen Kopfes fachlich rot, dann bestätigten Kopf aus nativen Belegen,
keine alte-Kopf-Verzweigung und keine neue Identität. SQLiteprojektion/CAS folgen denselben
Regeln in T060–T062 mit Crash-/Persistenzabnahme. Diese Tasks bleiben offen; dies ist kein
SQLite-, Live- oder Zielhostnachweis. Native Conversations-CI am d795ee3 ist Windows/Ubuntu
grün (quality 40840206354, Guard 40840206436); T116-CI folgt nach Push.

## T049/T056-Formatvertikale: expliziter SQLitebestand

Voranalyse architecture/2026-10-29-sqlite-foundation.md, Source1ab4726.
Öffentliche Factory zunächst ohne Verhalten; 26 Formatfälle fachlich rot. Nach zwei
zusätzlichen Journal-Sidecarfällen wurden 28/28 fachliche Fehler beobachtet (keine
Initialisierung bzw. erwartete typisierte Ablehnung, stattdessen NotImplemented).
Der letzte Redstand enthält bereits noch nicht verdrahtete DDL/Connectionpolicy;
kein Factory-/Persistenzverhalten war grün implementiert. Das ältere 26er-Manifest
wurde beim erweiterten Redlauf überschrieben; es wird nicht rückwirkend rekonstruiert.
Die hier belegte 28er-Fassung ist tatsächlich vor Factory-Green ausgeführt worden.
Anfänglicher Analyzerfehler CA1861 ist ausdrücklich kein Redbeleg.

Befehle über run-capped: dotnet test tests/Monstertacle.Multisession.IntegrationTests
-c Release --no-restore --filter FullyQualifiedName~StoreFormatTests, vor und nach Green.
Unmittelbar vor Tests erstellte Quellenmanifeste einschließlich .sql, UTC aus Dateizeit:

| Stand | UTC | SHA256 |
| --- | --- | --- |
| sqlite-format-red | 2026-10-29T10:18:21.2479431Z | 98a6d5dc8f49e7ec6572d01405745e16b9daed09444010e8dc5a5eb11dc64fa2 |
| sqlite-format-green-compiled | 2026-10-29T10:20:05.9905410Z | 26e169da042e3a863db741c05c1d91ec68317c409a968546f69a7f0af04da656 |

28/28 danach grün. SchemaV1 mit acht Tabellen und aktiven Uniqueconstraints,
Metadaten/Trustscope/Header/echter Schema-/Integritätsvergleich, keine Migration oder
Create-if-missing. Unknown/älter/korrupt/unerwartetes Schema bleibt unverändert.
Journalpfad wird auf privat vorbereiteter Kopie geprüft; originale DB/Sidecardatei
bleiben bei Ablehnung bytegleich. Das ist noch kein echter Hotjournal-/Prozesskillbeleg.
ReadWrite statt automatischem Create, Pooling=false und ausgelesene DELETE/EXTRA/
foreign_keys/busy_timeout1000 sind Voraussetzung erfolgreicher Factoryöffnung.
Der neue Leaseport verlangt private Dateivorbereitung. Native Rechte/Besitz bleiben
T091/T092; das Dateifixture ist kein Ersatz für Lifetime-Lock-/Alias-/Fremdprozessbeweise.
Factory ist zustandslos/statisch, kein unnötiges Objektregister. Domainports/Bootstrap
bleiben ausdrücklich unimplementiert für ihre nächsten TDDzyklen T050–T066; kein
öffentlicher Runtime-/Haltbarkeitsanspruch aus bloßem Formatstart.

Vollständige Qualität Exit0: artifacts/quality/linux/06cb479103f44c7896d5151f9ba799a7,
Sourcehash4950a2dc9be450a193cc8275ce91992e253e3ff2df7fb7340a05f70563409121.
Der Qualitäts-Sourcehash nimmt jetzt auch eingebettete .sql-Ressourcen auf.
TRX gezählt478 bestanden:253 Unit/195 Contract/29 Integration/1 Plattform,0 Fehler/Skips.
13 Coveragegatefälle,Locked-Restore/Build/Format/Analyzer/Audit erfolgreich.
1308/1387 Produktzeilen=94,3043 Prozent. Full-Guard206 Dateien/208 Typen,
0 Befunde/Parsefehler/Skips, keine Scope-/Grenzwertänderung. Erster Qualitätslauf
scheiterte an privaten Konstantennamen; danach an Projektkonvention angeglichen und
vollständig erneut geprüft. Nur sqlite-format-quality-final.log belegt finalen Pass.
T049/T056 bleiben für vollständige Binding-/Crashabnahme offen; nächster konkreter Auftrag
T048 echte Journalbarriere, danach T050/T054/T057/T058. Owner ausführende Sitzung.
1ab4726 auf Windows/Ubuntu erfolgreich: quality40855398948,Guard40855398914.

## T048: echte Hotjournalbarriere und hartes Prozessende

Voranalyse architecture/2026-10-29-crash-journal-harness.md. CrashHarness startet den
gebauten nicht produktiven ValidationHost in markierter privater Ablage. Child schreibt
wirkliche uncommittete SQLitepages bei kleinem Cache, meldet Phase/PID/UTC und wird nach
Barriere hart als Prozessbaum beendet. Hotjournalmagic wird an tatsächlichen Dateibytes
geprüft. Zwei Fälle sofort grün mit existierender Factory, keine Produktkorrektur/kein
künstliches Red: gültiger Store rollt zurück, StoreId bleibt erhalten, uncommittete Tabelle
verschwindet; unbekannte Metadataversion lässt Original-DB und -Journal bytegleich.
Probekopien werden entfernt. Keine SC-009-/Reservations-/NativeLeaseabnahme behauptet.

Zusatzregression für Fixtureprivatheit: Marker unter lokaler gemessener umask0077 schon
0600, gleicher Test unter022 zweimal fachlich rot mit0644. Harness erhält nun vorhandene
Lease und bereitet Marker ausschließlich per PrepareFileAsync/CreateNew vor; unter022
31 Integrationfälle grün. Kein globaler Umaskwechsel im Produkt/Testharness.
Befehle run-capped dotnet test IntegrationTests -c Release --no-restore --filter
FullyQualifiedName~CrashJournalTests; Marker-Red/Green im bash-Prozess mit umask022.
Manifeste aus src/tests/examples einschließlich SQL, UTC aus LastWriteTimeUtc:

| Stand | UTC | SHA256 |
| --- | --- | --- |
| crash-journal, ergänzend grün | 2026-10-29T10:25:45.5519254Z | 7d5e7d7f9749decd168aef966ba8468c8ed9f9f3c2848ad1935805ae84b954bf |
| crash-marker-default-umask, grün | 2026-10-29T10:26:43.9476024Z | b4be9686af74635d7ca590507e58f10e5e09d3f4c0f38ae396ee87a1fe19b9ed |
| crash-marker-red,022 | 2026-10-29T10:27:34.2427727Z | b4be9686af74635d7ca590507e58f10e5e09d3f4c0f38ae396ee87a1fe19b9ed |
| crash-marker-green,022 | 2026-10-29T10:27:57.9042712Z | 03324dfcd1e77b15adbf84181148666e85be996c58c1518f10facfbf36c7873e |

Volle lokale Qualität Exit0: artifacts/quality/linux/75042a1064b04160ac26b6072ca45640,
Sourcehash261734a0c50d457aec518d213caae9d45da861b7dc2894c9b902b5bb9f1a545d.
TRX gezählt480:253 Unit/195 Contract/31 Integration/1 Plattform,0 Fehler/Skips.
13 Coveragegatefälle,Locked-Restore/Build/Format/Analyzer/Audit erfolgreich.
1308/1387 Produktzeilen=94,3043 Prozent. Full-Guard210 Dateien/213 Typen,
0 Verstöße/Parsefehler/Skips. Host/Fixture im Guard, Host bleibt kein Produktcoveragecode.
Keine Pin-/Scopeänderung. Restore nach neuer Buildabhängigkeit ohne Lockfileänderung.
T048 bleibt für fachliche Annahme-/Dispatch-/Resultatbarrieren offen, nächster Auftrag
T050/T054/T057/T058. Crashmodus ist interne Infrastruktur, kein fertiger Szenariohost T094.
Formatstand1df4b05 auf Windows/Ubuntu-CI grün: quality40857368718,Guard40857368737.

## T057/T058: echte Annahme über einzelnen begrenzten DBworker

Voranalyse architecture/2026-10-29-sqlite-admission.md, Ausgang457e901.
AcceptancePersistenceTests/SerializationCultureTests zuerst14 fachliche Fehler am fehlenden
Storebootstrap; StorageFailureTests weitere5 fachliche Fehler vor Worker-/Annahmegreen.
Eine ursprüngliche Negativerwartung für nichtleeren Whitespace war unzulässig und zählt
nicht als Produktbefund: bereits Utf8MessageLimitsTests erlaubt ihn ausdrücklich. Die
Erwartung wurde durch positive unveränderte Whitespaceannahme ersetzt. Ebenso prüft der
Datenschutzfall jetzt Message/InnerException statt Namespaceworten im Stacktrace.
Keine Fachregel für diese beiden falschen Erwartungen geändert, kein künstliches Red.

Relevante Befehle immer run-capped, dotnet test IntegrationTests -c Release --no-restore:
Redfilter FullyQualifiedName~AcceptancePersistenceTests|FullyQualifiedName~SerializationCultureTests
sowie ~StorageFailureTests; nach Green vollständige Integrationssuite.
Zusätzlicher tatsächlicher Redfall UpdatesSessionTimestampTogetherWithSequenceAssignment:
NextSequence war verändert, UpdatedUtc noch die frühere Bindungszeit. Jetzt Timestamp und
Sessionjson gemeinsam mit Request/Sequenz committen. 50 Integrationfälle danach grün.

Worker führt SQLiteobjekte strikt seriell aus. Begrenzt TryWrite, nicht unbeschränkte
Wartetasks; Queuevoll/Jobcancel vor Beginn/Disposal liefern belegte Nichtannahme. Bereits
aktive Transaktion endet tatsächlich, danach erst Verbindung freigeben. Cancel wird nicht
mit nativer Endbestätigung verwechselt; erfolgreiche Commitrückkehr gewinnt spätes Token.
Eigener Parent hält reale konkurrierende Schreibtransaktion: Busy vor Begin verliert keinen
Input/Sequence, Channelcap1 weist Überhang ab, wartender Cancel innerhalb2s beobachtet,
Dispose schließt keine Verbindung während aktiver SQLarbeit. Dies ist kein SC-004-Nachweis
für Providerabbruch bei verfügbarem Store. Common Inputregeln werden öffentlich rein
wiederverwendet, nicht nachgebaut. UTF-8-Grenze inklusive/1Byte darüber und Injectionsyntax
bleiben echte gebundene Daten. Je zwei gleiche Eingaben erhalten unterschiedliche IDs.
Wiederöffnung behält IDs/FIFO/Model/Settings/Bindingversion7, nächste Sequence steigt weiter.
Culture de-DE/en-US/tr-TR unverändert; tatsächliche Zielhost-Zeitzonenmatrix T067/T099 offen.

DefaultTimeout/PRAGMA sind in erster nativer Version fest1s; abweichende gültige generische
Option durfte zunächst still passieren (neuer Factoryfall fachlich rot). Jetzt explizit
InvalidConfiguration vor Datei-I/O. Kein stiller Ersatzwert, allgemeine RuntimeOptions
unverändert. Weitere Busywerte sind noch keine zugesicherte native Fähigkeit.
Vor benannten Workstatuskonstanten50 Integrationfälle grün, Verhalten unverändert;
danach volle Regression/Guard. Compiler-CA1068 und Format sind keine Redbelege.

Zusätzliche AcceptanceFailureTests sofort grün mit bereits implementiertem Runner:
echter AFTER UPDATE-Trigger scheitert nach Requestinsert, Rollback entfernt Row/Sequence
und bestätigt NotAccepted. Tatsächlicher deferred ForeignKeyfehler erst bei COMMIT bleibt
Unknown samt Kandidaten-ID über RequestAdmission, kein Receipt/automatischer Retry und
keine SQL-/Inputmarker in Exceptions. Regressionsbelege, kein nachträgliches TDD-Red.

Unmittelbare Manifeste src/tests/examples inklusive SQL, UTC aus LastWriteTimeUtc:

| Stand | UTC | SHA256 |
| --- | --- | --- |
| sqlite-admission-red | 2026-10-29T10:40:38.5363148Z | e11e4d8cd0cd6ed149ca789633a41a2a301bf7794a352deacace395afe4ef36b |
| sqlite-work-red | 2026-10-29T10:41:46.5206019Z | 0106940a6576759e2e2d0f1525b9a667b6e0f2157bf807e7b540b417af9c3f8b |
| sqlite-admission-time-red | 2026-10-29T10:49:18.1530703Z | 5aba0a477cc56744b1120fcccac1e3b7b248079736af38fbd5aae82b5dbcacd8 |
| sqlite-admission-green-final | 2026-10-29T10:51:32.4495330Z | befd11651d2fbd7887c8fd27c9fc319707848b1071478892401ffec8f308fe0d |
| sqlite-busy-option-red | 2026-10-29T10:53:24.6766044Z | ebffef52f9bf3fdf5d71f2b2e94e5a27489ee394d32fc06c25a0f547a28df752 |
| sqlite-busy-option-green | 2026-10-29T10:54:10.6234993Z | 377bbff50c8e238e7544c49fc86b5b3f3a2dc2e7f0c06ba96ac3e99b56bb9521 |
| sqlite-admission-faults, ergänzend grün | 2026-10-29T10:57:29.4800280Z | f73f0717f59c16d16360bffccb3003f06e3b4485248851a1a565c92b095ee7ea |

Finale ganze Qualität Exit0: artifacts/quality/linux/0080ecd4009a4e17bdfc9303675309ac,
Sourcehash8cc3ef27a780baf5638a7f1c571054186c37f4b6350d929c58c3d36ce8824bf1.
TRX gezählt502 bestanden:253 Unit/195 Contract/53 Integration/1 Plattform,0 Fehler/Skips.
13 Coveragegatefälle,Locked-Restore/Build/Format/Analyzer/Audit erfolgreich.
1519/1595 Produktzeilen=95,2351 Prozent. Full-Guard227 Dateien/231 Typen,
0 Verstöße/Parsefehler/Skips, kein Scope-/Pin-/Grenzwertwechsel.

T057/T058 lokal umgesetzt. T050 bleibt für echte fachliche Crash-/verlorene-Ack-Punkte
und ListRequests offen; T054 für weitere Datenträger-/ReadOnlyfälle. T061 muss konservatives
Start-Nein für alte Opening/Running durch geprüfte Recovery ersetzen; derzeit kein Replay.
Nächster konkreter Auftrag T063 Session-/Requestkeysetqueries für Auffindbarkeit, dann
T051/T059/T060 gespeicherter Dispatch/Abschluss und T052/T061/T062 Recovery. Owner ausführende
Gesamtimplementierungssitzung. Keine SQLite-Runtime-/Gesamt-US5-Freigabe; übrige Ports noch
Gerüste. Frühere Crashvertikale457e901 auf Windows/Ubuntu-CI grün:
quality40858563809,Guard40858563778.

## T063-Teilschnitt: native Session-/Requestkeysets

Voranalyse architecture/2026-10-29-sqlite-pages.md, Source4113798. Elf SqlitePageTests
fachlich rot an den fehlenden nativen Listenports; anfängliches ungenutztes Using ist kein
Red, dessen Buildlauf separat unter sqlite-pages-build. Danach11 grün: ordinaler UUIDkey
bei gleichem CreatedUtc, Einfügung vor/nach Lastkey, Cursor nach Wiederöffnung, Request-
Sequence und ursprüngliche Kandidaten-ID ohne verwendetes Receipt, Größen1/2/100 und
ungültige Größen/Encoding, fremder Store/Session/Listenart, unbekannte Session ohne Rows.
Native SQLs nutzen tupleKey, Parameter, ORDER BY und LIMIT size+1. Kein OFFSET oder
Vollmaterialisieren des gesamten Katalogs. Gemeinsamer strenger PageCursor unverändert.
History-/Recoveryqueries bleiben ihre noch offenen T060/T062/T063-Vertikalen.
Befehle run-capped dotnet test IntegrationTests -c Release --no-restore --filter
FullyQualifiedName~SqlitePageTests, anschließend gesamte Integrations-/Qualitätssuite.
Manifeste unmittelbar vor Tests, UTC aus LastWriteTimeUtc:

| Stand | UTC | SHA256 |
| --- | --- | --- |
| sqlite-pages-red | 2026-10-29T11:07:11.5883851Z | 559d5c4eb95a3e44d24a069761692900154c81a900d993ba80c9f0962bc74e72 |
| sqlite-pages-green | 2026-10-29T11:08:37.1457191Z | db545ed93d4dd528f5b5a1737558447fc9473de6d356fbf70429f17c91f6da7a |

Finale gesamte Qualität Exit0: artifacts/quality/linux/bc75c37842e74bb5a16d16d7be3ebcd8,
Sourcehashc9a7ee01750e5a50980689b9e8b5706297575a6ceb660b9deee183e64e40b438.
TRX gezählt513 bestanden:253 Unit/195 Contract/64 Integration/1 Plattform,0 Fehler/Skips.
13 Coveragegatefälle,Locked-Restore/Build/Format/Analyzer/Audit erfolgreich.
1553/1626 Produktzeilen=95,5105 Prozent. Full-Guard230 Dateien/234 Typen,
0 Verstöße/Parsefehler/Skips, kein Scope-/Pin-/Grenzwertwechsel.
T063 bleibt für vollständige Listen offen. Nächster konkreter Auftrag T051/T059/T060
persistierter ReservedDispatch, Beobachtung und atomarer Abschluss; Owner ausführende
Gesamtimplementierungssitzung. Annahmestand4113798 Windows/Ubuntu-CI erfolgreich:
quality40862468558,Guard40862468395. Keine ganze SQLite-Runtime-/Recoveryfreigabe.

## T059/T060: ReservedDispatch und atomarer bekannter Abschluss

Voranalyse architecture/2026-10-29-sqlite-dispatch.md. Elf Reservierungs-/Abschlussfälle
fachlich rot an fehlendem TryReserve, weitere acht Callback-/Graphintegritätsfälle rot.
CA1861 im ersten Callback-Build ist kein Red, getrennt unter sqlite-observation-build.
Danach alle83 Integrationfälle grün,19 neue Verhaltensfälle. Gemeinsame Registrationprüfung
vor Send bleibt; reine DispatchReplyPolicy wird mit bereits geprüftem Originalbinding
auch im Storage benutzt, statt Outcome-/Identity-/Outputregeln zu kopieren.

ReservationPolicy entscheidet in derselben SQLtransaktion über Sessionhead, globale
Slots/Workspace; Running/InProgress, StartedUtc, DispatchId, BindingRevision und Snapshot
werden gemeinsam veröffentlicht. Private StoredDispatch-Envelope hält unveränderten
Snapshot und separat acknowledged Session-/Operationsbezug. Keine DDLänderung nötig.
GetReservedDispatch verweigert nach Reopen/vor Bootstrap altes Sendrecht, bei spätem
Bindingupdate bleibt FrozenSnapshot unverändert. Gegen widersprüchliche Graphids gibt es
StorageUnavailable statt Input für falsche Session. Gleichzeitige Reservierer überbuchen
weder globale Kapazität noch dieselbe Session, blockierter Workspace wird übersprungen.

Callbacks sind vor Rückkehr gespeichert und bestätigen allein keinen neuen Head.
Öffentlicher RequestDispatcher verarbeitet erste/Folgeeingabe mit bestätigtem neuen Head;
Providerfixture kann unabhängige Schreibtransaktion eröffnen und sieht committed Marker:
kein Provider-I/O in Produkt-DBtransaktion. Native Hersteller werden hier nicht aufgerufen.
Bekannter Abschluss schreibt Result/EndedUtc, unveränderte lokale Ein-/Ausgabehistory,
HistoryOrdinal/HistoryId, Binding/Revision/Blockgrund und Reservationrelease zusammen.
Endgültiger lokaler Resultattyp bleibt vorhandener RequestSnapshot.Result mit EndedUtc
und Results.CompletedUtc; keine zweite gleichbedeutende RequestResult-Klasse anlegen.
Opaque Operationbeleg bleibt dispatchgebunden im gespeicherten Envelope, nicht als Secret.

Exaktes/zu großes UTF-8-Output, Contextfehler ohne Änderung früherer Einträge, fortsetzbare
Historykeysets, doppelte/racende Completion und stale BindingRevision geprüft. Echter SQL-
Trigger nach Result-/Historywrites rollt gesamten Abschluss zurück: null Teilresultate,
Originalbinding/Running und aktive Reservation erhalten. Unknown wird hier ausdrücklich
nicht freigegeben; T052/T061/T062 folgen. Keine fachliche Kill-/SC-009-Abnahme vorgezogen.

Befehle run-capped dotnet test IntegrationTests -c Release --no-restore mit Filtern
ReservationPersistenceTests|CompletionPersistenceTests und
DispatchCallbackTests|ReservedSnapshotIntegrityTests, danach komplette Integration/Qualität.
Manifeste vor Tests, UTC aus LastWriteTimeUtc:

| Stand | UTC | SHA256 |
| --- | --- | --- |
| sqlite-dispatch-red | 2026-10-29T11:20:54.8679059Z | 56aa25d80112741005e7817a51098f7e756ef37d288d608c6a00c08a4c3754d6 |
| sqlite-observation-red | 2026-10-29T11:24:08.1880394Z | 7857306efbd0b2ec28d9a043b21ac11cf9135a33fceb61ae0957dd683c4c20ef |
| sqlite-dispatch-green | 2026-10-29T11:27:32.5888745Z | 6556fa09133ef6635e35cabe06e4eeb4281ea20c7ea17446b36503f2d54a9007 |

Volle Qualität vor CIharnessnachzug erfolgreich: artifacts/quality/linux/7fb1d3951846455b9d691b155b184106,
Sourcehash80ac71c23366779c3875bd6578adc6eec5bc6078117d59b6220a3c5888420289.
Danach Windows-Red an vorigem427691b gefunden: quality40863911503, CrashJournalTests
(false), sharing violation beim Journalread unmittelbar nach Kill/WaitForExitAsync.
Unbekannter tatsächlicher Handlebesitzer; keine erfundene Ursache. Vor Änderung dokumentiert
in architecture/2026-10-29-crash-journal-harness.md. Nur Harness wartet jetzt endliche
Windows-Sharing-/Lockfreigabe vor Lesen. Kein Produktretry, keine feste Pausenscheinabnahme.
Lokale Regression bestätigt normale Fälle, nativer Windows-Green bleibt bis neuer CI offen.

Finale gesamte lokale Qualität nach diesem Nachzug Exit0:
artifacts/quality/linux/8ae3c9e4ec1144dc98b5abc092fdca42,
Sourcehash29dbc44e48f253e6692ea0a82fda6f2910e2add1f627a6813c0b8490ca8c0162.
TRX gezählt532 bestanden:253 Unit/195 Contract/83 Integration/1 Plattform,0 Fehler/Skips.
13 Coveragegatefälle,Locked-Restore/Build/Format/Analyzer/Audit erfolgreich.
1710/1780 Produktzeilen=96,0674 Prozent. Full-Guard242 Dateien/247 Typen,
0 Verstöße/Parsefehler/Skips, keine Pin-/Scope-/Grenzwertänderung.
T059/T060 lokal umgesetzt. T051/T053 bleiben für fachliche Crashbarrieren/Gesamtabnahme
offen; T063 jetzt auch History, Recoveryliste folgt mit ihren Writes. Nächster konkreter
Auftrag T052/T061/T062: Unknown/Openingfälle, konservativer Bootstrap und CAS, Owner
Gesamtimplementierungssitzung. Windows-CIharnessnachweis T104/T111 am neuen Kopf verfolgen.

## T052/T061/T062: Unknown, Bootstrap, CAS und offene Falllisten

Voranalyse architecture/2026-10-29-sqlite-recovery.md, Ausgang6072d52.
RecoveryPersistenceTests/RecoveryDecisionTests/RecoveryBootstrapTests: zwölf fachliche
Fehler vor Green, ein bereits grüner konservativer Konsistenzfall. Danach96 Integration-
fälle bestanden. Unknown schreibt Interrupted/Unknown mit Result/EndedUtc und neuer
History, hält Reservation und bestätigt weder Head noch Erfolg. Native erste Session-ID
und Operation bleiben getrennte Casebelege. Openingcase auch nach zuvor bestätigter
Bindung vorhanden, kein Dummyrequest. Read/List sind rein lokal und begrenzt/keysetfähig.

Bootstrap prüft FK/Markerbeziehungen, projiziert altes Running atomar zu Unknown, blockiert
zugehörige Session und lässt unabhängige Queued in freiem Slot weiterarbeiten. Zweiter
Start dupliziert weder Case/Revision/History. Diese Tests disposen/reopen ausdrücklich;
sie sind keine echten Kindprozessabbrüche/SC-009. Registry-/Providerverfügbarkeit vor
Runtimeworkers ist verbleibender konkreter T061/T066-Auftrag der ausführenden Sitzung.

DecisionId/ExpectedRevision/Kind/Pflichtbelege werden im selben Commit geprüft. Gleichzeitige
Entscheidungen haben einen Sieger, doppelte/veraltete IDs keine Teilwirkung. ConfirmEnded
behält historischen Interrupted-Outcome, aktualisiert nur aktuelle Endgewissheit und
Reservation/Case/Binding; alte History bleibt damaliger Beobachtungsbeleg. Closeintent
hält Unknown, storniert nur Queued als Cancelled/NotStarted und setzt Closing. Erst nach
separatem Endbeleg Closed mit ClosedUtc. Kein neuer Nativebezug für reine Schließung nötig.
Native Reopen/Inspect bleibt im bereits geprüften Commonservice außerhalb SQL/CAS.

Zusätzliche tatsächliche Regressionen vor Korrektur: request-owner änderte ausschließlich
Requests.SessionId bei intakter Envelope und blieb unerkannt (1 rot/3 grün). Read vergleicht
nun den autoritativen Requestowner. Zwei weitere rote Casesnapshot-Links (RequestId/
DispatchId) werden gegen die scalarFKs und Targetart geprüft. Kein Sendrecht bzw. keine
Operation auf widersprüchlicher Identität. Quellen/Entscheidungen vor Korrektur in
architecture/2026-10-29-sqlite-dispatch.md und -sqlite-recovery.md.

Befehle run-capped dotnet test IntegrationTests -c Release --no-restore, Redfilter
RecoveryPersistenceTests|RecoveryDecisionTests|RecoveryBootstrapTests, danach
ReservedSnapshotIntegrityTests bzw.RecoveryIntegrityTests, dann komplette Suite.
Manifeste vor Tests, UTC aus LastWriteTimeUtc:

| Stand | UTC | SHA256 |
| --- | --- | --- |
| sqlite-recovery-red | 2026-10-29T11:44:48.6615376Z | 7ef8a0d98fc37b81a62502d0c571248cef8c3ac44d644fcdac38a37e1ab10581 |
| sqlite-recovery-green | 2026-10-29T11:47:38.7521462Z | fba80cdd6363683c9963eae36e52e1f83f89c9fba55e183736643b6ad11c2d7e |
| sqlite-owner-red | 2026-10-29T11:52:32.9425262Z | da7c3f9b900f62ab5a0dea60e3ea1636e32e284f01f23b3eebd83fc71958a3d4 |
| sqlite-owner-green | 2026-10-29T11:53:05.3045498Z | fb3e652a14f34b7abeca3dd5b971651e4cd2f73533bf44bbc3f57e2fb25d50b2 |
| sqlite-case-integrity-red | 2026-10-29T11:54:26.7576443Z | af8ebc7a64131b2739b1aa47f9c22083ebabf7667339487101d7f3f994344012 |
| sqlite-case-integrity-green | 2026-10-29T11:55:11.3204321Z | de32954248afe851f03f26e50c093fce2e3db422109e72e7a9cf7b75d4541c9b |

Erste gesamte Qualität scheiterte nach sauberem Guard an MSB4166 (MSBuildchild unerwartet
beendet); angekündigter Diagnosetempordner anschließend nicht vorhanden. Kein fachlicher
Fehler oder OOM als ungemessene Ursache behauptet. Frische Buildprozesse mit
MSBUILDDISABLENODEREUSE=1/DOTNET_CLI_USE_MSBUILD_SERVER=0 liefen erfolgreich, danach jede
geänderte Fassung vollständig neu geprüft. Keine Prüfungsabschwächung.
Finale gesamte Qualität Exit0: artifacts/quality/linux/2aaef93961034c2fa26b9e8c2d33ac7d,
Sourcehashc3901ce447771b6e60f6a42a6d22a820759d3c0b8191214a936d5923816e5576.
TRX gezählt548 bestanden:253 Unit/195 Contract/99 Integration/1 Plattform,0 Fehler/Skips.
13 Coveragegatefälle,Locked-Restore/Build/Format/Analyzer/Audit erfolgreich.
1935/2003 Produktzeilen=96,6051 Prozent. Full-Guard252 Dateien/257 Typen,
0 Befunde/Parsefehler/Skips, keine Pin-/Scope-/Grenzwertänderung.
T062 und komplette vier SQLkeysetlisten T063 lokal umgesetzt. T061 bleibt für generische
Verfügbarkeitskoordination offen; T052/T055/T066 für ganze öffentliche/Kill-/SC-009-Abnahme.
Nächster Auftrag: öffentliche Runtime mit echtem SQL und Verfügbarkeitsprüfung vor Workers,
anschließend fachliche Crashbarrieren/zehn SC-009-Läufe. Owner ausführende Sitzung.
6072d52 CI Windows/Ubuntu erfolgreich: quality40866982538,Guard40866982692; damit ist der
Windows-Sharingnachzug aus dem vorherigen Abschnitt nativ bestätigt.

## T061: lokale Availability vor öffentlichem Runtimeversand

Voranalyse architecture/2026-10-29-startup-availability.md. Acht SessionAvailabilityTests
fachlich rot, vier öffentliche StartupAvailabilityTests an echtem SQL ebenfalls fachlich
rot: fehlende Konfiguration wurde erst nach Reservation erkannt, statt ursprüngliche
Queued zu bewahren. Danach261 Unit/103 Integrationfälle grün.

RecoveryCoordinator läuft nach durable Bootstrap/validierter Hostkonfiguration und vor
Workers. Ordinale Registrierung, Adapter/Format und explizite Executionkind-/Workspace-
fähigkeit prüfen; keine Herstellerweiche, Modellallowlist, native Credentials-/Modelcalls
oder Änderung gefrorener Settings/Bindungen. SessionSnapshot.ConfigurationUnavailable
unterscheidet eigene Konfigurationssperre von nativer Fortsetzungsrestriktion. Die zehnte
gezielte Storewriteoperation verwendet gemeinsame SessionAvailabilityPolicy und echte
Transaktion; Wiederkehr löst nur eigene Sperre, Recoverypflicht hat Vorrang. Bestehende
Positionals/DDL unverändert. Read-onlyMetadata und Backendkonfiguration bleiben getrennt.

Missingreg, falsche Formatversion und fehlende lokale Ausführungsfähigkeit blockieren vor
Attempt; andere Session arbeitet weiter. Nach Registrierung derselbe persistierte Kandidat
mit derselben nativen Session ohne erneutes Open fortgesetzt. Testleasewrapper lässt
persistente Daten beim RuntimeStop stehen, äußerer Fixtureowner räumt privat ab; kein
nativer Lifetime-Lockbeweis. Erst beim Backend erkennbare Auth-/Modellzugriffsprobleme
bleiben konkrete AufgabenT034/T080, kein vollständiger vorgelagerter Featurekatalog gefordert.

Befehle run-capped dotnet test UnitTests -c Release --no-restore --filter
FullyQualifiedName~SessionAvailabilityTests und IntegrationTests mit
FullyQualifiedName~StartupAvailabilityTests, danach komplette Suiten/Qualität.
Manifeste vor Tests, UTC aus LastWriteTimeUtc:

| Stand | UTC | SHA256 |
| --- | --- | --- |
| startup-availability-red | 2026-10-29T12:03:22.2311632Z | c0916bb2620861f6563acb2f519374676e35c718dbb1d467cc2a2b6f839cb1dc |
| startup-runtime-red | 2026-10-29T12:04:27.6712616Z | de4f26393d5f125b49e2dd8148abf36b31ed4d205af99b08e1df00c078aac15e |
| startup-availability-green | 2026-10-29T12:05:53.2766235Z | 4d7dcf8c336bb5f3df0d6258ee39d2eb220a6f8c5cdf81b8499c944794312a04 |

Gesamte Qualität Exit0: artifacts/quality/linux/aeb7fec6ea904f73b6db6dff8bcc31c7,
Sourcehasha35a36c04aaaf0780d60bf208a88d0012d645a1bec4595f63f8e293d348526f2.
TRX gezählt560 bestanden:261 Unit/195 Contract/103 Integration/1 Plattform,0 Fehler/Skips.
13 Coveragegatefälle,Locked-Restore/Build/Format/Analyzer/Audit erfolgreich.
1977/2045 Produktzeilen=96,6748 Prozent. Full-Guard258 Dateien/263 Typen,
0 Verstöße/Parsefehler/Skips, keine Pin-/Scope-/Grenzwertänderung.
T061 lokal umgesetzt. T066 bleibt für komplette echte Prozessmatrix offen; nächster
konkreter AuftragT048/T055/T066 fachliche Crashbarrieren und zehn echte SC-009-Läufe,
Owner ausführende Sitzung. Recovery1bf65f1 auf Windows/Ubuntu erfolgreich:
quality40869457535,Guard40869457549. Keine native Rechte-/Live-/Produktfreigabe.

## SC-009: zehn wirkliche Prozessabbrüche über öffentliche Runtime

Voranalyse architecture/2026-10-29-sc009-process.md, Ausgangbe8cf38. Neuer markierter
Childmodus session-recovery verwendet öffentliche Catalog-/Admission-/Dispatcherports
und echten SQLitebestand, hält synthetisches Backend nach bestätigter Operations-ID.
Parent kontrolliert je zwei Succeeded, drei Queued in A, ein Running in B, getrennte
Workspaces und Limit2. Dann wirklich Process.Kill(entireProcessTree), nicht Dispose.
Manifest enthält ursprüngliche IDs, keine Eingaben/Secrets. Wiederanlauf läuft über
öffentliche SessionRuntime mit unabhängiger kontrollierter Lease und gleichem Adapterformat.

Zehn benannte Theoryfälle0..9 im ursprünglichen TRX gezählt: executed10,passed10,failed0,
notExecuted0,distinct10. Alle sechs angenommenen Identitäten je Fall erhalten; fertige
Resultate A/B unverändert, alle drei A-Requests in ursprünglicher FIFO erfolgreich,
B Interrupted/Unknown, Operationbeleg erhalten, null erneute B-Sends, ein belegter B-Slot/
Workspace nach Wiederanlauf. Native Session-ID unverändert, kein neues Open.
Der vollständige Prozessfall wurde mit bereits zuvor testgetriebener Recovery sofort grün;
zusätzliche Integration/Crashregression, kein fingiertes TDD-Red. Kontrollierte Childlease
beansprucht keine native Lock-/Alias-/Zielhostabnahme. Weitere einzelne Commitbarrieren
bleiben T048/T050/T051-Auftrag, diese zehn Fälle ersetzen sie nicht.

Befehl run-capped mit frischen MSBuildprozessen:
dotnet test IntegrationTests -c Release --no-restore --filter
FullyQualifiedName~PersistenceRecoveryTests --logger trx --results-directory artifacts/tdd/sc009-process.
Vor Test erzeugtes Quellenmanifest inklusive examples und SQL, LastWriteTimeUtc
2026-10-29T12:14:55.8144834Z, SHA256
20c112419300aef8237fbb48bd21ea629278822a6d8e6da9737d35af5eac94b7.

Gesamte Qualität Exit0: artifacts/quality/linux/73670216ed5e49afb1a1d03a886d039b,
Sourcehashe33bced52a82ec6b732a8cd7ddb041adf95d8e5950f263ebfe255d7a5302a695.
TRX gezählt570 bestanden:261 Unit/195 Contract/113 Integration/1 Plattform,0 Fehler/Skips.
Die komplette Suite hat die zehn Prozessfälle erneut ausgeführt, ursprünglicher gezählter
TRX bleibt separat erhalten.13 Coveragegatefälle,Locked-Restore/Build/Format/Analyzer/Audit
sauber.1977/2045 Produktzeilen=96,6748 Prozent. Full-Guard262 Dateien/268 Typen,
0 Befunde/Parsefehler/Skips, keine Pin-/Scope-/Grenzwertänderung. Host/Testinfra im Guard.
T055/T066 bleiben für gesamte Punkt-/Plattformabnahme offen; SC-009 lokal zusätzlich
beobachtet. Nächster konkreter Auftrag T048/T050/T051 feine Commit-/Übergabebarrieren,
Owner ausführende Sitzung. be8cf38 auf Windows/Ubuntu-CI grün:
quality40871066600,Guard40871066576. Keine Live-/Rechte-/Releasefreigabe.

## Sieben zusätzliche echte Übergabe-/Commitbarrieren

Voranalyse als Erweiterung architecture/2026-10-29-sc009-process.md, Ausgang9fdbda7.
Bestehendes Childsetup wiederverwendet, ausschließlich bekannte markierte Punktnamen:
before-acceptance, after-acceptance, after-acceptance-before-wake,
after-reservation-before-send, after-external-before-id, before-completion-commit,
after-completion-commit. Eigene kontrollierte Backendantwort, keine Live-API oder
Produkthooks. Parent killt real und prüft öffentliche Wiederaufnahme anhand Kandidaten-ID.

Vor erstem APIaccept existiert kein Kandidatenrow. Nach dauerhafter Annahme ohne
Wecksignal bleibt Queued auffindbar und wird nach Start genau einmal gesendet. Nach
Reservation/unklarer Übergabe bzw. empfangener synthetischer Reply vor lokalem Commit
bleibt Interrupted/Unknown samt Slot, null erneute Sends. Nach Ergebniscommit bleibt
Erfolg/Output erhalten, keine Wiederholung. Vor-API-Barriere ist ausdrücklich kein Beleg
für partiell geschriebene SQLseiten; echtes Hotjournal und reale Teilwrite-/Commitfehler
wurden getrennt in früheren Abschnitten geprüft. Keine weitergehende Atomicitätsbehauptung.

Alle sieben neuen Fälle wurden sofort grün. Zusammen mit zehn SC009- und zwei Journal-
fällen: ursprünglicher separater TRX total19,passed19,failed0,notExecuted0 gezählt.
Zusätzliche Prozessregression bereits testgetrieben implementierter Produktverträge,
kein künstliches Red. Befehl run-capped/frische MSBuildprozesse: dotnet test IntegrationTests
-c Release --no-restore --filter CrashBoundaryTests|PersistenceRecoveryTests|CrashJournalTests
--logger trx --results-directory artifacts/tdd/crash-boundaries.
Vor Test erzeugtes Manifest LastWriteTimeUtc2026-10-29T12:21:15.7421952Z,
SHA2562842902ce336826efc4b8b1a5b43816f89a8381bb108069989da66da07011799.

Gesamte Qualität Exit0: artifacts/quality/linux/f46c2f64efce4c9cac7a292f68972108,
Sourcehashe891465393f62d38a32490fc60f6bbd20a00157ca3a5cbd05baaf401808c2825.
TRX gezählt577 bestanden:261 Unit/195 Contract/120 Integration/1 Plattform,0 Fehler/Skips.
13 Coveragegatefälle,Locked-Restore/Build/Format/Analyzer/Audit sauber.
1977/2045 Produktzeilen=96,6748 Prozent. Full-Guard263 Dateien/269 Typen,
0 Befunde/Parsefehler/Skips, keine Pin-/Scope-/Grenzwertänderung.
T048-Infrastruktur jetzt vorbereitet und ausgeführt. T050/T051/T055/T066-Gesamtabnahme
bleibt mit T054-Retention/Datenträgerfällen, späterem Lifecycle und nativer Plattformmatrix
abzugleichen. Nächster konkreter AuftragT070/T075 Requestzustandsmatrix als Grundlage für
Cancel/Stop/Close und Retention; Owner ausführende Gesamtimplementierungssitzung.
9fdbda7 auf Windows/Ubuntu erfolgreich: quality40872437853,Guard40872437848.

## T118: wirksame Factory-/Runtimeoptionen vor Recovery prüfen

Architektur vor Portsignatur/Code architecture/2026-10-29-bootstrap-options.md,
Ausgang9e4dfa0. bootstrap-options-final-red:10 fachliche SQL-Redfälle, jeweils gültige
abweichende Retention/Limits/Fristen wurden ohne Ausnahme akzeptiert; zwei übereinstimmende
Reopen-/Ablagefälle schon grün. bootstrap-preparation-red: ein fachlich roter Vertrag,
Runtime-Startup beendete sich ohne die ausstehende Optionsprüfung abzuwarten. Der zuerst
kleinere tatsächliche Satzbootstrap-options-red hatte7 rote/2 grüne Fälle. Keine Compiler-
oder Infrastrukturfehler als Red verwendet.

Green: bestehendes IStoreBootstrap um passive Optionsprüfung ergänzt; vor Store.Start/
Recovery/Workers verbindlich awaited und durch Callertoken begrenzt. SQLite prüft das
vollständige immutable Record einschließlich Retention, normalisiert nur den durch Lease
bestätigten Ablagebezug. Kein stilles Überschreiben, keine Daten-/Schemaänderung bei
Abweisung; Queue und native Identität bleiben erhalten, null Sends, fehlgeschlagene
Vorbereitung entsorgt keine hosteigene Lease. Explizites OpenExisting mit neuem überein-
stimmendem Factory-/Runtimerecord bleibt erlaubt. Direkter Storeconsumer benutzt seine
bereits validierten Factoryoptionen. Kontrollierter Store hat keine eigene Factorypolicy
und sichert den awaitbaren Startupvertrag, nicht den SQLgleichheitsbeleg.

Vollständig lokal grün artifacts/quality/linux/fd5ba5660eef44b09dd2251bcbb3e9d7,
SourceHash8284d6752f0fd302db4a04a42e549c25cc18e7f4be54773e63b5900da6579767.
648 Tests (283 Unit,202 Contract,162 Integration,1 Plattform), null Fehler/Skips;
2236/2309 Produktzeilen=96,8385 %. Codemetr294 Dateien/300 Typen, null Verstöße/
Parsefehler/Skips. Logs artifacts/tdd/bootstrap-options-{red,final-red,quality}.log;
Fullbefehl run-capped.sh plus Format/Guard/Invoke-Quality. Qualitätslog enthält zusätzlich
12 gezielte SQL- und4 Vertragsfälle, die nicht zur vollständigen Testzahl addiert werden.
NativeCI des neuen Standes vor Taskabnahme abwarten; T064 darf anschließend fortfahren.

Der vorherige Close-/Stop-Sammelcommit9e4dfa0 ist inzwischen auf Windows/Ubuntu grün:
quality40882554552 und codemetr-guard40882554553. Damit ist der reale Windowsbefund von
42fc164 am korrigierten Kopf nachgeprüft, keine Wiederholung des alten roten Laufs als
neuer Codebeleg ausgegeben. Gesamte Produkt-/Zielhost-/Livefreigabe bleibt offen.

## T054/T064: ausdrückliche lokale Retention und Löschung

Architektur vor Code architecture/2026-10-29-local-retention.md, Ausgang6b0ed03.
retention-final-red: 3/5 Unitfälle fachlich rot an der ausführbaren reinen Signatur
(zulässiger expliziter Modus und inklusive Fristgrenze), zwei ablehnende Fälle schon grün.
8/8 echte SQLfälle fachlich rot am Delete-Stub. Vorheriger erster Durchlauf hatte eine
unbenutzte using-Direktive im neuen SQLtest; dessen Analyzerfehler ist kein Rednachweis.

Green: SessionRetentionPolicy/SessionDeletion und schmale ISessionRetentionStore-Facette.
Nur Closed mit ClosedUtc, ohne aktuelle aktive/ungewisse Arbeit und mit erfüllter expliziter
Regel. Frist wird einschließlich exaktem Tick und verschiedenen UTCoffset-Schreibweisen
geprüft; Subtraktion vermeidet Overflow langer zulässiger Fristen. Keine automatische
Löschung, auch nicht beim Reopen einer bereits abgelaufenen Sitzung. Erst ausdrücklicher
Aufruf. Gemeinsame Regelgültigkeit aus Contracts.RetentionPolicy.IsValid statt doppelter
Optionsvalidierung; 53 Optionsfälle vor reinem Refactoring grün, danach58 Options-/Retention-
Unitfälle grün. Erstes komplettes Formatgate fand einen privaten Testfeldnamen ohne Präfix;
mechanisch korrigiert, keine fachliche Fehlfarbe behauptet.

SQLite prüft Eligibility und löscht Decisions/Cases/History/Results/Reservations/Requests/
Session in FKkonformer Reihenfolge innerhalb einer Transaktion. Test löscht eine geschlossene
Session mit aufgelöster Unknown-Historie und Entscheidung; eine unabhängige Session mit
vollständigem eigenem Graphen und eigener Entscheidung bleibt in allen sechs Tabellen
vorhanden und öffentlich lesbar. Originale IDs nach Reopen erhalten, gelöschte IDs null/
NotFound. Ein echter Fehler nach ausgeführten Kindlöschungen rollt alle Zeilen zurück.
Caller-Cancel vor Write ohne Änderung, junge7-Tage-Regel blockiert, abgelaufene kurze Regel
und expliziter Modus erlauben. Sogar manipuliertes Closed mit Running/Unknown hält Daten/
Reservation. Keine Provider-/Backup-/physische Erasurebehauptung. Zusätzlicher sofort grüner
Vertragsfall prüft generische Runtime-Close/Deletefacetten mit ausdrücklich gesetzter
Hostpolicy; kein weiterer Redzyklus. Kontrollierte Projektion einschließlich Scope für
Entscheidungs-/Dispatchhistorie angepasst, kein Teststub verbleibt.

Vollständig lokal grün artifacts/quality/linux/a5b4381b7f10494b8b60896e1840bca4,
SourceHash983387180a750b9087fc3f4c821eff4ed78ae7c13927da909a44ed8414e5f369.
662 Tests (288 Unit,203 Contract,170 Integration,1 Plattform), null Fehler/Skips;
2271/2343 Produktzeilen=96,9270 %. Codemetr302 Dateien/308 Typen, null Verstöße/
Parsefehler/Skips. Logs artifacts/tdd/retention-{final-red,green,quality}.log und
retention-options-pre-refactor.log; Fullbefehl run-capped.sh plus Format/Guard/Invoke-Quality.
NativeCI des neuen Stands noch erforderlich; T054 verbleiben echte Read-only-/Disk-full-
Proben und T065/T068/T069 die vollständige Speicherauditmatrix. T064 bis zugehöriger Abnahme
weiter offen. Löschläufe ausschließlich in selbst erzeugten privaten Testfixtures.

T118 jetzt abgeschlossen: derselbe Optionscommit6b0ed039d56763159ab3815a654c3f6a5573ffda
ist nativ Windows/Ubuntu grün (quality40883956319, Guard40883956192). Damit ist die konkrete
Optionsabhängigkeit vor Retentionabnahme erledigt; tasks.md entsprechend markiert.

## T054/T065: tatsächliche Read-only- und Disk-full-Regressionsproben

Architektur vor Host-/Testcode architecture/2026-10-29-storage-native-faults.md,
Ausgangf838da6. ReadOnlyStorageTests verwendet eine vollständig neu geöffnete Verbindung
auf OSgeschützter eigener Datei und verifiziert zuerst tatsächliche Verweigerung eines
neuen Schreibhandles. Kooperatives vorhandenes Lease für existierende Datei, keine native
Ownershipbehauptung. Nach Modusrücksetzung gleicher DBhash, ursprünglicher Erfolg und
Historie erhalten. Tatsächlich erster ausführbarer Lauf grün; kein künstlicher Rednachweis.
Ein vorausgehender falscher Testenumname Accepted statt Committed war nur Compilerfehler.

DiskFullStorageTests startet getrennten Hostprozess mit eigener Fixturekopie. Linux nutzt
rootlose User-/Mountnamespace, private Propagation und tmpfs8MiB; stat/.NETDriveInfo wurden
vorher an1MiB gemessen. Windowsdriver bereitet ausschließlich eigene64MiB-VHD im privaten
.agenttemp-Root vor, wählt freien temporären Buchstaben, prüft Label/Größe und detachiert
nur diese Datei. Keine vorhandene Disk wird ausgewählt. Dateifüller prüft Kapazität und
unabhängiges128MiB-Limit, nur tatsächliche plattformspezifische no-space-Codes gelten;
Windows39/112 gemäß MicrosoftWinError.h, Linux28 lokal beobachtet. Quelle
https://learn.microsoft.com/en-us/windows/win32/debug/system-error-codes--0-499-.
Marker/Nonce, endliche Prozesse, Cleanup; keine Triggernachricht als FULL und keine
produktive Testbackdoor/Reflection. Unter vollem Dateisystem Submit über öffentliche
Runtime; anschließend Füller entfernen, vorheriges Result/Historie und fehlenden Kandidaten
prüfen, null zusätzliche Provider-Sends. Öffentliche Fehler nur stabile Kategorie.

Tatsächlich erster ausführbarer Linux-Disk-full-Fall ebenfalls grün. Report im finalen
Integration-TRX: MeasuredUtc2026-10-29T14:32:23.2356917Z, FullCode28, FilledBytes8273920,
Stage verify, Verifiedtrue, StorageUnavailable/NotAccepted. Vorheriger erster ausführbarer
Report14:26:33.7403242Z zeigte dasselbe. Produktive generische Fehlerlogik blieb unverändert;
zusätzliche Regression statt nachträglich behauptetem TDD-Red. Ein Analyzerbefund bei einem
neuen konstanten Testarray vor Ausführung wurde durch gemeinsame benannte OS-Code-Regel
behoben, kein fachliches Red. Windows ist bis eigener CIausführung ausdrücklich NOT RUN.

Vor reinem gemeinsamen Hostpfadrefactoring17 bestehende Prozess-/Crashfälle grün, danach
vollständige Regression. Vollständig lokal grün
artifacts/quality/linux/84e356af705e4681bdfcd3aec1f9a23e,
SourceHashc94015980f5fc7836d679ce69eeda825cc089d8f4c0e8c18f1bff33bed197eb5.
664 Tests (288 Unit,203 Contract,172 Integration,1 Plattform), null Fehler/Skips;
Produktabdeckung unverändert2271/2343=96,9270 %. Codemetr313 Dateien/322 Typen, null
Verstöße/Parsefehler/Skips, einschließlich aller neuen Host-/Fixtureklassen. Logs unter
artifacts/tdd/read-only-observed.log, storage-native-observed.log, native-fault-quality.log.
Fullbefehl run-capped.sh plus Format/Guard/Invoke-Quality. Privileg-/Namespace-/VHDfehler
werden nicht als PASS/Skip umgedeutet. T054/T065 behalten native Nachprüfung/Matrixaudit;
T107 prüft ursprüngliche TDDabdeckung, zusätzliche grüne Proben ersetzen keine fehlenden
historischen Redbelege. Retentioncommitf838da6 inzwischen Windows/Ubuntu grün:
quality40887145758, Guard40887146227; T064 bleibt bis zugehörigem Speicheraudit offen.

## T065/T075 Requestdatenprüfung vor Start-Recovery

Architektur vor Produktcode: architecture/2026-10-29-request-storage-consistency.md mit
Quellenhashes zu08a4cab. request-consistency-red.log zeigt20/20 tatsächlich ausgeführte
rote Fälle: gültiges Schema akzeptiert ungültige gespeicherte State-/Certaintypaare,
malformed/fehlende JSONfelder und Snapshot-/Spaltendrift. Erwartete bereinigte Ablehnung
fehlte; keine Compilerfehler als Red. Nach Streamingprüfung im selben Recoverytransaction
VOR jeder Mutation request-consistency-green20/20grün. Sechs zusätzliche unmittelbar
grüne Terminal-/Runningzeitstempel-/UTCregressionen, kein rückwirkend erfundenes Red.

RequestStateMachine bleibt die gemeinsame Zustandsregel. RequestId/SessionId/Sequence,
State/Certainty, Accepted/Started/Ended/CancellationUtc und lokale Resultfinalisierung
werden vor Replay-/Recoveryentscheidungen abgeglichen. UTCformat exakt/invariant, keine
Annahme monotoner Wallclock. Inkonsistente/malformed Daten melden StorageUnavailable;
DBhash bleibt nach fehlgeschlagenem Start unverändert. Kein Ersatzstore, keine stillen
Datenkorrekturen oder Verlust früherer bestätigter Inhalte. Reader bleibt speicherbegrenzt;
keine zusätzliche ganze Requestliste. Session-/Result-/History-/Casegraphvalidierung ist
noch separat in T065 zu bearbeiten; kein vollständiger Korruptionsschutz behauptet.

## T065 Sitzungsprojektionen vor Recovery

Architektur mit gemessenen Quellen vor Code: architecture/2026-10-29-session-storage-consistency.md.
session-consistency-red.log22/22 echte rote Fälle gegen bestehenden Startpfad: ungeprüfte
Open-Snapshots akzeptieren Identitäts-/Provider-/Profil-/Modell-/Bindungs-/State-/UTCdrift,
malformed/missing JSON, widersprüchliche ClosedUtc und falsches NextSequence. Nach eigener
Streamingprüfung im selben Starttransaction session-consistency-green48/48, einschließlich
26unveränderter Requestschutzfälle. Gemeinsamen UTCtextvergleich ohne Verhaltensänderung
in StoredUtcRepresentation herausgezogen. Fehler bereinigt und Datenbankhash unverändert;
keine Wiederherstellung durch Überschreiben vorhandener Daten.

Bindungsformat ist positive opake Adapterversion, keine Herstellerweiche/Format1annahme;
lazy Root mit Binding aber ohne native SessionReference bleibt gültig. ClosedUtc ist genau
für Closed gesetzt, Wallclockreihenfolge nicht erzwungen. NextSequence muss höchster noch
persistierter Requestsequence plus1 entsprechen. Result-/History-/Recoverycasegraph und
vollständiger Close-/Stopraceaudit bleiben ausdrücklich offene Schritte in T065/T079/T083;
dieser Nachweis schließt sie nicht stillschweigend aus dem Gesamtauftrag aus.

## T065 Result-/Historygraph

Architektur zuerst: architecture/2026-10-29-result-storage-consistency.md mit Quellenhashes.
result-consistency-red.log20Fälle tatsächlich ausgeführt,19rot/1grün: fehlende/zusätzliche
Finalzeilen, ersetzte Ein-/Ausgabe, malformed JSON, fremde Historyidentität, falsche
Completionzeit/Ordinalgaps wurden bislang beim Start akzeptiert. Legitime späte
ConfirmEnded-Klärung mit unveränderter Unknownhistory war schon grün. Unnötiges using
zuvor entfernt; dessen Analyzerfehler ist kein Red. Nach Relation-/Joinprüfung
result-consistency-green68/68Storagefälle grün. Relationen zählen History einmal gruppiert
statt pro Request ohne vorhandenen RequestId-Index die ganze History erneut zu scannen;
vorheriger grüner68-Fallstand als Refactoringregression erhalten, keine Schemaänderung.

Je Terminalrequest ein Result/History, kein Finaldatensatz für Queued/Running. CurrentResult
und Results exakt gleich; History darf ausschließlich den später bestätigten Unknown-
Endbeleg historisch behalten, niemals Outcome/Input/Output ändern. HistoryOrdinal folgt
Finalisierungsreihenfolge, nicht Sequence. UTC und IDs stimmen mit Spalten/Request überein.
Fehler vor Mutation, DBhash gleich. Kein kryptographischer Manipulationsschutz oder
vollständiger Submission-/ReservedDispatch-/Reservation-/Recoverycasegraph behauptet;
diese bleiben konkrete nächste Schritte desselben T065-Auftrags.

## T065 unveränderte gespeicherte Einreichungen

Architektur vor Code: architecture/2026-10-29-submission-storage-consistency.md.
submission-consistency-red.log8rot/1grün bei9tatsächlich ausgeführten Fällen:
Queuedsubmission mit fremdem Owner, leerem/nullInput, malformed JSON, ungültigen
Timeouts und fremdem Schreibworkspace wurden vor Start nicht abgewiesen. Bereits
angenommene2000Byte mit späterem Hostlimit1Byte blieben schon korrekt reservierbar.
Danach gemeinsamer ValidateFrozen-Schnitt für bestehende Input-/Deadline-/Workspace-
regeln, Lifecycleprüfung nur bei neuer Annahme. Streamingprüfung vor Recoverywrite,
keine neue Kürzung/Bytelimitanwendung auf historischen Inhalt. Quellenprüfung fand
nebenbei UTF8Defaultreplacement für unpaarige UTF16-Surrogate; als konkreten Folgeauftrag
T119 mit Owner/Abhängigkeit/Schritten/Abnahme festgehalten, nicht stillschweigend
als strikte Kodierung bezeichnet. FrozenDispatch-/Reservation-/Recoverycasegraph bleibt
nächster benannter Schritt in T065, kein Abschluss behauptet.

## T065 eingefrorene Dispatch-/Reservierungsbeziehungen

Architektur/Quellen vor Erweiterung: architecture/2026-10-29-dispatch-storage-consistency.md.
dispatch-consistency-red.log40/40 tatsächlich rot, je Drift bei Unknown und bekannt
freigegebenem Dispatch: gefrorene IDs/Sequenz/Input/Modell/Profil/Settings/Revision,
Ackreferenzen sowie SQLworkspace/-zeit/-fehlmetadaten/-verschwundene Reservation.
Besonders Unknown mit fehlender oder freigegebener Reservation wurde bisher akzeptiert.
Nach Streamingprüfung dispatch-consistency-green117/117Storagefälle grün.

Dispatchfelder müssen vollständig mit genau passender Reservation zusammenhängen;
Unknown braucht unreleased Row. Immutable Context-/Settingsvergleiche ordinal als
Schlüssel-/Wertmenge, ursprüngliche Submission exakt gleich. Aktuelle bestätigte
Bindungsrevision darf höher sein, alte ContinuationHeads bleiben unverändert. Native
Sessionidentität/acknowledgedRefs konsistent, UTC/StartedUtc/ReservedUtc und ReleaseReason
passen. Archivprüfung erteilt keinerlei Sendrecht; aktiver öffentliche Read bleibt
Running/neu reserviert. Keine neue Herstellerwahl/IDs/Replay. DBhash nach Ablehnung gleich.
Recoverycase-/Decisiongraph bleibt konkret nächster T065-Schritt.

## T065 Recoverycase-/Decisiongraph

Architektur/Quellen vor Code: architecture/2026-10-29-case-storage-consistency.md.
case-consistency-red38/38 und decision-consistency-red12/12 tatsächlich rot: beide Targets
wurden mit fremden Links/nativeEvidence/Status/Zeiten, fehlenden Cases/Blocks und unechten/
fehlenden Entscheidungen beim Start akzeptiert. Nach Case-/Decisionprüfung
case-decision-consistency-green167/167Storagefälle grün. Zwei weitere unmittelbar grüne
Regressionen halten mehrfache Closeentscheidungen plus Bestätigung mit rückläufigen
kanonischen UTC-Messdaten sowie historischen Openingkopf nach späterem Turn zulässig.

Cases sind zu Request-/Dispatch-/Ack-/Ownerdaten gebunden, Unknown braucht offenen Case
und unreleased Slot. Resolved bestätigt aktuelle Gewissheit, ohne Historienerfolg zu erfinden.
Openingcase hat keine DummyIDs und darf historische Continuation behalten. DecisionIDs/
Target/Revisionsbereich/Kind/Erklärung/EvidenceRef/UTC geprüft, je Target letzte passende
ConfirmEnded-Bestätigung für Resolvedstatus. Streaminggruppen statt alle Decisions zu laden,
keine zeitliche Wallclocksortierung als Kausalbeweis. SQLrelationen vermeiden wiederholte
ungeindexierte Vollscans. DBhash nach Ablehnung gleich, kein neues Provider-I/O oder Replay.
Startupcancel bei langen Scans bleibt im Fristaudit T076 zu prüfen; vollständiger T065-
Abgleich/Closingwriterreview und native Besitzabnahme bleiben eigenständig offen.

## T067 Kultur-/UTC-Roundtrips

Architektur/Quellen/Primärdokumentation vor Code: architecture/2026-10-29-serialization-culture.md.
serialization-culture-red.log6/6 tatsächlich rot: direkte akzeptierte Sessiondaten
behalten in JSON lokale Offsets statt UTC, obwohl Spalten bereits UTC tragen. Winter/
Sommer Berlin/NewYork/Istanbul unter de-DE/en-US/tr-TR jeweils explizit echte Zonendaten,
keine globale Hostzeitzonenänderung. Neuer speicherspezifischer DateTimeOffsetconverter
normalisiert ausschließlich neue Writes auf UTC bei gleichem Instant; Read bewahrt
ursprünglichen Offset zur weiterhin strikten Erkennung inkonsistenter Altdateien.
serialization-culture-green175/175Kultur-/Storage-/Recoveryfälle grün.

Originale opaque Provider-/Profil-/Modell-/NativeIDs und I/İ/ı/i-Settings, vollständige
Inputs/Outputs/History, Metadata-Store-/Trustscope-ID sowie Cursor über Kulturwechsel
und echtes SQLite-Reopen erhalten. Datumsähnliche Settingstrings unverändert. Negativer
NonUTC-Tamperfall bleibt rot im erwarteten Fehlerzweig, keine stille Altformatmigration.
Native Kultur-/ICUprüfung dieses Schnitts noch erforderlich; Taskmarkierung erst danach.
