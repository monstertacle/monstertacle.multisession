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
