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
| Q-01 Codemetr | Tool/Regeln genehmigt; FIFO-/Reservierungs-Arbeitsstand PASS (73 Typen); finale Lieferstandprüfung NOT RUN |
| Q-02 Build/Audit/Coverage | Lokaler Windowsrunner PASS: Build/Format/177 .NET-Tests/Audit und 88,2576 % Coverage bei genehmigtem Mindestwert 87 %; Linux/CI NOT RUN |
| Q-03 Plattform-/Crash-Verhalten | NOT RUN; keine produktiven Plattform-/Persistenzimplementierungen |
| Q-04 Live-Provider | NOT RUN; keine Kosten-/Kontofreigabe, keine Liveaufrufe |
| Q-05 Paket-/Zielhostkonsum | NOT RUN; Quellanalyse ist kein Konsumtest |
| Q-06 TDD | Tatsächliche Foundation-/Session-/Admission-/Ausgabegrenzenzyklen vorhanden; initiale Manifestlücke und abschließender Audit offen; siehe tdd-foundation.md / tdd-us1.md / tdd-us2.md |

Verantwortlich für Review und offene Freigabeentscheidungen ist der Projektverantwortliche.
Er bestätigt auch das ursprüngliche Ratifikationsdatum; bis dahin bleibt
`TODO(RATIFICATION_DATE)` unverändert. Keine Datumsannahme aus einem Implementierungslauf.
