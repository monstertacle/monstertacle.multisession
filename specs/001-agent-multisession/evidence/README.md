# Nachweisschema und aktueller Status

## Aktueller Abnahmeindex

Abgleich2026-10-29T23:04:34Z: technischer Kandidat7489450 / Source
`dba419f35bf40fd7282a5b2f3bfe7c685576e2c62ceb570d5fec2d0f4f84498b`.
Vollständige native CI auf Ubuntu/Server2025 erfolgreich,1036/1042Tests,
97,5796/97,5325 % Produktzeilen; [build-native.json](build-native.json),
[build-audit.md](build-audit.md), [ci.md](ci.md), [coverage.md](coverage.md).
Gleiche sechs NuGets in beiden Konsumenten: [canonical-package-final.json](canonical-package-final.json).
Guard440Dateien/458Typen ohne Regelverstöße, semantische Toolgrenzen ausdrücklich
in [codemetr-results.md](codemetr-results.md), Refactoring in [refactoring.md](refactoring.md).
Native Leases/Mounts: [platforms.md](platforms.md), [linux-mount-final.json](linux-mount-final.json).
Tatsächliche ältere Stunden: [soak.md](soak.md)/[hour-checkpoint.json](hour-checkpoint.json);
neue finale Stunden laufen separat, kein vorzeitiges PASS.

Abnahme bleibt gesperrt: [release.md](release.md) enthält FR001–027/SC001–011/Q01–06,
[remaining-acceptance.md](remaining-acceptance.md) konkrete offene Owner-/Folgeaufträge.
Windows11, Livefreigabe/-konten, fremder Integrator, Gemini-Kontextklassifikation,
historische Q06-Archivlücke und zuständige Distributionsentscheidungen fehlen.
Intermittierender Windows-Recoverybesitz wird als T125 nativ weiter untersucht.
Selbstprüfung [review.md](review.md), ehrlicher historischer [TDD-Audit](tdd-audit.md).
Die folgende ältere Statustabelle ist historisch und wird nicht als aktuelles Gate
verwendet. Chronologische Einzelakten behalten ihre damaligen Quellen/Ergebnisse.

## Ursprüngliches Schema und historische Zwischenstände

Jeder Nachweis nennt Datum, Task und FR-/SC-Bezug, Plattform/Runtime/Toolversion,
Quellstand, genaue Befehle, tatsächliche Exitcodes und Ergebnisse, Messumfang,
Artefaktpfade und offene Befunde. Geheimnisse, Prompts und rohe Providerantworten
gehören nicht in versionierte Nachweise.

- **PASS**: die benannte konkrete Prüfung wurde erfolgreich ausgeführt.
- **FAIL**: die Prüfung wurde ausgeführt und hat ihre Zusicherung nicht erfüllt.
- **NOT RUN**: nicht ausgeführt oder Voraussetzung fehlt; niemals als PASS zählen.
- Designentscheidung, Toolsetup und Produktfreigabe getrennt bewerten.

Ohne Git keinen Commit erfinden. Sourcehash: alle relevanten Quelldateien,
Projektdateien, Paket-Lockfiles und Qualitätskonfigurationen je relativem Pfad
mit SHA256 hashen, nach Pfad sortieren und den UTF-8-Manifesttext mit LF erneut
SHA256-hashen. Den Umfang jedes Manifests dokumentieren. Bei späteren Änderungen
sind alte Ergebnisse nur für den damaligen Stand gültig.

TDD-Zyklen benötigen zusätzlich Testkennung, beobachtete fachliche Assertion,
Red-Befehl/Exitcode/Quellstand vor Green, anschließendes Green und Regression.
Compiler-/Restorefehler und Infrastruktur-Smokes sind kein fachlicher Red-Nachweis.

| Gate | Aktueller Stand |
| --- | --- |
| Q-01 Codemetr | Tool/Regeln genehmigt; Arbeitsstand PASS, lokal 371 Dateien/381 Typen ohne Befunde; finale Lieferstandprüfung offen |
| Q-02 Build/Audit/Coverage | Arbeitsstand PASS: Locked Restore, Build, Format, Tests und Audit lokal sowie native Windows-/Ubuntu-CI; lokal 2804/2880 Produktzeilen = 97,3611 % bei unverändertem Mindestwert 87 %; finale Lieferstandprüfung offen |
| Q-03 Plattform-/Crash-Verhalten | Teilnachweise PASS: SQLite, zehn SC-009-Prozessabbrüche und Crashgrenzen, native Linux-Lease; vollständige Windows-/Linux-Zielsystemmatrix, Windows-Lease und Soaks offen |
| Q-04 Live-Provider | NOT RUN; keine Kosten-/Kontofreigabe, keine Liveaufrufe |
| Q-05 Paket-/Zielhostkonsum | NOT RUN; Quellanalyse ist kein Konsumtest |
| Q-06 TDD | Tatsächliche Foundation-/Session-/Admission-/Ausgabegrenzenzyklen vorhanden; initiale Manifestlücke und abschließender Audit offen; siehe tdd-foundation.md / tdd-us1.md / tdd-us2.md |

Statusabgleich gemessen am `2026-10-29T17:19:21Z`: letzter vollständig nativer
CI-Stand `075d4e2`, [Qualität auf Windows und Ubuntu](https://github.com/monstertacle/monstertacle.multisession/actions/runs/40919459445)
und [Guard](https://github.com/monstertacle/monstertacle.multisession/actions/runs/40919459473)
erfolgreich. Lokaler Gesamtbericht `artifacts/quality/linux/6b0105a766e14ce2b7dca0b491734444/`,
Sourcehash `a2bdf00708ec3515dc2de1e9a64ebd7bef7ad4294a753623663de6931974af21`.
Weitere aktuelle Zyklen stehen in `implementation-progress.md`, `tdd-us3.md`,
`tdd-us4.md` und `tdd-us5.md`; spätere Arbeitsstände brauchen eigene Prüfungen.
Historische Einzelberichte gelten weiterhin nur für ihren jeweiligen Quellstand.

Verantwortlich für Review und offene Freigabeentscheidungen ist der Projektverantwortliche.
Er bestätigt auch das ursprüngliche Ratifikationsdatum; bis dahin bleibt
`TODO(RATIFICATION_DATE)` unverändert. Keine Datumsannahme aus einem Implementierungslauf.
