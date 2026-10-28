# Quickstart & Validation: Agent-Multisession

**Datum**: 2026-10-27 | **Basis**: [plan.md](plan.md)

Diese Anleitung beschreibt die nach Implementierung auszuführenden Nachweise.
Die hier benannten Solution-, Projekt- und Hostdateien sind geplant und existieren
noch nicht. Die Befehle wurden in der Planungsphase NICHT ausgeführt; derzeit führen
sie noch nicht zu einer lauffähigen Anwendung. Kein Beispiel ist ein behauptetes Testergebnis.
Gegen Constitution v1.7.0 und FR-027 zu prüfen, einschließlich tatsächlichem Agent-/Zielhostvertrag (R-13): Die folgenden vollständigen
Abnahmebefehle ersetzen nicht den vorherigen Test-first-Ablauf je Verhaltensänderung.

## 1. Voraussetzungen

- Exaktes .NET SDK 10.0.403; C# 14; Entwicklung auf einem in plan.md genannten Zielsystem.
- Implementierte Solution gemäß geplantem Aufbau und eingecheckte Paket-Lockfiles.
- Privates lokales Testverzeichnis unter eigener Identität; keine produktiven Stores.
- Keine Providerzugänge für Unit-, Vertrags-, FIFO-, Crash- und Plattformtests nötig.
- Für Live-Proben: ausdrücklich freigegebene Testkonten beider Hersteller, passendes
  Modell, erlaubte native Speicherung und mögliche Gebühren. Keine Werkzeuge konfigurieren.
- Q-01: CLI 0.1.0, Standardgrenzwerte ohne Debt-Baseline und Guard-Skill sind genehmigt
  und eingerichtet; siehe `evidence/codemetr-approval.md`. Produktprüfung bleibt offen.

Alle folgenden Pfade sind relativ zum Repositoryroot. Der spätere ValidationHost
akzeptiert absolute `--state-root`-Pfade, verwendet je Lauf eine neue Unterablage und
löscht keine fremden/produktiven Verzeichnisse. `--scenario` und die unten genannten
Szenarionamen sind sein geplanter Abnahmevertrag, kein zusätzliches CLI-Produkt.

## 2. Restore, Build und statische Prüfungen — Q-02

Auf jedem Buildhost mit demselben Quellstand:

```text
dotnet --version
dotnet restore Monstertacle.Multisession.slnx --locked-mode
dotnet build Monstertacle.Multisession.slnx -c Release --no-restore
dotnet format Monstertacle.Multisession.slnx --verify-no-changes --no-restore
dotnet list Monstertacle.Multisession.slnx package --vulnerable --include-transitive
```

Erwartet: SDK 10.0.403, erfolgreiches Restore/Build, keine neuen Compiler-/Analyzer-
Warnungen und keine unerledigten Freigabebefunde. Audit-Ausgabe inhaltlich bewerten;
ein Prozess-Exitcode allein bedeutet nicht automatisch keine Sicherheitsbefunde.

Die spätere CI führt Restore, Build, Format/Analyzer und Tests unter Windows und Linux
aus und wiederholt NuGet-Vulnerability-Prüfung mindestens wöchentlich sowie vor Freigabe.
Paket- und native SQLite-Version, Laufzeit, Plattform und Quellstand im Nachweis festhalten.

Codemetr: Den projektlokalen Skill `codemetr-guard` laden; vom Projektroot ausführen:

```text
dotnet tool restore
dotnet tool run codemetr-csharp -- --help
dotnet tool run codemetr-csharp -- check --format json
```

Der vollständige Check umfasst eigene Klassen einschließlich Tests/ValidationHost.
Ergebnisse, Messgrenzen und Quellstand speichern, Befunde refactoren und erneut prüfen.
Exit 1/2 sperrt Freigabe; Exit 0 mit null Klassen ist kein Produktnachweis und wird
vom CI-Workflow abgewiesen. Keine Debt-Baseline und keine still geänderten Grenzwerte.
Tool-Restore benötigt NuGet-Netzwerkzugriff; keine globale Installation notwendig.

### Coverage-Gate — mindestens 87 % Zeilenabdeckung (Q-02)

Collector 10.1.0, `tests/coverage.runsettings`, Scopeinventar und Gate-Smokes sind
eingerichtet. Der lokale Runner entspricht dem konfigurierten CI-Ablauf und benötigt
PowerShell 7 sowie das gepinnte SDK; keine Python-Laufzeit:

```text
pwsh -NoProfile -File scripts/Invoke-Quality.ps1 -Platform windows
pwsh -NoProfile -File scripts/Invoke-Quality.ps1 -Platform linux
```

Jeden Befehl nur auf seinem nativen Betriebssystem ausführen. Der Runner verwendet
eine neue Reportablage und denselben Sourcehash für Build/Tests/Coverage. Aktueller
Windowslauf: 88,2576 % (233/264 Produktzeilen; 177 .NET-Tests bestanden), Exit 0
beim ausdrücklich genehmigten Mindestwert 87 % (Constitution v2.0.0);
Linux und echte GitHub-CI noch NOT RUN. Aktueller Quellstand: `evidence/coverage.md`.
Das ist keine fertige Anwendung; siehe `evidence/coverage.md`.

- Windows und Linux messen jeweils sämtliche gemeinsamen Produktbibliotheken und das
  native Plattformpaket, einschließlich nicht geladener Assemblies. Das Scopeinventar
  muss über beide nativen Läufe alle sechs Bibliotheken abdecken; keine stille Auslassung.
- Testcode zählt nicht zum Nenner; generierter Code/Fremdcode nur begründet ausschließen.
- Berichte der Testprojekte pro Plattform zeilenweise vereinigen, nicht Prozentwerte mitteln.
- Weniger als 87 %, fehlende/leere/ungültige Reports oder fehlende Produktassemblies
  lassen das Gate fehlschlagen. Exakt 87 % besteht; Bewertung vor Rundung vornehmen.
- In `evidence/coverage.md` Toolversionen, Befehle, Quellstand, Plattform, Projektinventar,
  Ausschlüsse, Rohberichte und Gate-Ergebnisse nachweisen. Coverage ersetzt keine TDD-Nachweise.

Die vorhandene `.github/workflows/codemetr-guard.yml` prüft Codemetr, nicht Coverage.
Nach Repositoryanbindung müssen sowohl Coverage-/Test-CI als auch Codemetr-Checks als
erforderliche Branchchecks eingerichtet werden; lokale Dateien aktivieren keinen Branchschutz.

### Test-first-Zyklus vor jeder Verhaltensimplementierung — Q-06

Die Einheiten TDD-01–TDD-07 stehen im Plan. Für jeden kleinen Verhaltensschritt:

1. Test und minimale verhaltenslose Signaturen so vorbereiten, dass der Test kompiliert
   und entdeckt wird. Nur Testinfrastruktur herstellen, nicht schon Produktivverhalten.
2. Den gezielten Test ausführen und den fachlich erwarteten Fehler tatsächlich beobachten.
   Beispiel für die erste geplante Testgruppe, nachdem das Testprojekt vorhanden ist:

   ```text
   dotnet test tests/Monstertacle.Multisession.UnitTests/Monstertacle.Multisession.UnitTests.csproj -c Release --no-restore --filter FullyQualifiedName~SessionRoutingTests
   ```

   Erwartetes Red: Eine konkrete Assertion zeigt beispielsweise eine fehlende oder
   falsche Sitzungszuordnung. Fehlendes SDK, Compilefehler, Installationsfehler oder
   null entdeckte Tests sind kein gültiger Nachweis. Noch nicht implementierte Testgruppen
   dürfen in dieser Dokumentationsphase nicht als tatsächlich rot bezeichnet werden.
3. Nur das zur Erfüllung dieses Tests nötige Verhalten implementieren. Denselben Test
   erneut ausführen, Green nachweisen und die relevante Regressionstestsuite starten.
4. Bei grünen Tests refactoren; anschließend Tests und vorgeschriebene Codemetr-Prüfung
   wiederholen. Kein neuer fachlicher Umfang im vermeintlich strukturellen Refactoring.
5. FR-/SC-Bezug, Testkennung, Befehl, Quellstand/Hash, Red-Grund und Green-Ergebnis im
   Änderungsnachweis festhalten. Ein später grüner Lauf allein belegt die Reihenfolge nicht.

Für TDD-03 gehören gezielte SQLite-/Crash-Tests zuerst in IntegrationTests, für TDD-05
kontrollierte HTTP-Proben in ContractTests und für TDD-06 native Besitz-/Rechteproben
zuerst in die jeweils passende PlatformTests-Zielvariante. Die Infrastruktur dafür darf
vorbereitet werden; das zu prüfende Produktivverhalten entsteht erst nach fachlichem Red.
Fehlerkorrekturen starten mit einem fehlschlagenden Regressionstest. Reine Refactorings
starten dagegen mit bereits grüner relevanter Suite und benötigen kein erfundenes Red.

