## Agent Framework

Host integrations use the provider-neutral `Monstertacle.Multisession.AgentFramework` package with Microsoft Agent Framework 1.27.0. Durable CQRS, request identity and Unknown recovery remain authoritative. Only the current store format is read; stores of any other format are refused, never converted.

# Monstertacle.Multisession

Providerneutrale C#/.NET-Bibliotheken zur Verwaltung paralleler Agent-Sitzungen.
**In Entwicklung, keine Produktfreigabe.** SQLite-Persistenz, native Windows-/Linux-Leases,
Protokolladapter für Conversations/Responses und Interactions sowie Cancel, Stop und Recovery sind implementiert.
Live-, Zielsystem-, Integrator- und finale Paketabnahmen bleiben erforderlich;
der aktuelle Umfang steht im [Arbeitsnachweis](specs/001-agent-multisession/evidence/implementation-progress.md).

## Lokale Pakete 0.2.0-alpha.5

Acht Bibliotheken werden lokal gepackt: `Common`, `AgentFramework`, `Storage.Sqlite`, `Linux`,
`Windows`, `Providers.Conversations`, `Providers.Interactions` und `Delivery`, jeweils mit dem Präfix
`Monstertacle.Multisession.`. `Delivery` ist unabhängig von den Sitzungspaketen: ein opaker dauerhafter
Zustell-Checkpoint je Kanal mit typisierten atomaren Quittungs-/CAS-Primitiven, siehe [Zustell-Checkpoint](docs/delivery.md). Testprojekte und ValidationHost sind keine Pakete.
Es erfolgt keine Veröffentlichung. Die Bibliotheken stehen unter der MIT-Lizenz
(SPDX `MIT`, Datei [LICENSE](LICENSE)); jedes Paket enthält den Lizenztext mit
Copyrightvermerk. Abhängigkeiten werden nur als NuGet-Verweise geführt und behalten
ihre eigenen Lizenzen; die Pakete enthalten keinen fremden Code. Dateien unter
`.opencode/skills/codemetr-guard/` behalten ihre eigene Lizenzdatei.

```powershell
dotnet build Monstertacle.Multisession.slnx -c Release --no-restore
dotnet pack Monstertacle.Multisession.slnx -c Release --no-build -o artifacts/packages
pwsh -NoProfile -File eng/Test-PackageLayout.ps1
```

Eine Anwendung referenziert `Common`, `Storage.Sqlite` und genau ihr natives
Plattformpaket. Unter Windows ist `net10.0-windows`, unter Linux `net10.0` zu
verwenden. Herstelleradapter sind optional; eigene Adapter implementieren
`ISessionProvider` ausschließlich über die öffentlichen Verträge.
Die Bibliothek wird im Prozess eingebettet und benötigt keinen zusätzlichen Dienst.
Die [Einbettungsanleitung](docs/embedding.md) führt durch Paketbezug, eigene Adapter,
Herstellerkonfiguration und Publish. [Betrieb und Recovery](docs/operations.md)
beschreiben Fristen, Reservierungen, Diagnose und sichere Wiederaufnahme.

## Entwicklungsumgebung und Prüfungen

CQRS ist über `Monstertacle.SharpQrs` **0.2.0-alpha.1** eingebunden.
`SessionRuntime.Commands` und `SessionRuntime.Queries` bieten nach dem Start typisierte
asynchrone Operationen. Die bisherigen Ports bleiben kompatibel und durchlaufen dieselben
Dispatcher. [CQRS-Vertrag und Beispiele](docs/cqrs.md), [SpecKit-Feature](specs/002-sharpqrs-cqrs/spec.md).

- .NET SDK **10.0.403** gemäß `global.json`, kein implizites Roll-forward.
- PowerShell 7 (`pwsh`), Git und NuGet-Zugriff; kein Python.
- Genehmigtes lokales `Codemetr.CSharp.Tool` **0.1.0**; keine Debt-Baseline.
- Repositorylokales `Codemetr.CSharp.Cover.Tool` **0.1.1**; Konfiguration in `codemetr-cover.json`.
- Mindestens **87 %** Zeilenabdeckung des gesamten nativen Produktumfangs je Plattform.

