# Native CI und erforderliche Checks

Statusabgleich gemessen am 2026-10-29T21:21:56Z. Zwischenstand, keine finale Freigabe.

Für Commit `7e336511489ecda95846c4b7ac40f3f98d672fb8` sind
[quality 40954320723](https://github.com/monstertacle/monstertacle.multisession/actions/runs/40954320723)
und [codemetr-guard 40954320725](https://github.com/monstertacle/monstertacle.multisession/actions/runs/40954320725)
auf beiden nativen Runnern erfolgreich. Der angeforderte Stundenkandidat hat Sourcehash
`322c7a7ca2900d834776cd2da5026900f2093a2dfbf1126b4ec7fc99270c1454`.
Quality führt Locked-Restore, Releasebuild, Format/Analyzer, alle vier Testsuiten,
transitiven NuGetaudit und mindestens 87 % gesamte native Produktcoverage aus.
Anschließend werden sechs lokale NuGets gebaut und von einem frischen externen
Konsumenten verwendet; der veröffentlichte native Host führt 14 endliche Szenarien
außerhalb des Quellbaums aus. Berichte und Pakete liegen als Laufartefakte vor.
Der getrennte Fullguard umfasst ausdrücklich eigene Tests und ValidationHost.

## Durchgesetzte Branchchecks

Beim Abgleich fehlten sowohl klassische Protection als auch Rulesets. Im Rahmen
T111 wurde `main` über die GitHub-API geschützt und der gespeicherte Zustand erneut
abgerufen: strict=true, enforce_admins=true, keine Forcepushes/Branchlöschung.
Erforderliche Checks, jeweils an GitHub Actions (App-ID 15368) gebunden:

- `quality (ubuntu-24.04)`
- `quality (windows-latest)`
- `guard (ubuntu-24.04)`
- `guard (windows-latest)`

Befehle: `gh api --method PUT repos/monstertacle/monstertacle.multisession/branches/main/protection --input <request.json>`
und anschließendes GET derselben Ressource, beide Exit 0. Exakte Request-/Responsekopien
unter `artifacts/tdd/required-checks-{request,response}.json`. Der verpflichtende Audit
ist Bestandteil beider quality-Jobs; der zusätzliche Workflow dependency-audit ist
zeitgesteuert/manuell, daher kein bei PRs unerfüllbarer zusätzlicher Checkname.
Keine Veröffentlichung, kein Merge und keine Zielhostmigration ausgeführt.

## Offene Abnahme

Die nachfolgende Stopcallback-Änderung benötigt eigene native Läufe. Die genannten
Ergebnisse werden deshalb nicht als ihr Nachweis verwendet. T104/T111 werden erst
im finalen Abgleich abgeschlossen. T111 besitzt damit eine tatsächlich konfigurierte
Mergebarriere, aber noch keine pauschale Zusage für einen künftigen Lieferstand.
