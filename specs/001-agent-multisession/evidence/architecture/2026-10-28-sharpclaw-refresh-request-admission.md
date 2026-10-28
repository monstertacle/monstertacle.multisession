# Aktualisierter Zielhost und begrenzte Request-Annahme

## Vorabprüfung und Quellstände

Diese Analyse wurde vor der folgenden RequestAdmission-Erweiterung erstellt.
SharpClaw wurde aus einem sauberen main-Arbeitsbaum mit `git pull --ff-only origin main`
auf **7854a122218382156789d520b0a00f2060b1f8f3** aktualisiert. Gitstatus danach sauber.
Der Vergleich bezieht ausdrücklich den letzten analysierten Stand **71d3cbe...**
ein, nicht nur den vor dem Pull bereits lokal vorhandenen 85282dd-Stand.
Keine SharpClaw-Quelldatei, Instanz, Session, Konfiguration oder Anmeldung verändert.
Keine dortigen Python-Skripte oder Laufzeittests ausgeführt.

### Erneuter Deep-Dive im Zielhost

- Git-Diff 71d3cbe..7854a12: IAgentBackend, ICliProtocol, RunContext, Session,
  Observation, AgentPluginContract, Registry und das OpenCode-Protokoll sind unverändert.
  Engine ändert nur den zusätzlichen Handoverparameter der ProgressMonitor-Beobachtung;
  ExecuteAsync-/Attempted-/native-ID-/Abschlusslogik bleibt gleich.
- Aktuell gelesen: IAgentBackend.ExecuteAsync und Session/RunContext, OpenCodeProtocol
  (weiterhin SupportedVersion=1.19.0), die ADRs 0053–0058 sowie RecoveryCoordinator,
  SubagentOutputContract, ContextAdmissions und ContextSourceCache.
- Neue Fleet-Recovery verarbeitet explizite revisionsgebundene Betreiberabsichten,
  keine automatische native Wiederholung und keinen Herstellerfallback.
- Aufgabenbezogene Fortschritts- und Zustandsbeobachtung ist Hostautorität, kein
  Erfolgsbeweis durch Timer, Prozessanwesenheit oder Zwischenereignis.
- Deklarierte Artefaktziele bleiben vom Ergebnismanifest getrennt. ContextSourceCache
  hält geprüfte Bytes hashgebunden statt später geänderte Dokumente nachzulesen.
  Kontext-/Artefakt-/Fleetverwaltung bleibt beim Host. Multisession übernimmt die
  vom Host vorbereitete neue Eingabe unverändert; sie interpretiert weder Dateipfade
  noch baut sie Tools, Agent-Prompts, Context-Replay oder diese Hostdienste nach.
- Der unveränderte generische Backendvertrag bleibt als Referenz passend. Das ist
  nur ein Quellabgleich, keine ausgeführte Binär-/Produktintegration oder Deploymentfreigabe.

Verifizierte aktuelle SHA256-Belege (Dateien relativ zum SharpClaw-Repository):

| Symbol / Datei | SHA256 |
| --- | --- |
| IAgentBackend | `A72CDAD6084E287FADE589EBC277DA86EA6206006BFAC1B481C7A044BB7552DA` |
| ICliProtocol | `E216332A737F4484560FB3AA97B9E1CED5DA44BAD2EDCF659C2E8C87B82DC132` |
| Session | `776D4A4AC36DA814D0FC197D2DBE8BD77D42DD8C334D70561D95ABB33B0C04C4` |
| RunContext | `E1E47B6A6F63E0E90C7980C8CBDDFA82D1E26E5451D0D15CA8E350E1BF1FC706` |
| plugins/OpenCode/OpenCodeProtocol | `779C090F1C27139EB2D3B86088649B8442B2917A5DFA6EEBA4172AEB3A7A58F9` |
| ContextAdmissions | `C24826607609810439364D7C7E3457958CB1FAA121A8E7B2B59239045B2BBFE1` |
| ContextSourceCache | `7AB0EA05830B8F20A654833CDEB9182994BE111C7E918AA4C7DE8DDC8B90AE72` |
| SubagentOutputContract | `7412D6C2820275398D635542BECF76036429CA845C82A8632469FA0BAA52D8DE` |
| RecoveryCoordinator | `E796D58DFBB74389A76E563B4C0F857865FDDA96D5E1E8B333EE7BCFFDDE4197` |

## Bestandscode in Multisession

Bezug: FR-005–FR-008, FR-016/FR-018/FR-027; T016/T022, spätere Deadline-/Persistenzarbeit
bleibt getrennt. Aktueller Bestand wurde nicht durch bloßes Lesen des Plans ersetzt:

| Datei / Symbol | SHA256 |
| --- | --- |
| Contracts/IRequests.cs | `351A08665898BAC79D28BCBFB93267BFAF9D93977B41EB1D69C99AC57A0C9410` |
| Contracts/ISessionStore.cs | `68C0497D26EA52EF921EF5BD6248AAF43353ECA207E946503777EAB11FB78E3E` |
| Contracts/ISessionQueryStore.cs | `468FDF83D8695A00F56F88D400403874D4E97DD4366F4C362AB01FF56BB23437` |
| Contracts/SubmitRequest.cs | `176BFEB12D76B32253EFB76DED5EAB88AC059C64043E3DCB0956812858035FAA` |
| Contracts/Utf8MessageLimits.cs | `DAF9F29787BF2A4A30098337983F6A6ADEA6A281682097CE53AFDA3475549311` |
| Contracts/RuntimeOptions.cs | `7B57090014903EFA6A3EA1A9008C13D9533DA88EF4266CC8FF63779A7E7994BC` |
| UnitTests/Fixtures/InMemorySessionStore.cs | `221421CA895367F18CC00DBE7CE945972B1C301032B3E4A4E26911C498659D1E` |

- IRequests besitzt schon Submit/Get/List/Cancel-Signaturen. SessionRuntime.Requests
  ist noch ein Nicht-implementiert-Gerüst; es gibt keinen produktiven RequestAdmission-
  Aufrufer und noch keine RequestAdmission-Klasse. Kein bestehender Scheduler oder
  SQLite-Code darf behauptet werden; diese Teile fehlen tatsächlich.
- SessionCatalog speichert lokale Identität vor Open und markiert unsichere Öffnungen
  Opening/RecoveryRequired. Die Annahme muss diese Zustände lesen und respektieren,
  statt eine solche Sitzung durch eine erfolgreiche neue Modellanfrage zu entsperren.
- ISessionStore.CommitAcceptanceAsync ist die vorhandene atomare fachliche Grenze
  für Input/Sequence/Queuekapazität; der In-Memory-Store enthält dort noch einen Stub.
  GetRequestAsync ist ebenfalls Stub. Diese Testdoubles werden für neue Fälle konkret
  erweitert, ohne sie als Produktpersistenz oder SQLite-Transaktionsbeweis auszugeben.
- Utf8MessageLimits und RuntimeDefaults.MaximumTimerTimeout sind vorhandene gemeinsame
  Regeln. Wiederverwenden; die private Deadlineprüfung von RuntimeOptions kann bei
  grüner bestehender Regression mechanisch gemeinsam genutzt werden.

## Entscheidung und begrenzter Testplan

RequestAdmission implementiert zunächst Submit und GetRequest. Es hat nur Store und
validierte RuntimeOptions als Abhängigkeiten, keinen Herstelleradapter und keine
eigene externe Ausführung. Neue Eingabe unverändert zum atomaren Store reichen;
Receipt niemals vor abgeschlossenem Commit, keine Deduplizierung gleicher Texte.
Queue und Sequence sind Storeautorität, keine konkurrierenden Zähler im Kern.

Vor Übergabe Fehler für unbekannte, nicht offene oder blockierte Sitzung, leeren/
zu großen UTF-8-Input, ungültige Ausführungsfrist und fehlenden Schreib-Workspace
prüfen. Ein expliziter Schreibschlüssel darf einer vorhandenen festen Sitzungsschlüssel-
Zuordnung nicht widersprechen. Kein stilles Umhängen der Session oder des Providers.
Vorbelegte Sitzungsschlüssel ersetzen in diesem Schnitt nicht den vom Auftraggeber
explizit geforderten Schreibschlüssel; der Host muss konsistente Schlüssel liefern.

Zuerst ausführbare Tests mit fachlicher Assertion Red, danach jeweils minimale Umsetzung:
1. Beleg nach Commit, neue ID/Sequence, unveränderter vorbereiteter Text.
2. Explizite Commitbarriere: Pending vor Storefreigabe; kein Adapteraufruf.
3. Isolierter Requestlookup; keine fremden Storekennungen und Cancellation weiterreichen.
4. Zustands-/Input-/Frist-/Workspace-Abweisungen ohne Storecommit.
5. Begrenzte globale Testqueue, getrennte Sessionsequenzen, gleiche Texte als neue Requests.
6. Cancellation nach bekannt erfolgreichem Commit löscht Receipt/Request nicht rückwirkend.

Regressionsrisiken: Interpretation veränderlicher Hostdateien statt vorbereiteter Texte,
Annahme in Opening/RecoveryRequired, vorzeitiger Beleg, lokales Nachzählen von Queue/Sequence,
stiller neuer Provider, nachträgliches Löschen bei Caller-Cancel. Bestehende Snapshot-/
Sessiontests und neue Admissiontests prüfen den Kern gegen kontrollierte Storezustände.
Fake-Queueprüfung belegt ausdrücklich keine realen SQLite-Races oder Crashhaltbarkeit.

Nicht Teil dieses Schnitts: List-Paging, Cancel-Ausführung, Dispatcher/Reservations,
AdmissionTimeout-Timer, native Provider, Livekosten oder Host-/Altzustandsmigration.
Das sind vorhandene weitere Tasks; kein unvollständiger Runtimepfad wird als fertig
freigegeben. Für diesen begrenzten Annahmeschnitt keine offene Architekturfrage.