Vom Projektroot:

```powershell
dotnet tool restore
dotnet restore Monstertacle.Multisession.slnx --locked-mode
pwsh -NoProfile -File scripts/Invoke-Quality.ps1 -Platform windows
dotnet tool run codemetr-csharp -- check --format json
```

Auf einem nativen Linuxhost `-Platform linux` verwenden. Die Prüfungen dürfen nicht
durch einen bloßen TFM-Wechsel auf einem anderen Betriebssystem ersetzt werden.
Die Qualitätsprüfung führt Codemetr Cover auf den frischen Cobertura-/TRX-Dateien aller
vier Testsuiten aus und verlangt `complete: true`. Neben dem nativen 87-%-Gate prüft
Cover mindestens 87 % des gesamten nativen Bibliothekscodes und des vollständigen Prüfhosts einschließlich ungemessener Zeilen
mit null Treffern sowie die unveränderten Standardgrenzen für Diff-Coverage und CRAP.
Das separate Produktgate behält den vollständigen Bibliotheksumfang als eigenen Nenner;
mitgemessene Prüfhost-Zeilen können ein unterschrittenes Produktgate nicht ausgleichen.
Build-Artefakte und Testcode zählen nicht als eigener Anwendungscode. Der Lauf erzeugt
`cover-native-config.json` und nimmt genau die fremde Plattform aus; sie wird zwingend
im jeweils anderen nativen CI-Lauf geprüft. Alle gemeinsamen Bibliotheken bleiben enthalten.
Berichte liegen unter `artifacts/quality/<plattform>/<lauf>/`.
Die drei funktionalen Wellen-Szenarien wählen ein explizites Aufnahmebudget von
30 Sekunden. Der SDK-Standard beträgt weiterhin fünf Sekunden; die eigenen
Deadline-Regressionen und alle weiteren Szenariofristen bleiben bestehen.
Diese funktionale Prüflast ist kein Nachweis einer fünfsekündigen Aufnahme-SLA;
die native Beobachtung dazu steht im [Untersuchungsplan](specs/003-native-reliability/follow-up-windows-admission.md).
Ausführliche Regeln stehen in [AGENTS.md](AGENTS.md), der
[Constitution](.specify/memory/constitution.md) und im
[Quickstart](specs/001-agent-multisession/quickstart.md).

## Übergabe an einen Remote-Agenten

Repository: <https://github.com/monstertacle/monstertacle.multisession>.
**Aktiver Auftrag: alle offenen Speckit-Tasks autonom implementieren.**
Arbeitsstand und Abhängigkeiten: [Gesamtimplementierung](specs/001-agent-multisession/evidence/implementation-progress.md).
T112–T115, Dispatcher und öffentliche Runtimekomposition sind umgesetzt; Nachweise und Grenzen
stehen in [tdd-us1.md](specs/001-agent-multisession/evidence/tdd-us1.md) und den anderen Storynachweisen.
Conversations/Responses und stabile Interactions sind gegen lokale HTTP-Fixtures implementiert;
[Providervertrag und Grenzen](docs/providers.md). Öffentliche Adapterintegration, SQLite-Transaktionen,
Crash-Recovery sowie Cancel, Stop und Close sind inzwischen mit echten lokalen Storetests umgesetzt;
Nachweise und verbleibende Abnahmegrenzen stehen in
[tdd-us3.md](specs/001-agent-multisession/evidence/tdd-us3.md) und
[tdd-us5.md](specs/001-agent-multisession/evidence/tdd-us5.md).
Bootstrap-Optionsabgleich und ausdrückliche lokale Retention/Löschung sind ebenfalls umgesetzt;
ihre vollständigen Abnahmegrenzen bleiben in den Tasks sichtbar. Speicherfehlerproben,
Diagnostik und native Prozessprüfungen sind vorhanden; Paketkonsum und finale
Abnahmen stehen im Arbeitsnachweis. Der kontrollierte Store bleibt eine Testprojektion.
Keine Produktfreigabe.

