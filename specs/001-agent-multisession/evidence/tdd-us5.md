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
