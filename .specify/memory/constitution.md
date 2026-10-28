<!--
Sync Impact Report — temporäres Review-Material; vor dem Commit entfernen.
Version change: 1.9.0 → 2.0.0 (genehmigte Neudefinition des Mindest-Coverage-Gates: 90 → 87 Prozent).
Modified principles:
  IV. Testgetriebene Entwicklung und reproduzierbare Qualität:
    mindestens 87 % Zeilenabdeckung je nativer Plattform, unveränderter Gesamtumfang.
Added sections: keine.
Removed sections: keine.
Deferred items:
  TODO(RATIFICATION_DATE): Datum der ursprünglichen Annahme bestätigen.
Projektname aus dem Arbeitsverzeichnis abgeleitet: monstertacle.multisession.
Nutzerfreigabe: „87 % code coverage sind okay, bitte anpassen“ (2026-10-28).
Plan, Tasks, Spec, AGENTS.md, Skill, Gate, Tests und CI werden synchronisiert.
Keine C#-/Toolpin-/Scopeänderung; historische Prüfergebnisse bleiben unverändert.
-->
# monstertacle.multisession Constitution

## Core Principles

### I. Verbindliche C#- und .NET-Standards

- Anwendungscode MUSS in C# auf einer unterstützten .NET-Version entwickelt werden.
  Die konkrete SDK-Version MUSS vor Implementierungsbeginn festgelegt werden.
- In `monstertacle.multisession` darf Python weder für Produktcode noch für Tests,
  Build-/CI-Skripte, Codegenerierung oder agentengestützte Projektarbeiten eingesetzt
  werden. Stattdessen sind C#/.NET und bei Bedarf PowerShell beziehungsweise Shellskripte
  zu verwenden. Das Verbot gilt auch für temporäre Hilfsskripte und einmalige
  Agentenbefehle; eine Python-Laufzeit darf keine Projektvoraussetzung sein.
- Code MUSS den dokumentierten Microsoft-C#-Konventionen und den vereinbarten
  .NET-Analyzer-Regeln folgen. Formatierung und Nullable-Referenztypen MÜSSEN aktiviert sein.
- Neue Compiler- und Analyzer-Warnungen MÜSSEN vor Freigabe behoben werden.
  Unterdrückungen benötigen eine lokale, überprüfbare Begründung.
- Asynchrone I/O MUSS über async/await erfolgen; blockierendes Warten auf Tasks ist im
  Anwendungspfad untersagt. Ressourcen MÜSSEN über definierte Lebenszyklen freigegeben werden.
- Länger laufende Operationen und externe Aufrufe MÜSSEN kooperativen Abbruch unterstützen.
  Ein übergebener CancellationToken MUSS bis zu den unterstützenden Abhängigkeiten
  weitergereicht und bei eigenen Schleifen regelmäßig ausgewertet werden.
- Externe Aufrufe MÜSSEN endliche, dokumentierte Zeitlimits besitzen. Für länger laufende
  Abläufe MÜSSEN Zeitlimits oder eine fachlich begründete Laufzeitstrategie festgelegt werden.
  Abbruch MUSS Ressourcen freigeben und darf nicht als erfolgreicher Abschluss gemeldet werden.

Diese Regeln machen „Best Practices“ überprüfbar statt zu einer bloßen Absichtserklärung.

### II. Einfache, wartbare Architektur

- Vor jeder Erweiterung MUSS der aktuelle Bestandscode durch einen vertieften
  Code-Deep-Dive und eine Architekturanalyse untersucht werden, bevor die Erweiterung
  entworfen und implementiert wird. Dies gilt auch für kleine Erweiterungen und
  agentengestützte Arbeiten; bloßes Lesen von Plan oder Dokumentation genügt nicht.
  Zu untersuchen sind die betroffenen Module und ihre relevanten aufrufenden und
  aufgerufenen Komponenten, Verantwortlichkeiten, Abhängigkeitsrichtungen, öffentlichen
  Verträge, Daten-/Kontrollflüsse, Zustands- und Persistenzregeln sowie vorhandene Tests.
  Bestehende Erweiterungspunkte, wiederverwendbare Fachregeln, technische Schulden,
  Kompatibilitäts- und Regressionsrisiken MÜSSEN bewertet werden. Die Untersuchungstiefe
  MUSS den Auswirkungen entsprechen; kleine Erweiterungen sind nicht von der Pflicht befreit.