Die spätere CI muss die reproduzierbare Green-/Regressionsprüfung ausführen; zusätzlich
wird die dokumentierte Red–Green-Reihenfolge im Review geprüft. Live-Proben, Paket-Smoke,
Last-/Dauerläufe und vollständige Zielsystemabnahme bleiben weitere Freigabenachweise.

## 3. Deterministische Tests — Q-03

```text
dotnet test tests/Monstertacle.Multisession.UnitTests/Monstertacle.Multisession.UnitTests.csproj -c Release --no-restore
dotnet test tests/Monstertacle.Multisession.ContractTests/Monstertacle.Multisession.ContractTests.csproj -c Release --no-restore
dotnet test tests/Monstertacle.Multisession.IntegrationTests/Monstertacle.Multisession.IntegrationTests.csproj -c Release --no-restore
```

Diese Projekte haben keine automatische Live-Ausführung. Provider-Fakes und kontrollierte
HTTP-Handler nutzen denselben öffentlichen Vertrag wie die echten Adapter.
Sie messen ausschließlich Verwaltung und Zuordnung, nicht Herstellerantwortqualität.

Plattformtests getrennt ausführen, nicht eine nicht passende Plattform als grün überspringen:

Windows:

```text
dotnet test tests/Monstertacle.Multisession.PlatformTests/Monstertacle.Multisession.PlatformTests.csproj -c Release -f net10.0-windows --no-restore
```

Linux:

```text
dotnet test tests/Monstertacle.Multisession.PlatformTests/Monstertacle.Multisession.PlatformTests.csproj -c Release -f net10.0 --no-restore
```

Erwartet: gültige private Rechte, Ablehnung fremder beziehungsweise ungültiger Ablagen,
zweiter Besitzer in einem separaten Prozess abgewiesen, Pfadaliase geprüft, Lease nach
echtem Prozessende wieder erwerbbar und ungewisse Reservationsdaten trotzdem erhalten.
Windows 11 und Windows Server erhalten jeweils einen echten Lauf; Linux ersetzt diese nicht.

## 4. Konsumierender Abnahmehost

Windows-Beispiel in PowerShell:

```powershell
dotnet run --project examples/Monstertacle.Multisession.ValidationHost -c Release -f net10.0-windows -- --scenario deterministic --state-root "$env:LOCALAPPDATA/Monstertacle.Multisession.Validation"
```

Linux-Beispiel in Bash, nach Vorbereitung einer eigenen privaten Testablage:

```bash
dotnet run --project examples/Monstertacle.Multisession.ValidationHost -c Release -f net10.0 -- --scenario deterministic --state-root "$HOME/.local/state/monstertacle-multisession-validation"
```

Der Host verbindet Common, die jeweilige Plattformbibliothek, SQLite und Testprovider.
Er öffnet Sitzungen, reicht Anfragen ein, liest Receipt/Status/History und stoppt kontrolliert.
Er darf weder Dienstinstallation noch öffentliche Ports, Zugangsdatenprompts oder
produktive Anmeldungen nebenbei einrichten. Details des Bibliotheksaufrufs stehen im
[öffentlichen Vertrag](contracts/public-api.md), nicht als vollständiger Anwendungscode hier.

## 5. Verbindliche Szenarien und erwartete Ergebnisse

