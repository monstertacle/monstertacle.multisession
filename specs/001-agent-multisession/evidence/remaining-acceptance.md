# Konkreter Folgeplan für verbleibende Abnahme

Erstellt 2026-10-29T22:50:59Z, gemessene UTC. Elternauftrag: gesamte Implementierung
001-agent-multisession. **Status: Abnahme blockiert**, keine Produktfreigabe.
Technische Umsetzung darf auf ihrem Topicbranch bleiben; kein NuGet-Push, Merge,
Deployment oder SharpClaw-Umbau. Bereits behobene technische Befunde stehen mit
Prüfung in acceptance-audit.md und den TDD-Akten. Diese Akte bündelt ausschließlich
die tatsächlich übernommenen noch offenen Befunde.

| Task / Ursprung | Owner / Readiness | Begrenzter nächster Schritt | Abnahme / Abhängigkeit |
| --- | --- | --- | --- |
| T034: eindeutiger nativer Gemini-Kontextfehler fehlt | Projektverantwortlicher für zulässige Testdaten/Freigabe; ausführende Sitzung für Adapter; blockiert durch fehlenden dokumentierten Fehlercode | Vorhandene Klassifikation beibehalten; offizielle maschinenlesbare Kategorie oder nach T105 genehmigte native Probe sichern; nur bei eindeutigem Signal zuerst fachlich roten Test ergänzen und minimal klassifizieren | ProviderContextLimit ohne Textheuristik/Retry/Compaction; alternativ ausdrücklich zuständige, begrenzte Änderung der Abnahme. Allgemeines INVALID_ARGUMENT/OUT_OF_RANGE allein reicht nicht |
| T087/T093/T099/T100: Windows 11 25H2 fehlt | Projektverantwortlicher stellt Host, ausführende Sitzung führt aus; Host fehlt | Gepinnten finalen Stand auf echtem x64-Host mit .NET10 bereitstellen; unveränderte Quality-, PackageSmoke-, Publish-/14-Szenario- und Stundenbefehle ausführen | Gleicher Source-/kanonischer Paketstand, echte Rechte/Prozess-/Kill-/Unknown-Belege, 87-%-Gate und monotone 60-Minutenreceipts. Server 2025 ersetzt Windows 11 nicht |
| T100: vollständige finale Stundenmatrix | Verfügbare Ubuntu-/Serverstunden durch ausführende Sitzung erledigt; Projektverantwortlicher stellt Win11 | hour-final.json belegt beide tatsächlich abgeschlossenen Stunden des eingefrorenen Kandidaten. Auf bereitgestelltem Win11 denselben Source-/Paketstand und unveränderten Soak ausführen | SC-007 bleibt bis zum dritten Ziel offen; je60 eindeutige Receiptfenster/Originale und sichere Endzustände nach Reopen. Kein Server-als-Client-Ersatz |
| T105/Q-04: keine Livefreigabe/Konten | Projektverantwortlicher; Freigabe fehlt | Kostenobergrenze, Konten, Endpoint/Modell, erlaubte Daten/Retention ausdrücklich bestätigen; erst dann ausführbare Liveprobe vervollständigen und native Matrix messen | Zwei Hersteller, neue/erste/Folgeanfrage, Neustart/Fortsetzung, Status/Cancel auf allen drei Zielen; live-providers.md |
| T106/SC-006: unabhängiger Integrator fehlt | Projektverantwortlicher benennt Person; Voraussetzung fehlt | Gleiche Pakete und README/docs/embedding samt Zugang bereitstellen; monotone Uhr erst nach Voraussetzungen starten | Fremde Person schafft Paketbezug/erste/Folgeanfrage in höchstens30Minuten; tatsächlicher Bericht, integrator.md |
| T123/Q-06: initiale volle Redquellarchive fehlen | Projektverantwortlicher entscheidet; ausführende Sitzung inventarisiert; Audit ausgeführt, Entscheidung offen | Vorhandene Originalarchive/Transkripte mit Ursprung prüfen. Nur tatsächlich erhaltene Quellen/Zeiten zuordnen. Wenn nicht wiederherstellbar: begrenzten historischen Befund samt betroffenen Tests, Risiko, Verantwortlichem, Ablauf-/Prüftermin und Folgeauftrag ausdrücklich entscheiden | Keine Rekonstruktion als damaliger Lauf, kein nachträgliches Green als Red. Vor T110 dokumentierte Entscheidung; tdd-audit.md ist keine selbst erteilte Ausnahme |
| T104/T108: DEP-001 und Distributionslizenz | Projektverantwortlicher; Entscheidung offen | Bekannte Test-only-Legacypins ausdrücklich beibehalten oder gesonderte Migration beauftragen; Produktlizenz und erforderliche Notices für Distribution festlegen | Zuständige Entscheidung vor erster Freigabe, keine erfundene MIT-Lizenz, keine stillen Paketupgrades; dependencies.md |
| T110: finale Bewertung | Projektverantwortlicher/benannter Reviewer; abhängig von vorigen Abnahmen | release.md mit tatsächlichen Proof-/Source-/Paketbelegen prüfen; offene P0/P1-Abnahmeblocker schließen | Schriftliches Urteil zu FR001–027, SC001–011 und Q01–06, keine automatische Freigabe durch diese Sitzung |
| T125: native Exitwartebarriere | Erledigt, ausführende Sitzung | Native CLI und neue Kernelassertions zuerst rot; endlicher BCL-Kernelwait im Abnahmegerüst | windows-process-exit-regression-red.json / windows-process-exit-green.json: 600 Kills/Reopens, 1200 positive Signale, vollständige native Gesamtprüfungen grün |
| T126: unabhängige I/O-Testkollektionen | Erledigt, ausführende Sitzung | Assemblykonfiguration isoliert echte I/O-Fixtures; sämtliche Produktfristen und inneren Nebenläufigkeitstests unverändert | native-io-before.json / native-io-linux-after.json / native-io-windows-after.json: vorher max4, danach max1 Klasse; alle405 Fälle auf beiden OS grün |
| Constitution-Ratifikationsdatum | Projektverantwortlicher; historisches Datum unbekannt | Ursprüngliche Quelle ermitteln oder verbleibendes Dokumentations-TODO ausdrücklich bewerten | Kein aus aktuellem Lauf erfundenes Datum; ursprünglicher TODO bleibt sichtbar |

Frist aller Freigabeblocker: **vor T110/erster Freigabe**, ohne erfundenes kalendarisches
Eigentümerdatum. Dieser Plan ist konkret genug zum Übernehmen, aber ersetzt weder
Voraussetzungen noch Zustimmung. Keine pauschale historische Befundinventur beauftragt.