1. Eigenen Arbeitsbranch aus dem aktuellen Remote-Stand erstellen; keine parallelen
   Änderungen mehrerer Agenten im selben Arbeitsbaum.
2. AGENTS.md, Constitution, [Plan](specs/001-agent-multisession/plan.md),
   [Tasks](specs/001-agent-multisession/tasks.md) und den
   [Orchestrierungsvertrag](specs/001-agent-multisession/contracts/orchestration-details.md) lesen.
3. Featurekontext lokal setzen, da `.specify/feature.json` absichtlich nicht versioniert wird:

```powershell
$env:SPECIFY_FEATURE_DIRECTORY = 'specs/001-agent-multisession'
& ./.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireSpec -RequireTasks -IncludeTasks
```

Für eine Bash-gestartete Agentensitzung vorab:

```sh
export SPECIFY_FEATURE_DIRECTORY=specs/001-agent-multisession
```

Die Codex-Skills liegen bereits unter `.agents/skills/`. Bei einem Client mit dieser
Skill-Unterstützung lautet der Agentenauftrag (kein Shellbefehl):

```text
$speckit-implement Alle offenen Tasks nach ihren Abhängigkeiten implementieren.
Vorhandene Runtime-, Dispatch-, Recovery- und Pagingverträge wiederverwenden.
Vor Umsetzung aktuellen Deep-Dive dokumentieren,
fachliches Red vor Green beobachten, Regression, native Qualitätsprüfung und vollständigen
Codemetr-Guard ausführen. T112/T025 wiederverwenden; alle offenen Storys weiterführen.
Änderungen und Prüfnachweise
auf einem Arbeitsbranch zur Überprüfung zurückliefern; kein Merge, Deployment oder NuGet-Push.
```

Falls der Client Skills nicht automatisch erkennt, den Skill
`.agents/skills/speckit-implement/SKILL.md` mit diesem Gesamtimplementierungsauftrag laden.
Die projektlokalen Spec-Kit-Skripte verwenden PowerShell, auch unter Linux.

Die mitgelieferte Spec-Kit-Erweiterung `git` (1.0.1) ist nur für den ausdrücklichen
Aufruf installiert. Alle ihre Hooks sind in `.specify/extensions.yml` abgeschaltet:
`$speckit-specify` legt keinen Branch an, und kein Befehl committet automatisch. Jedes
Feature erhält genau einen Branch, angelegt von seinem Autor: entweder der eigene
Arbeitsbranch aus Schritt 1 oder einmalig `$speckit-git-feature` (OpenCode:
`/speckit.git.feature`) auf aktuellem `main`, das `feature/{number}-{slug}` mit der
nächsten Specnummer erzeugt. `$speckit-git-validate` prüft den Branchnamen. Die
Python-Kopie der Erweiterungsskripte ist wegen der Constitution nicht installiert.
`specify extension update git` oder eine Neuinstallation schaltet die Hooks wieder ein,
entfernt die Codex-Skills und stellt `scripts/python` wieder her. Dieser Diff ist vor
der Nutzung zu prüfen und auf die freigegebene Konfiguration zurückzuführen;
`scripts/Test-SpecKitConfiguration.ps1 -ForbidPython` prüft sie im Guard-CI und
weist eine nicht geprüfte Erweiterungsversion zurück.

## Nachweise und Repositorygrenzen