| Szenario | Ausführung / Erwartung | Bezug |
| --- | --- | --- |
| deterministic | Zehn Sitzungen, zwei Hersteller-Fakes, je zwei Nachrichten; freie unabhängige Sends überlappen | SC-001 |
| routing | 100 gemischte Anfragen; richtige Identität, kein Kontextübertritt, FIFO je Sitzung | SC-002 |
| failures | Fünf Fehler und fünf unabhängige Erfolge bleiben korrekt zugeordnet | SC-003 |
| cancellation | Monotone Messung ab Eingang des Abbruchwunsches beziehungsweise tatsächlichem Fristende; bei kooperativ offen gehaltener Anfrage binnen zwei Sekunden dauerhaft abfragbarer Endzustand samt Gewissheit | SC-004 |
| shutdown | Monotone Frist ab Stop-Eingang: bis zehn Sekunden lokales Ende; verbleibende Queued-Anfragen Cancelled/NotStarted, unbestätigte externe Wirkung Unknown mit Reservierung | SC-004, FR-013 |
| capacity | Zwei Anfragen laufen bereits kontrolliert angehalten; danach acht gleichzeitige Eingänge bei Queue-Limit 3: drei zusätzlich angenommen, fünf abgewiesen; insgesamt fünf von zehn angenommen | SC-005 |
| workspace | Gleicher schreibender Workspace nie gleichzeitig, unabhängiger dritter Workspace kann freie Kapazität nutzen | FR-016 |
| extensions | Dritter andersartiger Testvertrag ohne Änderung am Kern oder Konsumenten; kompatibler Endpunkt nur über Konfiguration | SC-008 |
| recovery | Zehn Prozessabbrüche mit Limit 2: je ein abgeschlossenes Ergebnis in A/B, drei Queued in A, eine Running in B, getrennte Workspaces; A nutzt einen freien Slot, B bleibt blockiert ohne Replay | SC-009 |
| native-context | Folgeanfragen behalten native Zuordnung; Kontextfehler unverändert zugeordnet; null eigene Tools/Verdichtung/Replay | SC-010 |
| isolation | Zwei Instanzen und Stores vermischen weder Kennungen noch Daten/Providerregistrierungen | FR-026 |
| embedded-consumer | Konsumierende Anwendung mit eigenem Provideradapter: zwei parallele Sitzungen, Folgeanfragen, Status, Abbruch und Beenden ohne Bibliotheksänderung oder zusätzlichen Multisession-Prozess | FR-027, SC-011 |
| retention | Nur abgeschlossene konfliktfreie Sitzung lokal löschbar; keine ungeklärte Arbeit still entfernt | FR-021 |
| soak | Eine monotone Stunde ab Start, mindestens 60 angenommene Anfragen je Zielsystem; alle unter ursprünglicher Kennung mit Endzustand beziehungsweise gesichertem Zustand/Reservierung nachweisbar | SC-007 |
| limits | Anforderungen an leere Eingaben sowie exakt zulässige und um ein UTF-8-Byte zu große Ein-/Ausgaben; keine gekürzte Erfolgsantwort und keine Änderung bestehender Historie | FR-021, FR-025 |

Jeder Szenarioname ist als `--scenario` nach demselben Befehlsmuster wie oben vorgesehen.
Soak- und Crash-Proben laufen ausdrücklich separat, nicht als zufällige Langläufer im Unit-Test.
Der Host liefert strukturierte Pass/Fail/NotRun-Zusammenfassung und Exitcode 0 nur bei
bestandener gewählter Probe. Maschinenlesbarer Nachweis enthält keine Inhalte/Secrets.

Für Recovery einen Kindprozess wirklich beenden, nicht nur normal disposen. Crash-Punkte:
vor/nach Annahmecommit, nach Commit vor Wecksignal, nach Reservierung vor Netzwerkübergabe,
nach externer Annahme vor Operations-ID-Sicherung, vor/nach Ergebniscommit.
Zusätzlich Speicherausfall, Read-only, voller Datenträger, beschädigtes/unbekanntes Schema
und fehlender erwarteter Store prüfen. Keine Probe darf einen leeren Ersatzstore erzeugen.
Die ergänzenden Vertragsfälle sind in [orchestration-details.md](contracts/orchestration-details.md)
festgelegt und in T112–T115 eingeplant: eingefrorene Dispatchnutzlast, ungewisse Eröffnung
ohne Request, öffentlich lesbare Recoveryrevision, CAS-Konflikt, Annahme-Unknown mit
Kandidatenkennung und bereichsgebundene Cursor. Sie sind noch keine ausgeführten Proben.
Die erste Lieferung führt keine Formatmigration aus: Ein abweichendes Schema/Binding
muss vor Änderungen abgewiesen werden. Ein eigener Migrations-/Rollbacktestplan ist
erst mit einer ausdrücklich geplanten späteren Formatänderung erforderlich; er wird
hier nicht durch eine scheinbare Prüfung noch nicht vorhandener Migrationen ersetzt.

Für jede Probe den Ausgangszustand, Limits, angenommenen/abgewiesenen Kennungssatz,
Sollreihenfolge und monotone Zeitpunkte dokumentieren. Bei voller Reservierungskapazität
ist wartende Arbeit zulässig; ungewisse Slots dürfen nicht zur Erfüllung eines scheinbaren
Fortschrittsnachweises freigegeben werden. Auf nicht verfügbarem Speicher ist eine
erfolgreiche persistierte Abschaltung keine Zusicherung und darf nicht als solche gelten.

