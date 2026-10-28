# Auswahlfehler und optionaler Workspace

Die vor Umsetzung geschriebene Bestandsanalyse, Architekturentscheidung und der
Testplan stehen im Abschnitt „Vorab-Deep-Dive: Auswahlfehler und optionaler Workspace“
in [tdd-us1.md](../tdd-us1.md). Dort stehen konkrete Symbole, fünf geprüfte Dateihashes,
Aufrufer, Kontroll-/Persistenzfluss, Wiederverwendung und Regressionsrisiken.

Dieser Index wurde anschließend am nun vorgeschriebenen Architektur-Nachweispfad
ergänzt; er behauptet keinen rückdatierten Dateianlagezeitpunkt. Der Red-Lauf
`artifacts/tdd/2026-10-28-selection-red/sources.json` enthält den Hash
`6F66EB30AF4071F11F06631EAE310EE90A5C24F8BF8A93D21EC20CA5153D9EB0`
des bereits vorliegenden Analyse-/TDD-Dokuments vor der Produktcodeänderung.

Bezug: Constitution II (aktuell 1.9.0), FR-003/FR-005–FR-007/FR-027,
provider-contract.md §8 und T016/T021 sowie vorgezogene Abweisungsfälle aus T030.
Ergebnis: fünf neue Fälle tatsächlich Red, danach 102/102 Unit-Regression Green;
keine Herstellerverzweigung, kein Fallback, keine Sourceabhängigkeit zum Zielhost.
Die Gesamtfreigabe bleibt wegen Coverage und fehlender externer Abnahmen gesperrt.
