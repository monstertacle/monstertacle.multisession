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