- Die Bestandsanalyse MUSS vor der Umsetzung mit Anforderungsbezug, geprüftem Quellstand
  beziehungsweise Sourcehash, konkreten Datei-/Symbolbelegen, Befunden, offenen Fragen,
  Architekturfolgen, gewähltem Erweiterungspunkt und vorgesehenen Tests dokumentiert sein.
  Ohne diesen Nachweis oder bei ungeklärten implementierungsrelevanten Architekturfragen
  darf die Implementierung der Erweiterung nicht beginnen. Frühere Analysen dürfen nur
  nach erneutem Abgleich mit aktuellem Bestandscode und dem konkreten Erweiterungsumfang
  wiederverwendet werden. Fehlt Bestandscode, MUSS dies samt geprüftem Gerüst und
  vorhandenen Verträgen ausdrücklich festgehalten statt eine Codeanalyse behauptet werden.
- Klassen und Module MÜSSEN eine benennbare Verantwortung besitzen. Fachlogik MUSS von
  Benutzeroberfläche, Persistenz und externen Integrationen getrennt bleiben.
- Klassen und Schnittstellen MÜSSEN den SOLID-Prinzipien entsprechen:
  - Single Responsibility: Jede Klasse MUSS genau eine fachlich benennbare Verantwortung
    und damit einen zusammenhängenden Änderungsgrund besitzen.
  - Open/Closed: Bestehende Erweiterungsanforderungen MÜSSEN über geeignete Schnittstellen
    oder Komposition umgesetzt werden, ohne unabhängige Fachlogik ändern zu müssen.
    Daraus folgt keine Pflicht zu spekulativen Erweiterungspunkten.
  - Liskov Substitution: Subtypen MÜSSEN die Verträge ihrer Basistypen einhalten und ohne
    geänderte Verhaltenserwartungen an deren Stelle einsetzbar sein.
  - Interface Segregation: Schnittstellen MÜSSEN auf ihre Nutzer zugeschnitten sein;
    Nutzer dürfen nicht von Methoden abhängen müssen, die sie nicht benötigen.
  - Dependency Inversion: Fachlogik darf nicht von konkreten Infrastrukturimplementierungen
    abhängen; Abhängigkeiten über diese Grenze MÜSSEN auf Abstraktionen beruhen.