Arbeitsnachweise liegen unter [evidence](specs/001-agent-multisession/evidence/README.md).
Die [Repository-/Clone-Prüfung](specs/001-agent-multisession/evidence/repository-bootstrap.md)
dokumentiert den getesteten Initialcommit und die Grenzen dieser lokalen Prüfung.
Rohberichte/TRX/Source-Manifeste unter `artifacts/` und `TestResults/` bleiben lokal
beziehungsweise werden als CI-Artefakte übertragen; ein frischer Clone enthält sie nicht.
Fehlende Originalartefakte dürfen nicht rekonstruiert und als historische Ausführung
ausgegeben werden. Die dokumentierte anfängliche Manifestlücke bleibt offen.

Frühere Aussagen „kein Git-Repository“ in historischen Nachweisen beschreiben den damaligen
Stand. Die Initialisierung und der erste Push wurden anschließend ausdrücklich freigegeben.
Das ist keine Erfüllung von T111/Required Checks oder der noch offenen Produktabnahmen.
SharpClaw bleibt ein späterer Konsument, OpenCode **1.19.0** eine optionale Referenz.

`.gitignore` basiert auf der [offiziellen GitHub-VisualStudio-Vorlage](https://github.com/github/gitignore/blob/main/VisualStudio.gitignore),
ergänzt um lokale Agentdaten, SQLite-Zustand und die ausdrückliche Aufnahme des Coverageinventars.
Es werden keine Zugangsdaten, nativen Sessions oder produktiven Zustandsspeicher versioniert.

SQLite unterstützt explizites `SqliteStoreFactory.CreateNewAsync` beziehungsweise
`OpenExistingAsync`, Session-/Requestlookup, Session-/Bindingcommit und dauerhafte Annahme
über einen begrenzten einzelnen DBworker. Der Leaseport bereitet private Dateien vor;
Metadaten, Trustscope, Header und tatsächliches Schema werden vor Nutzung geprüft.
Wiederöffnung erhält wartende Eingaben und Sequenzen. Native DB-Busyzeit ist zunächst
fest eine Sekunde; andere Werte werden ausdrücklich vor Dateizugriff abgewiesen.
Fachliche Prozess-Kill- und native Besitznachweise bleiben offen;
[Nachweise](specs/001-agent-multisession/evidence/tdd-us5.md).

Der SQLite-Dispatchschnitt sichert Reservierung und unveränderte Eingabe vor Versand,
native Kennungs-Callbacks vor Bestätigung und bekannte Abschlüsse atomar mit Ergebnis,
History, Bindungsfortschritt und Freigabe. Session-/Request-/Historyseiten sind verfügbar.

Die SQL-Recoveryprojektion hält ungewisse Reservierungen, eröffnet persistente Fälle
und unterstützt revisionsgesicherte explizite Entscheidungen. Bootstrap behandelt alte
Running als Interrupted/Unknown; offene Fälle sind paginiert lesbar. Vor Workerstart wird
lokale Provider-/Binding-/Ausführungsunterstützung geprüft. Wieder vorhandene Registrierung
löst nur eigene Konfigurationssperren. Öffentliche Prozess-Kill-Gesamtabnahme und weitere
Lebenszyklus-/Plattformaufgaben bleiben offen; keine Produktfreigabe.

Optionale Diagnosen werden über ein hosteigenes `SessionDiagnostics` an `SessionRuntime`
übergeben und nach Runtimeende entsorgt. Bestätigte Annahmen und gespeicherte Abschlüsse
liefern strukturierte ILogger-Ereignisse und Activities mit lokalen Kennungen, Zustand,
Gewissheit, Dauer und öffentlicher Fehlerkategorie. Eingaben, Ausgaben, Bindungen und
Workspaceinhalte werden nicht gemeldet. Logger-/Listenerexceptions sind isoliert;
`FailureCount` zählt sie. Ein eigenes Diagnoseobjekt je Runtime hält außerdem BCL-Dauerhistogramme, einen Zähler beobachteter Abschlüsse und gecachte Queue-/Reservierungsgauges. Der Monitor liest initial und mit einer Sekunde Pause; fehlende oder veraltete Werte werden bei Lesefehler/Stop zurückgezogen. Die vollständige Diagnoseabnahme ist noch offen.
