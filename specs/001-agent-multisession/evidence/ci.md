# Native CI und erforderliche Checks

Aktueller technischer Kandidat nach T125/T126: **fb1a167 / Source d51bf9f9...**,
native Gesamt-/Paket-/Guard-/Auditprüfungen erfolgreich. Verbindlicher aktueller
Index mit vollständigem Sourcehash, ausgewähltem Paketartefakt, Laufnummern und
Abnahmegrenzen: [delivery-candidate.md](delivery-candidate.md),
[build-native.json](build-native.json), [canonical-package-final.json](canonical-package-final.json).
Die älteren Zahlen/Quellstände in den folgenden Abschnitten bleiben historische
Checkpoints; sie ersetzen diesen aktuellen Bericht nicht. Die reale finale Stunde
und fehlenden externen Freigaben bleiben separat ausgewiesen.

## Aktueller geprüfter technischer Kandidat

Abgleich2026-10-29T23:00:40Z: Commit7489450, Sourcehash
`dba419f35bf40fd7282a5b2f3bfe7c685576e2c62ceb570d5fec2d0f4f84498b`.
[Quality40968636580](https://github.com/monstertacle/monstertacle.multisession/actions/runs/40968636580),
[Guard40968636533](https://github.com/monstertacle/monstertacle.multisession/actions/runs/40968636533)
und [separater Audit40969224192](https://github.com/monstertacle/monstertacle.multisession/actions/runs/40969224192)
sind erfolgreich. Alle vier Quality-/Kanonconsumerjobs und beide Guardjobs tatsächlich
grün. Unabhängig gelesene native TRX/Coverage/Audit-/14-Szenarioberichte:
[build-native.json](build-native.json). Gleiche sechs Archive beim Produzenten und
beiden echten PackageReference-Konsumenten: [canonical-package-final.json](canonical-package-final.json).

Nach erfolgreichem Kanonconsumercheckpoint40966982840 wurden zusätzlich
`canonical-consumer (ubuntu-24.04)` und `canonical-consumer (windows-latest)`
als Required Checks eingetragen. Readback in build-native.json bestätigt nun
sechs GitHub-Actions-Checks, strict=true/enforce_admins=true, keine Forcepushes
oder Löschung. Vorige vier Checks bleiben erhalten. PATCH nur der
required_status_checks-Ressource; keine andere Protection entfernt.
Rohkopien artifacts/tdd/canonical-required-checks-{request,response}.json,
final-required-checks-readback.json. Der folgende Bericht bleibt historischer
Checkpoint. Aktuelle Stundenmatrix separat soak.md; keine Produktfreigabe.

## Historischer Checkpoint

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