- DRY (Don't Repeat Yourself) MUSS für fachliches Wissen gelten: Dieselbe Geschäftsregel,
  Validierungsregel oder fachliche Konstante darf nicht unabhängig mehrfach gepflegt werden.
  Gemeinsame Regeln MÜSSEN eine maßgebliche Definition besitzen. Nur äußerlich ähnlicher
  Code mit unterschiedlicher fachlicher Bedeutung darf nicht künstlich gekoppelt werden.
- Anti-Patterns dürfen nicht neu eingeführt werden. Insbesondere sind Magic Values,
  God Classes, Service Locator und kopierte Geschäftsregeln untersagt. Bestehende Befunde
  im geänderten Bereich MÜSSEN behoben oder als befristete Ausnahme genehmigt werden.
- Magic Values sind Zahlen- oder Zeichenfolgenliterale mit nicht unmittelbar erkennbarer
  fachlicher oder technischer Bedeutung. Sie MÜSSEN durch benannte Konstanten, Enums,
  Wertobjekte oder validierte Konfiguration ersetzt werden. Offensichtliche Strukturwerte
  wie der Startindex 0 einer Schleife benötigen keine künstliche Konstante.
- Veränderliche externe Abhängigkeiten MÜSSEN austauschbar und isoliert testbar sein.
  Globale veränderliche Zustände und versteckte Abhängigkeiten sind zu vermeiden;
  notwendige Ausnahmen MÜSSEN dokumentiert und im Review genehmigt werden.
- Der Multisession-Kern und seine öffentlichen Sitzungsoperationen MÜSSEN gegenüber
  Herstellern, Agent-Produkten und Ausführungstransporten generisch bleiben. Auswahl
  MUSS über explizit registrierte, polymorphe Adapter erfolgen. Provider-, Backend-
  und Modellkennungen sind technische Konfigurationsdaten, keine Grundlage für
  herstellerspezifische if-/switch-Verzweigungen im Kern. Die Zuordnung von Kennungen
  zu registrierten Adaptern am Kompositionsrand ist zulässig.
- Protokoll, Authentifizierung, native Sitzungs-/Fortsetzungsregeln und Interpretation
  von Abschluss- und Fehlersignalen MÜSSEN im jeweiligen Adapter gekapselt sein.
  Ein weiterer Adapter für einen neuen Vertrag MUSS ohne Hersteller-Sonderfälle
  oder Anpassung bestehender Fachlogik registrierbar sein. Bereits unterstützte
  kompatible Zugänge und Modelle MÜSSEN durch Konfiguration auswählbar sein.
- Erforderliche Fähigkeiten und fehlende Unterstützung MÜSSEN über explizite Verträge
  beziehungsweise typisierte Ergebnisse dargestellt werden, nicht aus Produktnamen
  erraten werden. Nicht unterstütztes Verhalten MUSS nachvollziehbar abgewiesen werden;
  es darf keinen stillen Fallback oder vorgetäuschte Gleichwertigkeit geben.
- Eine Modell-API und ein vollständiges Agent-Backend dürfen nicht gleichgesetzt werden.
  Der generische Vertrag MUSS die zugesicherten Agent-Sitzungsanforderungen tatsächlich
  darstellen können: hostseitigen Workspacebezug, stabile opaque native Identität,
  Fortsetzung, bestätigten Abschluss, Abbruch und ungewisse Übergabe. Werkzeugschleifen,
  Kontextverwaltung und Verdichtung bleiben beim externen Agent-/Provider-Backend;
  die Bibliothek darf fehlende Fähigkeiten nicht durch eine eigene Agent-Laufzeit ersetzen.
- Die Einbettungsfähigkeit für einen benannten Zielhost MUSS gegen dessen tatsächlich
  untersuchten Ausführungs- und Sitzungsvertrag geplant und durch passende Vertrags-/
  Konsumtests nachgewiesen werden. Ein beliebiger Fake oder eine Modellantwort allein
  belegt keine Agent-Integration. Die Bibliothek darf vom Zielhost weder über Assemblies
  noch über Quellcodekopien abhängen; eine gemeinsame Abstraktion ist keine Zusage
  zur Übernahme bestehender nativer Sitzungen oder zur Unterstützung aller Backends.
- KISS (Keep It Simple) MUSS die Lösungswahl bestimmen: Die einfachste Lösung, welche
  die aktuellen Anforderungen und Qualitätsvorgaben erfüllt, ist zu wählen. Zusätzliche
  Schichten, Entwurfsmuster oder Abhängigkeiten benötigen eine dokumentierte Begründung.
- YAGNI (You Aren't Gonna Need It) MUSS eingehalten werden: Abstraktionen und Funktionen
  benötigen einen aktuellen Anwendungsfall oder eine verbindliche Anforderung.
  Spekulative Erweiterungspunkte und ungenutzte Funktionalität dürfen nicht eingeführt werden.
- Architekturentscheidungen mit langfristigen Auswirkungen MÜSSEN mit Kontext,
  Alternativen und Konsequenzen dokumentiert werden.

Ziel sind verständliche Änderungen und begrenzte Kopplung, nicht maximale Schichtenanzahl.

### III. Codemetr-Prüfung und kontrolliertes Refactoring

- Jede neue oder geänderte, projektverantwortete C#-Klasse MUSS vor Freigabe mit dem
  offiziellen .NET-CLI-Werkzeug `Codemetr.CSharp.Tool` 0.1.0 aus dem Projekt
  `codemetr/Codemetr.CSharp` geprüft werden, einschließlich Test- und Hostklassen.
  Der projektlokale Skill `codemetr-guard`, abgeleitet aus `codemetr/skills`, MUSS den
  agentengestützten Prüfablauf begleiten; er ersetzt weder Werkzeug noch CI-Gate.
  Generierter Code und
  Fremdcode dürfen nur mit dokumentierter Abgrenzung ausgeschlossen werden.
- Tool-Version, Prüfumfang, relevante Ergebnisse und die Entscheidung über Befunde MÜSSEN
  im Änderungsnachweis festgehalten werden. Eine installierte Erweiterung allein ist kein Nachweis.
- Überschrittene vereinbarte Grenzwerte und festgestellte Wartbarkeitsprobleme MÜSSEN
  durch Refactoring behoben oder als begründete, befristete Ausnahme genehmigt werden.
  Ausnahmen benötigen Verantwortliche, Ablaufdatum und eine verknüpfte Folgeaufgabe.
- Nach Refactoring MÜSSEN Codemetr-Prüfung und betroffene Tests erneut ausgeführt werden.
  Refactoring MUSS das bestehende Verhalten erhalten; fachliche Änderungen sind separat auszuweisen.
- Ist das Tool nicht verfügbar, darf die Prüfung nicht als bestanden gelten. Die Freigabe
  bleibt gesperrt, sofern keine explizite befristete Ausnahme genehmigt wurde.

Die genehmigten Standardgrenzwerte MÜSSEN in `codemetr.json` festgehalten werden:
Typzeilen ≤600, Konstruktorparameter ≤6, öffentliche Methoden ≤10, Instanzfelder ≤8,
Member-Komplexität ≤25, Maintainability Index ≥15, Kopplung ≤40, Vererbungstiefe ≤3,
Memberparameter ≤7 und Verschachtelung ≤4. Die Standardverbote für Typnamenssuffixe
Helper, Helpers, Util, Utils, Utilities, Manager und Processor MÜSSEN gelten.
Es wird keine bestehende Schuld über `codemetr.baseline.json` akzeptiert.
Vom Projektroot MUSS `dotnet tool run codemetr-csharp -- check --format json` als
vollständige Prüfung ausgeführt werden; die lokale Toolversion MUSS gepinnt sein.
Die CI MUSS diesen Check unter Windows und Linux ausführen. Exitcode 1 (Befunde)
oder 2 (Eingabe-/Aufruffehler) MUSS die Freigabe sperren. Null gemessene Klassen sind
kein Codefreigabenachweis. Messgrenzen und übersprungener Code MÜSSEN bewertet werden.
Änderungen an Version, Regeln, Messumfang oder Debt-Baseline benötigen ausdrückliche
Genehmigung; Grenzwerte dürfen nicht zum bloßen Bestehen abgeschwächt werden.

### IV. Testgetriebene Entwicklung und reproduzierbare Qualität

- TDD (Test-Driven Development) MUSS für jede neue oder geänderte Verhaltensanforderung
  angewendet werden: zuerst einen automatisierten Verhaltenstest schreiben, dann dessen
  begründetes Fehlschlagen beobachten (Red), danach ausschließlich die zur Erfüllung
  nötige Implementierung ergänzen (Green) und erst anschließend strukturell verbessern
  (Refactor). Betroffene Tests MÜSSEN während des Refactorings grün bleiben.
- Vor dem ersten Produktivverhalten MÜSSEN zugehörige Tests ausführbar sein. Minimale
  Signaturen und verhaltenslose Test-/Projektgerüste sind zur Ausführbarkeit zulässig;
  ein reiner Compile-, Installations- oder Infrastrukturfehler ist kein Red-Nachweis
  für das zu entwickelnde Verhalten. Nachträglich geschriebene Tests gelten nicht als TDD.
- Der Änderungsnachweis MUSS Anforderung beziehungsweise Fehler, Testkennung, ausgeführten
  Testbefehl, beobachteten fachlichen Red-Grund und anschließendes Green-Ergebnis enthalten.
  Nicht ausgeführte Tests dürfen weder als Red noch als Green dokumentiert werden.
- Fehlerkorrekturen MÜSSEN zuerst durch einen fehlschlagenden Regressionstest reproduziert
  werden. Geänderte Integrationsverträge und Persistenz-/Recovery-Regeln MÜSSEN vor ihrer
  Implementierung durch passende fehlschlagende Vertrags- oder Integrationstests erfasst sein.
- Providerunabhängige Verhaltensregeln MÜSSEN deterministisch ohne produktive Zugänge im
  TDD-Zyklus prüfbar sein. Echte Provider- und Plattformabnahmen bleiben zusätzliche
  Freigabenachweise; sie ersetzen weder den Red-Nachweis noch dürfen Fakes sie vortäuschen.
- Reine verhaltenserhaltende Refactorings MÜSSEN vor Beginn eine grüne relevante Testsuite
  besitzen und danach dieselben Zusicherungen erfüllen. Es ist kein künstlich roter Test
  erforderlich, solange sich kein Verhalten ändert. Reine Dokumentations- oder Buildmetadaten-
  Änderungen benötigen passende Validierung statt erfundener Verhaltenstests.
- Neue oder geänderte Fachlogik MUSS automatisierte Tests für reguläre Abläufe,
  relevante Grenzfälle und Fehlerfälle erhalten. Fehlerkorrekturen benötigen einen Regressionstest.
- Schnittstellenänderungen, Persistenz und externe Integrationen MÜSSEN durch geeignete
  Integrations- oder Vertragstests abgesichert werden, soweit sie von der Änderung betroffen sind.
- Tests MÜSSEN unabhängig und wiederholbar ausführbar sein. Unit-Tests dürfen keine
  produktiven Dienste, realen Zugangsdaten oder unkontrollierten Netzwerkzugriffe benötigen.
- Die automatisierten Tests MÜSSEN für den gesamten projektverantworteten Produktivcode
  mindestens 87 % Zeilenabdeckung (Line Coverage) erreichen, nicht nur für geänderte Zeilen.
  Nicht ausgeführter Produktivcode MUSS in die Messung eingehen. Testcode zählt nicht zum
  Messumfang; generierter Code und Fremdcode dürfen nur mit dokumentierter Begründung
  ausgeschlossen werden. Ausschlüsse zur bloßen Erfüllung des Grenzwerts sind untersagt.
- Die CI MUSS die Zeilenabdeckung unter Linux und Windows für den freizugebenden Versionsstand
  reproduzierbar messen und je Plattform mindestens 87 % durchsetzen. Fehlende Messwerte oder
  eine Unterschreitung MÜSSEN die Freigabe sperren. Tool-Version, Messbefehl, Messumfang,
  Ausschlüsse und Coverage-Bericht MÜSSEN als Prüfnachweis nachvollziehbar sein.
- Vor Freigabe MÜSSEN Restore, Build, Format-/Analyzer-Prüfung und betroffene Tests mit
  dokumentierten Befehlen erfolgreich durchlaufen. Bekannte Fehlschläge sind offenzulegen,
  nicht als bestandene Prüfung darzustellen.
- Restore, Build und die automatisierten Tests MÜSSEN vor Freigabe sowohl unter Linux
  als auch unter Windows erfolgreich ausgeführt werden. Plattformabhängige Funktionen
  MÜSSEN auf dem jeweiligen Zielsystem getestet werden; übersprungene Tests sind kein
  Nachweis für plattformübergreifend zugesichertes Verhalten.
- Die CI MUSS Restore, Build, Format-/Analyzer-Prüfung und automatisierte Tests unter
  Linux und Windows ausführen. Jede zur Freigabe vorgesehene Änderung MUSS diese Prüfungen
  für denselben Versionsstand erfolgreich durchlaufen; lokale Ergebnisse ersetzen sie nicht.
  Der vollständige Codemetr-CLI-Check MUSS ebenfalls für diesen Versionsstand bestanden sein.

Metriken ergänzen diese Verhaltensnachweise; sie ersetzen keine Tests oder Code-Reviews.

### V. Sicherheit und nachvollziehbarer Betrieb

- Geheimnisse und personenbezogene Daten dürfen weder im Quellcode noch in Testdaten
  oder Logs offengelegt werden. Konfiguration und Geheimnisse MÜSSEN getrennt verwaltet werden.
- Eingaben an Vertrauensgrenzen MÜSSEN validiert werden. Zugriffe MÜSSEN mit den minimal
  notwendigen Berechtigungen erfolgen; Datenbankabfragen MÜSSEN parametrisiert sein.
- Abhängigkeiten MÜSSEN vor Einführung auf Wartungsstatus, Lizenz und bekannte
  Sicherheitslücken geprüft werden. Offene Sicherheitsbefunde benötigen eine dokumentierte
  Risikobewertung; kritische Befunde sperren eine Freigabe ohne explizite Risikofreigabe.
- Direkte und transitive NuGet-Abhängigkeiten MÜSSEN zusätzlich vor jeder Freigabe und
  mindestens wöchentlich automatisiert auf bekannte Sicherheitslücken geprüft werden.
  Ergebnisse MÜSSEN nachvollziehbar sein; Befunde benötigen Verantwortliche und eine
  risikobasierte Behebungsfrist. Die bestehenden Regeln zur Risikofreigabe gelten weiterhin.
- Fehler MÜSSEN nachvollziehbar behandelt werden; stilles Verschlucken von Ausnahmen ist
  untersagt. Betriebsrelevante Abläufe MÜSSEN strukturierte, datensparsame Diagnosen liefern.

## Technische Rahmenbedingungen

- Die Anwendung MUSS unter Linux und Windows unterstützt werden. Konkrete unterstützte
  Betriebssystemversionen, Linux-Distributionen und Prozessorarchitekturen MÜSSEN vor
  Implementierungsbeginn im technischen Plan festgelegt werden.
- Anwendungstyp, unterstützte .NET-/SDK-Version und notwendige Bibliotheken MÜSSEN
  im technischen Plan festgelegt werden. UI-Technologie und Abhängigkeiten MÜSSEN beide
  Zielplattformen unterstützen; diese Constitution legt keine konkrete UI, Datenbank
  oder Bereitstellungsplattform ohne fachlichen Bedarf fest.
- Plattformabhängige APIs und native Bibliotheken MÜSSEN hinter klaren Schnittstellen
  isoliert sein. Für zugesicherte Funktionen MUSS auf beiden Zielplattformen eine
  Implementierung vorliegen; Einschränkungen benötigen eine ausdrückliche Umfangsentscheidung.
- Dateipfade MÜSSEN über plattformgeeignete .NET-APIs verarbeitet werden. Fest kodierte
  Laufwerksbuchstaben, Pfadtrenner und Annahmen über Groß-/Kleinschreibung sind untersagt.
  Dateizugriffe und Prozessaufrufe MÜSSEN Unterschiede bei Berechtigungen, Zeilenenden
  und Shell-Verhalten berücksichtigen, sofern diese für die Funktion relevant sind.
- Für beide Zielplattformen MÜSSEN reproduzierbare Start- und Publish-Anweisungen sowie
  notwendige Laufzeitvoraussetzungen dokumentiert werden. Entwicklungswerkzeuge müssen
  nicht auf beiden Plattformen laufen, sofern die erforderlichen Prüfnachweise vorliegen.
- Die Projektdokumentation MUSS Einrichtung, Konfiguration, Restore, Build, Tests und
  Ausführung unter Linux und Windows abdecken. Änderungen an diesen Abläufen MÜSSEN die
  zugehörige Dokumentation im selben Änderungssatz aktualisieren.
- Maschinenlesbare technische Daten MÜSSEN mit explizit kulturunabhängigen Formaten
  serialisiert und eingelesen werden. Benutzerbezogene Ein-/Ausgaben MÜSSEN eine bewusst
  gewählte Kultur verwenden; implizite Abhängigkeiten von der Systemkultur sind untersagt.
- Eindeutige Zeitpunkte MÜSSEN mit UTC oder explizitem Offset gespeichert und übertragen
  werden; lokale Darstellung MUSS davon getrennt bleiben. Fachliche Kalenderdaten und
  lokale Termine MÜSSEN mit passender Datums-/Zeitsemantik und, falls nötig, benannter
  Zeitzone modelliert werden. Kultur- und zeitzonenabhängiges Verhalten MUSS getestet werden.
- Builds MÜSSEN aus einem sauberen Arbeitsstand mit dokumentierter SDK-Version und
  deklarierten Abhängigkeiten reproduzierbar ausführbar sein.
- Öffentliche Schnittstellen und persistierte Datenformate MÜSSEN dokumentiert werden.
  Inkompatible Änderungen benötigen eine Auswirkungsanalyse und einen Migrationsplan.
- Leistungsanforderungen MÜSSEN bei relevantem fachlichem Bedarf messbar definiert werden.
  Optimierungen benötigen Messungen; Codequalitätsmetriken allein sind kein Leistungsnachweis.

## Entwicklungs- und Freigabeprozess

1. Vor Implementierung MÜSSEN Umfang, Akzeptanzkriterien und technische Planung vorliegen.
   Vor jeder Erweiterung MUSS zusätzlich der dokumentierte Bestandscode-Deep-Dive samt
   Architekturanalyse nach Prinzip II vorliegen; dieser geht Erweiterungsentwurf und
   Test-first-Umsetzung voraus und ersetzt weder TDD noch spätere Qualitätsprüfungen.
   Der Plan MUSS die Einhaltung dieser Constitution und notwendige Ausnahmen prüfen.
   Für jede verhaltensändernde Umsetzungseinheit MUSS er zuerst zu schreibende Tests,
   erwarteten fachlichen Red-Grund und die folgende minimale Green-Umsetzung benennen.
2. Änderungen MÜSSEN klein und nachvollziehbar bleiben. Funktionale Änderungen und
   umfangreiche Refactorings MÜSSEN getrennt überprüfbar sein.
3. Vor Freigabe MÜSSEN Build-, Analyzer-, Test-, Coverage- und Codemetr-Ergebnisse vorliegen.
   Die Coverage-Nachweise MÜSSEN mindestens 87 % Zeilenabdeckung je Zielplattform belegen.
   Für neue oder geänderte Verhaltensanforderungen MUSS außerdem der Red–Green-Nachweis
   vorliegen; eine ausschließlich am Ende grüne Testsuite belegt keinen Test-first-Ablauf.
   Die CI-Nachweise für Linux und Windows sowie die aktuelle Abhängigkeitsprüfung MÜSSEN
   den freizugebenden Versionsstand abdecken; betroffene Dokumentation MUSS aktuell sein.
   Der Codemetr-Nachweis MUSS den geprüften Arbeitsstand benennen und den CI-Check enthalten.
4. Jede Änderung MUSS durch den Projektverantwortlichen oder einen benannten Reviewer
   auf Verhalten, Verständlichkeit, Sicherheit und Constitution-Konformität geprüft werden.
   Die Prüfung MUSS SOLID-Verträge, mehrfach gepflegte Fachregeln und Anti-Patterns
   einschließlich Magic Values ausdrücklich abdecken; Codemetr allein ersetzt sie nicht.
   Bei Adapter-/Sitzungsänderungen MUSS sie außerdem Backendneutralität, explizite
   Fähigkeitsgrenzen und die tatsächliche Darstellbarkeit der zugesicherten Agent-
   und Zielhostverträge prüfen. Anbieterneutrale Methodennamen allein genügen nicht.
   Bei Einzelentwicklung ist eine dokumentierte Selbstprüfung erforderlich.
5. Fehlende Nachweise und ungelöste verpflichtende Befunde sperren die Freigabe.
   Ausnahmen MÜSSEN vor Freigabe ausdrücklich genehmigt und befristet dokumentiert sein.

## Governance

Diese Constitution ist die verbindliche Projektgrundlage. Bei Widersprüchen MÜSSEN
Pläne und Umsetzung angepasst oder eine formelle Änderung dieser Constitution genehmigt werden.

Änderungsanträge MÜSSEN Anlass, geänderte Regeln, betroffene Abläufe und gegebenenfalls
einen Migrationsplan enthalten. Der Projektverantwortliche MUSS die Änderung ausdrücklich
genehmigen. Die Version und das Änderungsdatum MÜSSEN dabei aktualisiert werden.

Die Version folgt Semantic Versioning: MAJOR für inkompatible Aufhebungen oder Neudefinitionen
von Regeln, MINOR für neue Prinzipien oder wesentlich erweiterte Vorgaben und PATCH für
Klarstellungen ohne Änderung der Verpflichtungen. Die erstmalige Definition beginnt mit 1.0.0.

Die Konformität MUSS bei jeder technischen Planung und jeder Änderungsfreigabe geprüft werden.
Befristete Ausnahmen MÜSSEN spätestens bei Ablauf erneut bewertet werden; sie ändern die
Constitution nicht stillschweigend. Diese Regeln gelten ab ihrer Annahme; ein historisches
Annahmedatum wird nicht ohne Bestätigung erfunden.

**Version**: 2.0.0 | **Ratified**: TODO(RATIFICATION_DATE): Annahmedatum bestätigen | **Last Amended**: 2026-10-28
