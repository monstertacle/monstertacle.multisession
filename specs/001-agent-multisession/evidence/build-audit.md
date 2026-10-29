# T104: Build, Coverage und Abhängigkeiten

Prüfstand 2026-10-29T22:50:59Z. Finaler technischer Kandidat nach ABIrefactoring:
Commit 7489450, Sourcehash
`dba419f35bf40fd7282a5b2f3bfe7c685576e2c62ceb570d5fec2d0f4f84498b`.
Native CI läuft separat; diese Akte erklärt ein laufendes Gate nicht für bestanden.

Lokaler Linux-Gesamtlauf `8530362e86bf4cfcaf1669f640cd3f24`, Exit0:
Locked Restore, Releasebuild, Format/Analyzer ohne Warnungen/Fehler,
1036 Tests (359 Unit/241 Contract/405 Integration/31 Plattform), keine Skips.
2943/3016 eigene Produktzeilen =97,5796 %, Mindestwert unverändert87 %.
Zusätzlich 13 Coveragegate-,4 Sourcehash- und8 kanonische Hashprüferfälle.
Befehl `bash ~/dev/scripts/run-capped.sh -- pwsh -NoProfile -File scripts/Invoke-Quality.ps1 -Platform linux`.
SDK10.0.403, Runtime10.0.14. Eigene Windowsbibliothek wird separat nativ geprüft,
nicht aus Linuxcoverage als getestet ausgegeben. Gesamtinventar: sechs Bibliotheken,
vier Tests, ein ValidationHost. Tests/Host zählen nicht zum Coverage-Nenner;
generierte/Fremdquellen nicht als eigener Produktcode, eigener nicht ausgeführter
Code bleibt im Nenner. Details coverage.md.

`dotnet list ... package --vulnerable --include-transitive --no-restore --format json`
hat Exit0 und enthält alle elf Projektpfade; JSON tatsächlich geprüft, keine
gemeldeten direkten/transitiven Advisories. Rohbericht im Qualitätsordner
`dependency-audit.json`. Locked Graph unverändert, Auditmodus all/low, keine
ignorierten Advisories. Das ist eine Momentaufnahme bekannter Meldungen.
Nativer SQLitecheckpoint:3.53.3 auf beiden CI-Systemen. SQLitePCLRaw2.1.13
Apache-2.0 laut restaurierten Nuspecs, sonstige Herkunft/Lizenzen dependencies.md.
Collector10.1.0 und TimeProvider.Testing10.7.0 jeweils MIT, test-only/PrivateAssets;
aktuelle lokale Nuspecs geprüft, keine ungeprüfte Lizenz aus Paketnamen geraten.

Offen vor Freigabe: DEP-001 xunit-v2-Legacypins und Auswahl der eigenen
Distributionslizenz durch Projektverantwortlichen. Keine kritische bekannte
Advisory gemeldet, aber kein Lizenz-/Maintenancefreigabeurteil erfunden.
remaining-acceptance.md weist Owner, nächsten Schritt und Freigabefrist zu.
T104 wird erst mit finaler nativer Prüfung und erledigter Distributionsentscheidung
vollständig geschlossen. Win11 gehört zusätzlich zur vollständigen Q03-Matrix.

Native Ergänzung, unabhängig gelesen2026-10-29T22:57:19.8915539Z:
[build-native.json](build-native.json) bestätigt gleichen Sourcehash auf Ubuntu
und WindowsServer2025,1036/1042Tests ohne Fehler/Skips,97,5796/97,5325 %,
je14 publizierte native Szenarien und erfolgreichen Paket-Smoke. Quality40968636580,
Guard40968636533 und zusätzlicher Audit40969224192 erfolgreich. Lizenz-/DEP-001-
Entscheidung sowie Windows11-/T125-Grenzen bleiben ausdrücklich offen.