## 6. Echte Provider-Proben — Q-04

Hostkonfiguration enthält Endpunkte, native Vertragsversion, Modell und CredentialReference.
Geheimnisse kommen aus einer vom Betreiber vorbereiteten sicheren Quelle, nicht aus
Kommandozeilenargumenten oder versionierten Dateien. Keine Keys in diesen Guide eintragen.

Nach expliziter Kosten-/Speicherfreigabe `--scenario live-openai` und `--scenario live-gemini`
mit demselben Hostmuster ausführen. Voraussetzungen und Native-Operationen stehen im
[Providervertrag](contracts/provider-contract.md).

Je Hersteller und Zielsystem:

1. Neue lokale Sitzung und erste Textnachricht senden; native Kennungen im privaten
   Store prüfen, nicht als vertraulichen Inhalt in öffentliche Logs schreiben.
2. Folgeanfrage mit Bezug auf die erste Nachricht senden; derselbe native Sitzungsbezug
   beziehungsweise bestätigte Interaktionskettenkopf wird verwendet.
3. Host ohne laufende Anfrage neu starten und erneut fortsetzen; keine Historie replayen.
4. Fehlende/abgelaufene Sitzung und Kontextfehler mit kontrollierten Vertragsfixtures
   nachweisen, nicht eine echte lange oder teure Unterhaltung absichtlich erzeugen.
5. Ergebnis- und Abbruchzustände gegen native Operationszustände abgleichen.

Fehlende Credentials, Modellzugriffe oder fehlender Zielhost ergeben NOT RUN und sperren
die entsprechende Freigabe. Erfolgreiche Fakes ersetzen keine Live-Probe.
Konfigurierte Retention/Storage-Bedingungen beider Hersteller explizit dokumentieren.

## 7. NuGet-Konsum und Veröffentlichungsvorbereitung — Q-05

```text
dotnet pack Monstertacle.Multisession.slnx -c Release --no-build -o artifacts/packages -p:PackageVersion=0.1.0
```

Der spätere Package-Smoke-Test erzeugt eine frische konsumierende Beispielanwendung
außerhalb des Produktquellbaums, restauriert ausschließlich die erzeugten Bibliothekspakete
und deren freigegebene NuGet-Abhängigkeiten und führt erste Nachricht/Folgeanfrage aus.
Keine Source-/ProjectReference auf fremde Repositories; keine bereits vorhandene Installation
als versteckte Voraussetzung. Paketversion und Abhängigkeiten auf Vollständigkeit prüfen.

Der Szenarionachweis embedded-consumer verwendet ausschließlich öffentliche Verträge und
eine im konsumierenden Testprojekt definierte Providerimplementierung. Er ist der erste
Integrationsfähigkeitsnachweis für den langfristigen Zielkonsumenten SharpClaw, aber keine
Behauptung, dass die produktive Anwendung bereits umgestellt wurde. Keine Source-Referenz
oder Installation dieses Zielkonsumenten ist für den Nachweis erforderlich.

Für den ValidationHost Start-/Publish-Varianten pro TFM/RID prüfen, ohne einen Dienst zu installieren:

```text
dotnet publish examples/Monstertacle.Multisession.ValidationHost -c Release -f net10.0-windows -r win-x64 --self-contained false
dotnet publish examples/Monstertacle.Multisession.ValidationHost -c Release -f net10.0 -r linux-x64 --self-contained false
```

Veröffentlichung auf nuget.org, Produktionsdeployment, Windows-Service-Installer und
Linux-Service-Units sind ausdrücklich nicht Teil dieser Planungsphase oder dieser Befehle.

## 8. Freigabeprotokoll

Q-01 bis Q-06 getrennt mit Quellstand, Plattform, Versionen, Ergebnis und Nachweisdatei
festhalten. SC-006 zusätzlich durch einen Integrator ohne Quellcodekenntnis prüfen:
nach bereitgestellten Voraussetzungen erste Nachricht und Folgeanfrage in höchstens 30 Minuten.
Nicht ausgeführte Fälle bleiben sichtbar. Metriken, erfolgreiche Builds oder Dokumentation
allein ersetzen keine Verhaltens-, Crash-, Paket- oder Herstellerabnahme.
Q-06 verlangt zusätzlich den tatsächlich beobachteten Red-Grund vor Green je neuer
Verhaltensänderung. Im aktuellen Planungsstand sind sämtliche Red-/Green-Nachweise NOT RUN.
