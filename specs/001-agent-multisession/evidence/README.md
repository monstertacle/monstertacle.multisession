# Nachweisschema und aktueller Status

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
