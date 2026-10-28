# Verbindliche Projektregeln

- Kein Python für Produktcode, Tests, Build-/CI-Skripte, Codegenerierung oder
  agentengestützte Projektarbeiten einsetzen, auch nicht für temporäre Hilfsskripte
  oder einmalige Befehle. C#/.NET und bei Bedarf PowerShell beziehungsweise Shellskripte
  verwenden; keine Python-Laufzeit als Projektvoraussetzung einführen.
- Vor Implementierung `.specify/memory/constitution.md`, den Featureplan und dessen
  `tasks.md` lesen. TDD mit tatsächlich beobachtetem fachlichem Red vor Green einhalten.
- Vor jeder Erweiterung zuerst aktuellen Bestandscode per Code-Deep-Dive und
  Architekturanalyse untersuchen: betroffene Module, relevante Aufrufer/Abhängigkeiten,
  Verantwortlichkeiten, Verträge, Daten-/Kontrollflüsse, Zustände/Persistenz und Tests.
  Wiederverwendung, Erweiterungspunkte und Kompatibilitäts-/Regressionsrisiken bewerten.
  Vor Entwurf/Implementierung Quellstand/Sourcehash, konkrete Datei-/Symbolbelege,
  Befunde, offene Fragen, Architekturentscheidung und Testplan im Änderungsnachweis
  dokumentieren. Ohne Nachweis oder bei ungeklärten implementierungsrelevanten
  Architekturfragen nicht implementieren. Auch kleine Erweiterungen benötigen die
  Analyse; alte Analysen am aktuellen Code und Umfang erneut prüfen. Fehlenden
  Bestandscode ausdrücklich benennen, keine Untersuchung vortäuschen.
- Für C#-Änderungen und Refactorings den projektlokalen Skill `codemetr-guard` unter
  `.opencode/skills/codemetr-guard/SKILL.md` laden und befolgen. Falls der Client den
  Skill noch nicht auflistet, die Datei direkt lesen.
- Vom Projektroot den vollständigen Guard ausführen:
  `dotnet tool run codemetr-csharp -- check --format json`.
  Er umfasst eigene C#-Klassen einschließlich Tests und ValidationHost. Nach Refactoring
  erneut prüfen. Fehlende Werkzeuge, Eingabefehler und fehlender Messumfang sind kein PASS.
- `codemetr.json` und Toolpins nicht still ändern; keine Debt-Baseline, Ausschlüsse oder
  gelockerten Grenzwerte zum Verbergen von Befunden erzeugen.
- Vor Freigabe mindestens 87 % Zeilenabdeckung des gesamten eigenen Produktivcodes
  nachweisen, nicht nur geänderte Zeilen. Windows- und Linux-CI müssen das Gate jeweils
  durchsetzen. Für die Messung die native Plattformbibliothek und sämtliche gemeinsamen
  Produktbibliotheken einbeziehen; die jeweils fremde Plattform separat ausweisen und
  zwingend im passenden nativen CI-Lauf prüfen. Testcode zählt nicht zum Coverage-Nenner.
- Skills und lokale Erfolge ersetzen keine erforderlichen CI-/Freigabenachweise.
  Keine Abnahme mit null gemessenen Klassen oder nicht ausgeführten Tests behaupten.
