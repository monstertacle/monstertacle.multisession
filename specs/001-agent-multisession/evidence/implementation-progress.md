# Gesamtimplementierung

Created: 2026-10-29T06:32:23Z
Owner: ausführende Implementierungssitzung.
Branch: `feature/reserved-dispatch-contract`.

Auftrag des Eigentümers: „Bitte autonom mit speckit implementieren. Alles“.
Dieser Auftrag ersetzt die frühere Begrenzung auf einzelne Übergabetasks. Alle offenen
Implementierungstasks aus tasks.md werden nach Abhängigkeiten bearbeitet; nach einzelnen
Tasks nicht mit einer bloßen nächsten Empfehlung aufhören. Speckit-implement und der
projektlokale codemetr-guard sind geladen. Keine parallelen Subagenten beauftragt.

## Aktueller Stand

- T112 und T025 bereits implementiert/committet/gepusht; letzter technischer Anker `67a6d3f`.
  Linux-/Windows-Qualität und Codemetr für diesen Anker auf GitHub erfolgreich.
- T113 lokal abgeschlossen: zehn zusätzliche Recovery-Vertragsfälle nach beobachtetem Red grün;
  Falltargets, lesbare Revision, CAS und Reopen außerhalb der Storetransaktion vorhanden.
  233 .NET-Tests im vollständigen Linuxlauf grün, 95,4936 % Produktcoverage.
  Full-Codemetr 98 Dateien/99 Typen sauber. Nachweis: tdd-us5.md.
- T115 lokal abgeschlossen: 263 .NET-Tests, 96,0821 % Coverage, 110 Dateien/112 Typen
  im sauberen Full-Guard. T113 ist auch auf Linux/Windows-CI grün.
- T114 lokal abgeschlossen: 271 .NET-Tests, 96,2433 % Coverage, 115 Dateien/117 Typen
  im sauberen Full-Guard. Pagingstand aa15e97 auch auf Linux/Windows-CI grün.
- Als Nächstes T026/T027 (öffentliche Runtimekomposition),
  US2-Adapter, US5-SQLite/Recovery, US3-Lebenszyklus/Cancel/Diagnose, US4-Plattform-/
  Paket-/Konsumabnahme und schließlich gesamte Polish-/Freigabematrix.
- T114 muss vor dauerhafter Annahme T058 und zugehörigen Fristen T076 umgesetzt werden.
  Aktuelle Architekturbelege vor jedem neuen Schnitt fortschreiben oder neu anlegen.

## Verbindliche Arbeitsregeln

TDD: Signaturen/Stubs → ausführbarer fachlicher Red → minimales Green → grüne Regression
und Codemetr. Kein Python. SDK 10.0.403 / Codemetr 0.1.0 / 87-Prozent-Scope nicht ändern.
Schwere Läufe über /home/user1/dev/scripts/run-capped.sh. Tempdateien unter .agenttemp.
Erledigte Tasks nur nach tatsächlichen Ergebnissen markieren, Fortschritt committen/pushen.
Keine Rohartefakte/Secrets versionieren; nur eigene überprüfbare Nachweise festhalten.

## Externe Grenzen

Vollständige Implementierung ist beauftragt. Echte kostenpflichtige Herstellerläufe T105
benötigen gemäß Task und Constitution zusätzliche ausdrückliche Kosten-/Speicher-/
Testkontofreigabe, die hier nicht erfunden wird. T106 benötigt einen unabhängigen Integrator.
Windows 11/Server-2025/Ubuntu-24.04-Rechte-/Crash-/Soak-Abnahmen brauchen die tatsächlichen
Hosts. GitHub Windows-/Ubuntu-CI ist verfügbar, aber kein Nachweis aller genannten Hosts.
Branchchecks/finale Freigabe bleiben gesondert zu prüfen. Kein NuGet-Push, Deployment,
SharpClaw-Produktwechsel oder stilles Merge aus dem Implementierungsauftrag ableiten.
Offene externe Gates erst nach konkreter vorbereiteter Abnahme auflösen, während unabhängige
Implementierungsarbeit weitergeht. Die Gesamtimplementierung ist noch nicht abgeschlossen.
