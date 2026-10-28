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
