# Feature Specification: Herstellerübergreifende Agent-Multisession

**Feature Branch**: Logische Feature-ID `001-agent-multisession`; Repositoryinitialisierung
und erster Push auf `main` am 2026-10-28 ausdrücklich freigegeben. Keine automatische Featurebranchpflicht.

**Created**: 2026-10-26

**Updated**: 2026-10-28

**Status**: Inkrementelle Implementierung; vollständige Abnahme und Produktfreigabe offen.

**Input**: Zusammenfassung der Nutzerbeschreibung: Eine oder mehrere wiederverwendbare
NuGet-Bibliotheken für parallele Sitzungen verschiedener AI-Agents und Hersteller.
Spätere Hosts sollen als Konsolenanwendung, Windows-Dienst oder Linux-Daemon dauerhaft
Anfragen entgegennehmen können. Externe Rechercheprojekte sind kein Bestandteil der Lösung.

**Clarifications** (2026-10-26): Herstellerunabhängige Nutzung nach dem fachlichen Vorbild
von OpenCode: konfigurierbare Provider und Modelle statt einer fest begrenzten Agent-Auswahl.
Sitzungen und Verlauf sowie angenommene wartende Anfragen MÜSSEN einen Host-Neustart überstehen
(Antwort auf Frage 2: B+C). Ungewisse laufende Ausführungen werden nicht automatisch wiederholt.
Als Planungspräferenz sollen ein gemeinsamer plattformneutraler Kern und davon abhängige
Windows-/Linux-Bibliotheken geprüft werden. Die endgültige Paketanzahl richtet sich nach
den tatsächlich benötigten Plattformfunktionen. Externe Referenzprojekte dienen nur der
temporären Ideenfindung; nicht erforderliche temporäre Analyseunterlagen werden nach
Übernahme der Erkenntnisse entfernt. Verpflichtende Architektur-/Zielhostnachweise nach
Constitution II bleiben davon ausgenommen und werden bereinigt dauerhaft aufbewahrt.
Davon zu unterscheiden ist die neue Produktanforderung: SharpClaw ist ein ausdrücklich
benannter langfristiger Zielkonsument der Bibliothek, keine Bibliotheksabhängigkeit.

## Clarifications

### Session 2026-10-27

- Q: Soll die Bibliothek pro Agent-Profil wahlweise reine Modellantworten oder einen Agent-Ablauf mit freigegebenen Werkzeugen unterstützen? → A: Zunächst wurden beide Betriebsarten mit automatischer Werkzeugnutzung gewählt. Diese vorläufige Entscheidung für eine eigene Werkzeugschleife wurde durch die abschließende Umfangspräzisierung dieser Sitzung ersetzt: Sitzungsfunktionen werden ausschließlich vom Provider verantwortet.
- Q: Soll eine Bibliotheksinstanz nur einem gemeinsamen Vertrauensbereich dienen oder mehrere voneinander abgeschottete Benutzer beziehungsweise Kunden unterstützen? → A: Ein Vertrauensbereich je Instanz; der Host kontrolliert den Zugriff. Sitzungen bleiben getrennt. Eine interne Mandantentrennung ist nicht Teil des Lieferumfangs.
- Q: Soll die Bibliothek die Kontextverwaltung vollständig dem jeweiligen Provider überlassen und bei fehlender Unterstützung eine verständliche Kontextgrenzen-Meldung liefern? → A: Ja. Die Bibliothek verwaltet ausschließlich mehrere Sitzungen; deren Funktionen, einschließlich Kontextverwaltung und Verdichtung, liegen beim Provider. Sie führt weder eigene Verdichtung noch eine eigene Werkzeugschleife aus und entfernt Inhalte nicht stillschweigend. Provider- beziehungsweise modellbedingte Grenzen werden als solche gemeldet.

Erneute Klärungsprüfung vom 2026-10-27 nach Ergänzung von FR-027 und Constitution v1.4.0:
Der Nutzer verlangt verbindliches TDD. Der Ablauf Red–Green–Refactor ist damit bereits
entschieden und erfordert keine zusätzliche Auswahlfrage. Funktionsumfang, Providergrenze,
Vertrauensbereich und langfristige Einbettung bleiben unverändert. Die Abnahmeszenarien
sind vor der jeweiligen Verhaltensimplementierung als ausführbare Tests zu konkretisieren.
Keine neue kritische fachliche Unklarheit festgestellt; Paket-/Technikdetails verbleiben im Plan.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Unabhängige Sitzungen parallel nutzen (Priority: P1)

Als Integrator möchte ich mehrere Agent-Sitzungen unterschiedlicher Hersteller gleichzeitig
betreiben, damit unabhängige Anfragen einander nicht blockieren und ihre Kontexte getrennt bleiben.

**Why this priority**: Herstellerübergreifende Parallelität und getrennte Sitzungskontexte
sind der zentrale Nutzen der Bibliothek.

**Independent Test**: Zwei kontrolliert verzögerte Agent-Anbindungen unterschiedlicher
Hersteller bearbeiten je eine Sitzung gleichzeitig. Antworten bleiben eindeutig zugeordnet.

**Acceptance Scenarios**:

1. **Given** zwei konfigurierte Hersteller und freie Kapazität, **When** je eine Anfrage
   in zwei Sitzungen eingeht, **Then** beginnen beide Ausführungen, bevor die erste endet.
2. **Given** zwei Sitzungen mit unterschiedlichen Inhalten, **When** Folgeanfragen eintreffen,
   **Then** erhält jeder Agent ausschließlich den seiner Sitzung zugeordneten Kontext.
3. **Given** mehrere Anfragen an dieselbe Sitzung, **When** eine Ausführung bereits läuft,
   **Then** werden weitere Anfragen in Annahmereihenfolge bearbeitet, ohne Kontextüberschneidung.

---

### User Story 2 - Agent auswählen und Sitzung fortführen (Priority: P1)

Als Integrator möchte ich einen konfigurierten Agent auswählen, eine Sitzung eröffnen und
weitere Anfragen an dieselbe Sitzung richten, ohne herstellerspezifische Abläufe selbst zu steuern.

**Why this priority**: Eine einheitliche Sitzungsverwaltung macht die Funktion in anderen
Anwendungen wiederverwendbar und verhindert eine Bindung an einen einzigen Hersteller.

**Independent Test**: Ein Agent bearbeitet eine erste Anfrage und eine kontextabhängige
Folgeanfrage; Sitzung, Status und Ergebnis können unabhängig von anderen Sitzungen abgefragt werden.

**Acceptance Scenarios**:

1. **Given** ein gültiger konfigurierter Agent, **When** eine Sitzung eröffnet wird,
   **Then** erhält der Integrator eine eindeutige Sitzungskennung und die Agent-Zuordnung.
2. **Given** eine bestehende Sitzung, **When** eine Folgeanfrage eingeht,
   **Then** bleibt die Sitzung demselben Agent und ihrem bisherigen Kontext zugeordnet.
3. **Given** eine unbekannte Agent-Kennung oder ungültige Konfiguration, **When** eine Sitzung
   eröffnet werden soll, **Then** erfolgt ein eindeutiger Fehler ohne Ersatz durch einen anderen Agent.
4. **Given** eine geschlossene Sitzung, **When** eine neue Anfrage für sie eingeht,
   **Then** wird diese abgelehnt; eine neue Sitzung erfordert eine neue Kennung.
5. **Given** ein weiterer Zugang mit bereits unterstütztem Herstellervertrag, **When** der
   Integrator Endpunkt, Modelle und Zugangsdatenreferenz konfiguriert, **Then** kann ein
   Agent-Profil ihn ohne Änderung der Sitzungsverwaltung nutzen.
6. **Given** ein Hersteller mit bisher nicht unterstütztem Vertrag, **When** eine passende
   Erweiterung eingebunden wird, **Then** können dessen Modelle über dieselben Sitzungsabläufe
   genutzt werden, ohne bestehende Provider oder die konsumierende Anwendung umzuschreiben.
7. **Given** zwei Agent-Profile mit unterschiedlicher Aufgabe, **When** beide dasselbe
   verfügbare Modell verwenden, **Then** bleiben Profile, Anweisungen und Sitzungskontexte getrennt.
8. **Given** eine bestehende Provider-Sitzung, **When** weitere Anfragen eingehen,
   **Then** übergibt die Bibliothek sie der zugeordneten Provider-Sitzung, ohne selbst
   Werkzeugschleifen, Zusammenfassungen oder andere Sitzungsfunktionen zu implementieren.
9. **Given** eine vom Provider gemeldete Kontextgrenze, **When** eine Folgeanfrage nicht
   verarbeitet werden kann, **Then** wird der Fehler der betroffenen Anfrage zugeordnet;
   gespeicherte Inhalte bleiben unverändert und werden weder gekürzt noch zusammengefasst.
10. **Given** ein Provider mit eigener automatischer Kontextverwaltung, **When** dieser
    die Sitzung intern verdichtet, **Then** bleibt die Bibliothek bei derselben Sitzungszuordnung
    und übergibt weitere Anfragen, ohne eine zusätzliche eigene Verdichtung auszuführen.

---

### User Story 3 - Fehler, Abbruch und Auslastung beherrschen (Priority: P2)

Als Betreiber möchte ich Ausführungen beobachten, abbrechen und begrenzen können,
damit der Ausfall eines Agent keine anderen Sitzungen stilllegt oder Ressourcen unbeschränkt belegt.

**Why this priority**: Dauerbetrieb erfordert kontrollierbare Fehler und begrenzte Ressourcen.

**Independent Test**: Während eine Sitzung fehlschlägt oder abgebrochen wird, schließt
eine unabhängige Sitzung ihre Anfrage erfolgreich ab; alle Ergebnisse bleiben zuordenbar.

**Acceptance Scenarios**:

1. **Given** zwei aktive Sitzungen, **When** ein Hersteller ausfällt, **Then** meldet nur die
   betroffene Ausführung einen Fehler; die andere Sitzung arbeitet weiter.
2. **Given** eine laufende oder wartende Anfrage, **When** der Integrator sie abbricht,
   **Then** wird ihr Ergebnis als abgebrochen gekennzeichnet und nicht als erfolgreich gemeldet.
3. **Given** ausgeschöpfte Ausführungskapazität und eine volle Warteschlange, **When** eine
   weitere Anfrage eingeht, **Then** wird sie mit einer eindeutigen Überlastmeldung abgelehnt.
4. **Given** aktive Sitzungen, **When** ein kontrolliertes Herunterfahren beginnt, **Then**
   werden keine neuen Anfragen angenommen; offene Anfragen enden innerhalb der konfigurierten
   Frist regulär oder mit Abbruch beziehungsweise ausdrücklich ungewissem Ausführungsstatus.

---

### User Story 4 - In unterschiedliche dauerlaufende Hosts einbetten (Priority: P2)

Als Anwendungsentwickler möchte ich die Funktion als wiederverwendbare Bibliothek beziehen
und in einen späteren Konsolenprozess, Windows-Dienst oder Linux-Daemon einbetten, ohne
die Sitzungslogik für jede Betriebsart neu zu entwickeln.

**Why this priority**: Liefergegenstand sind Bibliotheken, nicht ein fest verdrahteter Dienst.

**Independent Test**: Eine kleine konsumierende Anwendung bindet die Pakete ein, startet die
Verarbeitung und bearbeitet zeitversetzt eintreffende Anfragen ohne Neustart der Bibliothek.

**Acceptance Scenarios**:

1. **Given** die ausgelieferten NuGet-Pakete, **When** eine konsumierende Anwendung sie
   einbindet, **Then** kann sie Agent-Sitzungen ohne Quellcodekopie oder Installation
   eines fremden Referenzprojekts nutzen.
2. **Given** eine gestartete Verarbeitung ohne aktuelle Anfragen, **When** später eine neue
   Anfrage eingeht, **Then** wird diese ohne Neustart bearbeitet, bis der Host das Beenden anfordert.
3. **Given** ein Konsolenprozess unter Windows beziehungsweise Linux, **When** derselbe
   Sitzungsablauf durchlaufen wird, **Then** gelten dieselben fachlichen Zusicherungen.
4. **Given** ein Host mit eigenem Lebenszyklus und Anfrageeingang, **When** er die Bibliothek
   einbindet, **Then** kann er Start, Anfrageübergabe und Beenden selbst steuern; die Bibliothek
   erzwingt weder einen Netzwerkport noch eine Dienstinstallation.
5. **Given** die gemeinsame Bibliothek ohne konkrete Plattformintegration, **When** ein
   Integrator die Sitzungsabläufe mit kontrollierten Test-Anbindungen prüft, **Then** sind
   weder Windows- noch Linux-spezifische Laufzeitmechanismen erforderlich.
6. **Given** ein Host mit der passenden Plattformintegration, **When** die zugesicherten
   Sitzungsabläufe ausgeführt werden, **Then** gelten dieselben fachlichen Regeln auf beiden
   Zielsystemen; nicht unterstützte Plattformfähigkeiten werden ausdrücklich gemeldet.
7. **Given** zwei vom Host getrennt konfigurierte Instanzen mit unterschiedlichen
   Vertrauensbereichen und getrenntem Zustandsspeicher, **When** Sitzungen und Provider
   registriert werden, **Then** kann eine Instanz nicht über die Bibliotheksoperationen
   auf Sitzungen, Ergebnisse oder Providerregistrierungen der anderen Instanz zugreifen.
8. **Given** eine bestehende konsumierende Anwendung mit eigenem Lebenszyklus und eigenen
   Provider-Anbindungen, **When** sie die NuGet-Bibliothek im eigenen Prozess einbindet,
   **Then** kann sie Sitzungseröffnung, parallele Anfragen, Folgeanfragen, Status, Abbruch
   und Beenden über öffentliche Verträge verwenden, ohne Quellcode zu kopieren oder einen
   zusätzlichen Multisession-Dienst starten zu müssen. SharpClaw ist der langfristig
   vorgesehene produktive Zielkonsument dieser Einbettung.

---

### User Story 5 - Sitzungen und wartende Aufträge nach Neustart fortsetzen (Priority: P1)

Als Betreiber möchte ich nach einem geplanten oder unerwarteten Host-Neustart vorhandene
Sitzungen mit ihrem Verlauf weiterverwenden und angenommene wartende Anfragen abarbeiten,
ohne Aufträge zu verlieren oder möglicherweise ausgeführte Arbeiten doppelt auszulösen.

**Why this priority**: Neustartfestigkeit ist ausdrücklich Teil des ersten Lieferumfangs
und Voraussetzung für verlässlichen Dauerbetrieb.

**Independent Test**: Zwei Sitzungen enthalten abgeschlossene, wartende und eine laufende
Anfrage. Nach erzwungenem Prozessende und Neustart bleiben Kontext und Zuordnung erhalten;
wartende Anfragen werden fortgesetzt, die unterbrochene Ausführung wird nicht blind wiederholt.

**Acceptance Scenarios**:

1. **Given** eine Sitzung mit abgeschlossener Anfrage, **When** der Host mit demselben
   gespeicherten Zustand neu startet, **Then** sind Kennung, Profil-/Modellzuordnung, Verlauf
   und Ergebnis wieder verfügbar; eine Folgeanfrage kann den bisherigen Kontext verwenden.
2. **Given** dauerhaft angenommene, noch nicht gestartete Anfragen, **When** der Host neu
   startet, **Then** werden diese ohne erneute Einreichung unter ihren bisherigen Kennungen
   und in der ursprünglichen Reihenfolge je Sitzung abgearbeitet, unter Beachtung aller Limits.
3. **Given** eine vor dem Prozessende bereits gestartete Anfrage mit unbestätigtem Ausgang,
   **When** der Host neu startet, **Then** wird sie als unterbrochen mit ungewissem Ausgang
   ausgewiesen und weder als erfolgreich gemeldet noch automatisch erneut ausgeführt.
4. **Given** eine solche ungewisse Anfrage, **When** Folgeanfragen derselben Sitzung warten,
   **Then** bleiben sie bis zur ausdrücklichen Klärung durch den Integrator gesperrt;
   unabhängige Sitzungen dürfen weiterarbeiten. Eine erneute Ausführung erfordert einen
   expliziten neuen Auftrag und einen Hinweis auf mögliche doppelte externe Wirkung.
5. **Given** ein nicht beschreibbarer Zustandsspeicher, **When** eine Anfrage eingereicht
   wird, **Then** wird keine dauerhafte Annahme bestätigt und keine externe Ausführung gestartet.
6. **Given** eine Aufbewahrungsregel und löschbare abgeschlossene Sitzungen, **When** der
   Integrator deren Löschung auslöst, **Then** sind Verlauf und Ergebnisse nicht mehr über
   die Bibliothek abrufbar; wartende, laufende oder ungeklärte Aufträge werden nicht still gelöscht.

---

### Edge Cases

- Fehlende Zugangsdaten, nicht verfügbare Agent-Programme oder ungültige Agent-Konfiguration:
  betroffene Sitzung kann nicht starten; kein stiller Herstellerwechsel.
- Gleichzeitige Anfragen an dieselbe Sitzung: geordnete Bearbeitung statt paralleler Kontextänderung.
- Abbruch gleichzeitig mit Fertigstellung: genau ein endgültiges lokales Ergebnis je Anfrage.
- Zeitlimit oder Verbindungsverlust nach Übergabe an einen Hersteller: ungewisse externe
  Ausführung ausdrücklich melden; keine automatische Wiederholung mit möglichen Seiteneffekten.
- Eine lokal bekannte unzulässige Konfiguration vor Übergabe ablehnen; erst beim Provider
  erkennbare Funktionsgrenzen als Fehler der betroffenen Anfrage melden. Ein vollständiger
  Featurekatalog oder vorgelagerter Nachweis aller Herstellerfähigkeiten ist nicht erforderlich.
- Schließen einer Sitzung mit offenen Anfragen: neue Anfragen ablehnen, offene kontrolliert beenden.
- Vollständige Auslastung: begrenzte Warteschlangengröße und eindeutige Ablehnung bei
  vollem Limit. Für bereits angenommene wartende Anfragen besteht keine feste Zusage
  einer maximalen Wartezeit; der Host kann sie ausdrücklich abbrechen.
- Zwei schreibende Agent-Aufträge betreffen denselben deklarierten Arbeitsbereich:
  kein gleichzeitiger Schreibzugriff innerhalb derselben Bibliotheksinstanz.
- Host-Absturz zwischen externer Übergabe und gespeichertem Ergebnis: ungewissen Zustand
  ausweisen, keine automatische Wiederholung oder Fortsetzung abhängiger Anfragen.
- Host-Absturz nach dauerhafter Annahme, aber vor Rückmeldung an den Aufrufer: Anfrage
  bleibt unter ihrer Kennung auffindbar; erneutes Einreichen ist nicht automatisch dedupliziert.
- Fehlender Provider, geänderte Modellverfügbarkeit oder abgelaufene Zugangsdaten nach Neustart:
  betroffene Sitzung blockieren und Ursache melden; kein stiller Ersatz, andere Sitzungen laufen weiter.
- Nicht verfügbarer, voller oder beschädigter Zustandsspeicher: keine ungesicherte Annahme
  oder unbemerkte Ersetzung durch einen leeren Zustand; betroffene Verarbeitung sicher anhalten.
- Möglicherweise weiterlaufender externer Agent nach Host-Absturz: betroffenen Arbeitsbereich
  bis zur bestätigten Klärung sperren, nicht nur die unterbrochene Sitzung.
- Ein konfigurierbarer kompatibler Zugang unterstützt eine erwartete Fähigkeit nicht:
  Konfiguration beziehungsweise Ausführung ablehnen; Protokollähnlichkeit ist kein Fähigkeitsnachweis.
- Provider meldet eine Kontextüberschreitung oder eine nicht unterstützte Sitzungsfunktion:
  Ursache zuordnen, keine eigene Ersatzfunktion, Verdichtung oder stilles Abschneiden starten.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Die Lösung MUSS als eine oder mehrere NuGet-Bibliotheken konsumierbar sein,
  ohne ein fremdes Referenzprojekt oder einen bestimmten Dienst-Host vorauszusetzen.
  Abnahme: User Story 4.1.
- **FR-002**: Die Lösung MUSS mindestens zwei Hersteller innerhalb derselben Instanz
  gleichzeitig unterstützen. Abnahme: User Story 1.1 und SC-001.
- **FR-003**: Agent-Profile, Provider-Zugänge und Modelle MÜSSEN getrennt wählbar sein.
  Für bereits unterstützte Herstellerverträge MÜSSEN zusätzliche Zugänge und Modelle
  durch Konfiguration von Endpunkt, Modellkennung und Zugangsdatenreferenz nutzbar sein,
  ohne Änderungen an Sitzungsverwaltung oder konsumierender Anwendung. Für abweichende
  Herstellerverträge MUSS eine Erweiterung möglich sein, ohne bestehende Integrationen
  umzuschreiben. Eine vollständige Gleichwertigkeit aller Hersteller wird nicht zugesichert.
  Abnahme: User Story 2.5–2.7 und SC-008.
- **FR-004**: Integratoren MÜSSEN konfigurierte Provider-Zugänge und Agent-Profile erkennen
  und gezielt auswählen können. Ungültige lokale Konfiguration MUSS vor Beginn der
  betroffenen Ausführung abgewiesen werden. Providerseitige Konfigurations- und
  Funktionsfehler MÜSSEN eindeutig gemeldet werden; ein eigener Katalog oder Nachbau
  sämtlicher Sitzungsfunktionen ist nicht erforderlich. Abnahme: User Story 2.3 und 2.9.
- **FR-005**: Integratoren MÜSSEN Sitzungen erstellen, ihren Zustand abfragen und sie
  schließen können. Jede Sitzung MUSS eine eindeutige Kennung und feste Agent-Zuordnung
  besitzen. Profil, Provider, Modell und kontextrelevante Einstellungen MÜSSEN bei Erstellung
  festgehalten werden; spätere Konfigurationsänderungen dürfen laufende oder wiederhergestellte
  Sitzungen nicht unbemerkt umstellen. Abnahme: User Story 2.1–2.4 und 5.1.
  Eine ungewisse Sitzungseröffnung MUSS auch ohne bereits vorhandene Anfrage auffindbar
  und ausdrücklich klärbar bleiben; eine neue native Sitzung darf nicht still erzeugt werden.
- **FR-006**: Jede Anfrage und ihr Ergebnis MÜSSEN eindeutig einer Sitzung und einer
  Anfragekennung zugeordnet werden. Sitzungsverläufe und Zugangsdaten dürfen nicht
  versehentlich zwischen Sitzungen oder Herstellern geteilt werden. Abnahme: User Story 1.2.
- **FR-007**: Folgeanfragen MÜSSEN den bisherigen Kontext ihrer Sitzung nutzen können.
  Anfragen derselben Sitzung MÜSSEN in Annahmereihenfolge ausgeführt werden.
  Abnahme: User Story 1.3 und 2.2.
- **FR-008**: Unabhängige Sitzungen MÜSSEN bis zur eingestellten Kapazität parallel arbeiten.
  Gleichzeitige Ausführungen und wartende Anfragen MÜSSEN konfigurierbar begrenzt sein;
  volle Warteschlangen MÜSSEN weitere Anfragen ablehnen. Abnahme: User Story 1.1 und 3.3.
  FIFO gilt je Sitzung. Zwischen zulassungsfähigen Sitzungsköpfen hat die älteste Annahme
  Vorrang; blockierte Köpfe dürfen unabhängige zulassungsfähige Köpfe nicht sperren.
  Eine feste Wartezeit- oder allgemeine Starvation-Freiheitsgarantie wird nicht zugesichert.
- **FR-009**: Der Integrator MUSS je Anfrage mindestens wartend, laufend, erfolgreich,
  fehlgeschlagen, abgebrochen und zeitlich abgelaufen unterscheiden können. Bei ungewisser
  externer Ausführung MUSS diese Unsicherheit zusätzlich erkennbar sein. Endzustände MÜSSEN
  eindeutig sein. Abnahme: User Story 3 und Abbruch-/Verbindungsverlust-Edge Cases.
  Die kanonischen Zustände sind Queued, Running, Succeeded, Failed, Cancelled, TimedOut
  und Interrupted. Interrupted bezeichnet einen beim Wiederanlauf ungeklärt gestarteten
  Auftrag. Ausführungsgewissheit, Sitzungslebenszyklus und Blockierung sind getrennte
  Angaben; ein lokaler Endzustand allein beweist kein Ende externer Arbeit.
- **FR-010**: Integratoren MÜSSEN einzelne wartende und laufende Anfragen abbrechen sowie
  Zeitlimits festlegen können. Die lokale Bearbeitung MUSS enden; kann eine externe
  Ausführung nicht bestätigt beendet werden, MUSS dies gemeldet werden. Abnahme: User Story 3.2.
- **FR-011**: Fehler einer Sitzung dürfen unabhängige Sitzungen nicht beenden.
  Zugangsdatenfehler, Überlastung, Zeitüberschreitung und Ausführungsfehler MÜSSEN unterscheidbar
  sein. Abnahme: User Story 3.1 und die Fehler-Edge Cases.
  Weiterarbeit setzt freie verbleibende Ausführungskapazität voraus. Sind alle Slots
  durch laufende oder ungewisse Arbeit reserviert, bleiben weitere Anfragen wartend;
  zur scheinbaren Fehlerisolation dürfen keine Limits oder Reservierungen umgangen werden.
- **FR-012**: Es darf weder ein stiller Herstellerwechsel noch eine automatische
  Wiederholung einer möglicherweise bereits ausgeführten Anfrage erfolgen.
  Abnahme: User Story 2.3 und der Verbindungsverlust-Edge Case.
- **FR-013**: Die Bibliotheken MÜSSEN bis zum angeforderten Beenden für weitere Anfragen
  verfügbar bleiben und ein kontrolliertes Herunterfahren mit endlicher, konfigurierbarer
  Frist erlauben. Abnahme: User Story 3.4 und 4.2.
  Ein kontrollierter Stop beendet verbliebene wartende Anfragen ausdrücklich als
  Cancelled/NotStarted; laufende Anfragen enden bestätigt oder mit ausgewiesener
  Ungewissheit. Bei Absturz nicht abschließend beendete Queued-Anfragen bleiben
  wiederaufnehmbar. Diese Regel unterscheidet bewusst Stop von Prozessabsturz.
- **FR-014**: Die Bibliotheken MÜSSEN unter Linux und Windows denselben fachlichen Vertrag
  erfüllen und unabhängig von Konsole, Windows-Dienst oder Linux-Daemon einbettbar sein.
  Dienstinstallation und Anfrage-Transport liegen beim späteren Host. Abnahme: User Story 4.3–4.4.
- **FR-015**: Status- und Fehlerdiagnosen MÜSSEN Anfrage und Sitzung zuordnen lassen, dürfen
  aber weder Zugangsdaten noch standardmäßig vollständige vertrauliche Anfrageinhalte
  offenlegen. Abnahme: Diagnoseprüfung der Szenarien aus User Story 3.
- **FR-016**: Bei Agent-Aufträgen mit Schreibzugriff MUSS ein deklarierter gemeinsamer
  Arbeitsbereich innerhalb derselben Instanz gegen gleichzeitige schreibende Ausführungen
  geschützt werden; unabhängige Arbeitsbereiche dürfen weiter parallel arbeiten.
  Abnahme: Arbeitsbereich-Edge Case mit einem unabhängigen dritten Auftrag.
- **FR-017**: Die Lieferung MUSS eine nachvollziehbare Anleitung für Paketbezug,
  Agent-Konfiguration, erste Sitzung, Folgeanfrage, Abbruch und Beenden auf beiden
  Zielplattformen enthalten. Abnahme: SC-006 und User Story 4.
- **FR-018**: Sitzungskennungen, verwendete Profile/Provider/Modelle, für Folgeanfragen
  erforderlicher Kontext, Ergebnisse und Anfragezustände MÜSSEN Host-Neustarts überstehen.
  Angenommene, noch nicht gestartete Anfragen MÜSSEN nach Neustart unter denselben Kennungen
  und in derselben Reihenfolge je Sitzung weiterbearbeitet werden. Grenzen und Sperren
  gelten auch bei Wiederherstellung. Abnahme: User Story 5.1–5.2 und SC-009.
- **FR-019**: Eine Anfrage darf erst nach dauerhafter Sicherung ihrer Eingabe, Kennung und
  Sitzungszuordnung als angenommen bestätigt oder extern gestartet werden. Speicherausfälle
  MÜSSEN sichtbar sein; fehlender oder beschädigter Zustand darf nicht still durch einen
  leeren Zustand ersetzt werden. Abnahme: User Story 5.5 und Speicher-Edge Cases.
  Öffentliche Fehler MÜSSEN nachgewiesene Nichtannahme von unbekanntem Commitausgang
  unterscheiden. Eine vor Commit erzeugte lokale Kandidatenkennung dient dem Nachschlagen,
  nicht als Receipt oder Deduplizierungszusage; bestätigter Commit bleibt angenommen.
- **FR-020**: Nach Neustart MÜSSEN bereits gestartete Anfragen ohne gesicherten Endzustand
  als unterbrochen mit ungewissem Ausgang erkennbar sein. Ohne ausdrückliche Klärung dürfen
  weder automatische Wiederholung noch weitere Ausführungen in der betroffenen Sitzung
  oder einem möglicherweise noch belegten Arbeitsbereich erfolgen. Unabhängige Sitzungen
  MÜSSEN nutzbar bleiben. Abnahme: User Story 5.3–5.4 und der externe-Agent-Edge Case.
  Eine Schließanforderung entfernt keine ungewisse Reservierung: Die Sitzung bleibt
  Closing und blockiert, bis die Beendigung geklärt ist. Erst dann ist Closed zulässig.
  Klärungsfälle MÜSSEN eine öffentlich lesbare aktuelle Revision besitzen; veraltete oder
  konkurrierende Entscheidungen dürfen keine teilweise Klärung oder Freigabe bewirken.
- **FR-021**: Integratoren MÜSSEN Aufbewahrungsregeln für gespeicherte Inhalte festlegen
  und abgeschlossene Sitzungen einschließlich ihrer Inhalte löschen können. Wartende,
  laufende oder ungeklärte Aufträge dürfen dabei nicht still entfernt werden. Zugangsdaten
  dürfen nicht Teil gespeicherter Verläufe werden; der Host MUSS den Speicher gegen
  unbefugten Zugriff schützen können. Abnahme: User Story 5.6 und Sicherheitsprüfung.
  Leere oder zu große Eingaben sind vor Annahme abzulehnen. Eine Ausgabe oberhalb des
  konfigurierten Limits ergibt einen ausgewiesenen Fehler statt eines still gekürzten
  Erfolgs. Bereits gespeicherte Inhalte bleiben unverändert; kann das externe Ende
  nicht bestätigt werden, gelten die Regeln für ungewisse Ausführung.
- **FR-022**: Die Lieferung MUSS mindestens zwei unterschiedliche Herstellerverträge und
  konfigurierbare kompatible Zugänge nachweisen. Es darf kein einzelner Hersteller oder
  ein zwingend vorgeschalteter OpenCode-Prozess Voraussetzung der Sitzungsnutzung sein.
  Unterstützte Verträge, Fähigkeiten und Grenzen MÜSSEN dokumentiert werden. Konkrete
  Referenzhersteller werden im technischen Plan als Nachweise ausgewählt, nicht als feste
  Begrenzung des Produktumfangs. Abnahme: User Story 1.1, 2.5–2.6 und SC-008.
- **FR-023**: Gemeinsame Sitzungsverträge und Fachlogik MÜSSEN ohne konkrete Windows- oder
  Linux-Implementierung nutzbar und testbar sein. Betriebssystemspezifische Mechanismen
  MÜSSEN davon getrennt eingebunden werden und denselben fachlichen Vertrag erfüllen;
  fehlende Fähigkeiten dürfen nicht still als unterstützt gelten.
  Abnahme: User Story 4.5–4.6 sowie SC-007.
- **FR-024**: Die Bibliothek MUSS Anfragen und Folgeanfragen an die zugeordnete
  Provider-Sitzung übergeben. Werkzeugausführung und interne Agent-Schleifen MÜSSEN
  beim Provider bleiben; sie dürfen nicht als eigene Bibliotheksfunktion implementiert
  oder nachgebildet werden. Die Kontext-/Inhaltsregeln stehen verbindlich in FR-025.
  Abnahme: User Story 2.8 und 2.10 sowie SC-010.
- **FR-025**: Die Bibliothek darf Kontext weder selbst zusammenfassen noch still kürzen.
  Unterstützt ein Provider eigene Kontextverwaltung, MUSS deren Nutzung ohne zusätzliche
  bibliothekseigene Verdichtung möglich sein. Provider-/Modellgrenzen MÜSSEN als Fehler
  der betroffenen Anfrage gemeldet werden, ohne gespeicherte Verläufe zu verändern.
  Abnahme: User Story 2.9–2.10 und SC-010.
- **FR-026**: Eine Bibliotheksinstanz MUSS genau einen gemeinsamen Vertrauensbereich
  abbilden. Authentifizierung und Zugriffsfreigabe für Aufrufer liegen beim Host;
  eine interne Benutzer-/Mandantenverwaltung ist nicht enthalten. Getrennte Instanzen
  dürfen Sitzungsdaten, Providerregistrierungen und Zustand nicht durch globalen veränderlichen
  Bibliothekszustand vermischen. Abnahme: User Story 4.7 und die Kontexttrennung aus SC-002.
- **FR-027**: Die Bibliothek MUSS langfristig als eingebettete Komponente von SharpClaw
  nutzbar sein. Öffentliche Bibliotheksverträge MÜSSEN dessen hostseitige Einbindung von
  Lebenszyklus, Konfiguration und eigenen Provider-Adaptern ermöglichen, ohne eine
  Abhängigkeit der Bibliothek von SharpClaw, Quellcodekopien oder einen obligatorischen
  separaten Multisession-Prozess einzuführen. Die tatsächliche Umstellung der Anwendung
  ist ein gesondertes späteres Integrationsvorhaben. Abnahme der Einbettungsfähigkeit:
  User Story 4.8 und SC-011.

### Key Entities *(include if feature involves data)*

- **Hersteller**: Anbieter eines Agent-Produkts; bestimmt verfügbare Zugänge und Voraussetzungen.
- **Provider-Zugang**: Konfigurierter Zugang zu einem Hersteller oder kompatiblen Dienst mit
  Endpunkt, unterstütztem Vertrag, Zugangsdatenreferenz und verfügbaren Modellen.
- **Modell**: Über einen Provider verfügbares Modell mit Kennung, Fähigkeiten und Grenzen.
- **Agent-Profil**: Benannte Aufgabe mit Anweisungen, Auswahl eines Providers und Modells
  sowie Ausführungsgrenzen. Mehrere Profile dürfen dasselbe Modell verwenden.
- **Sitzung**: Identifizierbarer, isolierter Gesprächs- und Ausführungskontext mit festem Agent,
  Lebenszyklus und geordneten Anfragen.
- **Anfrage**: Auftrag innerhalb einer Sitzung mit Kennung, Eingabe, Annahmereihenfolge,
  optionalem Arbeitsbereich, Zeitlimit und Bearbeitungszustand.
- **Ergebnis**: Zugeordnetes Resultat einer Anfrage mit Ausgabe oder Fehler, Endzustand
  und gegebenenfalls Hinweis auf ungewisse externe Ausführung.
- **Arbeitsbereich**: Vom Integrator deklarierter Arbeitskontext, dessen schreibende Nutzung
  nicht gleichzeitig durch mehrere Aufträge derselben Instanz erfolgen darf.
- **Wiederherstellbarer Zustand**: Gesicherte Sitzungen, Kontexte, Ergebnisse und wartende
  Aufträge samt Reihenfolge und Sperren; Grundlage für Wiederaufnahme und Zustandsklärung.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In einem kontrollierten Abnahmelauf bearbeiten mindestens zwei Hersteller
  insgesamt zehn unabhängige Sitzungen mit je zwei Anfragen. Bei ausreichender Kapazität
  laufen mindestens zwei Anfragen unterschiedlicher Hersteller nachweislich zeitlich überlappend.
  Die zwei Anfragen je Sitzung werden in Reihenfolge eingereicht; mindestens zwei
  unabhängige erste Anfragen bleiben bis zur festgestellten Überlappung kontrolliert offen.
- **SC-002**: Bei 100 gemischt eingehenden Anfragen über zehn Sitzungen werden 100 % der
  Ergebnisse der richtigen Anfrage und Sitzung zugeordnet; keine fremden Sitzungsinhalte
  gelangen in eine andere Sitzung, und jede Sitzung hält ihre Annahmereihenfolge ein.
  Der kontrollierte Lauf umfasst je zehn Anfragen pro Sitzung mit eindeutigen
  Anfrage-/Sitzungsmarkierungen; Sollreihenfolge ist die bestätigte Annahmesequenz,
  nicht die Startreihenfolge parallel aufrufender Clients.
- **SC-003**: Bei fünf absichtlich fehlerhaften und fünf unabhängigen erfolgreichen
  Anfragen werden alle zehn Endzustände korrekt ausgewiesen; alle fünf unabhängigen
  Anfragen schließen trotz der fremden Fehler erfolgreich ab.
  Die fünf Fehler sind bestätigte beendete Ausführungen; ungewisse belegte Slots werden
  separat betrachtet und dürfen diese Fehlerisolationsprobe nicht künstlich entlasten.
- **SC-004**: Im kontrollierten Test mit kooperativen Providern, verfügbarem lokalen
  Speicher und bis zum Abbruch kontrolliert offen gehaltenen Anfragen ist spätestens
  zwei Sekunden nach Eingang eines Abbruchwunsches beziehungsweise
  nach Ablauf der Anfragefrist ein dauerhaft abfragbarer Cancelled-/TimedOut-Endzustand
  samt korrekter Ausführungsgewissheit sichtbar. Die Zeitmessung erfolgt monoton;
  der Eingang beim Manager beziehungsweise das tatsächliche Fristende ist Messbeginn.
  Bei ShutdownTimeout=zehn Sekunden endet der lokale kontrollierte Stop spätestens
  zehn Sekunden nach Eingang der Stop-Anforderung. Noch unbestätigte externe Arbeit
  bleibt Unknown mit Reservierung; keine offene Anfrage verschwindet ohne Zustand.
  Dies verspricht keine erzwungene Beendigung eines nicht kooperativen Providers und
  keine erfolgreiche Speicherung bei ausgefallenem Datenträger.
- **SC-005**: Bei einem Ausführungslimit von zwei und einem Wartelimit von drei laufen
  zuerst zwei kontrolliert angehaltene Anfragen in getrennten Sitzungen. Danach treffen
  acht weitere Anfragen gleichzeitig ein: Genau drei werden wartend angenommen und
  fünf wegen voller Warteschlange abgelehnt. Somit sind insgesamt fünf von zehn Anfragen
  angenommen, keines der beiden Limits wird überschritten und keine laufende Anfrage
  endet vor Abschluss aller Annahmeentscheidungen. Der explizite Ausgangszustand verhindert,
  dass das Ergebnis vom Timing des ersten Scheduler-Anlaufs abhängt.
- **SC-006**: Ein Integrator ohne Kenntnis des Bibliotheksquellcodes kann anhand der
  Dokumentation innerhalb von 30 Minuten nach Bereitstellung der Voraussetzungen eine
  erste Anfrage und eine Folgeanfrage erfolgreich ausführen.
- **SC-007**: Alle plattformübergreifend zugesicherten Abnahmeszenarien bestehen unter
  Windows und Linux. Ein einstündiger Testbetrieb mit mindestens einer Anfrage pro Minute
  je Zielplattform benötigt keinen Neustart und verliert keine angenommene Anfrage.
  Die Stunde wird ab erfolgreichem Start monoton gemessen; mindestens 60 angenommene
  Anfragen je Zielsystem und deren Endzustände müssen am Ende beziehungsweise nach
  kontrolliertem Stop unter ihrer ursprünglichen Kennung abfragbar sein. Eine noch
  laufende oder ungewisse Anfrage zählt nicht als verloren, wenn Zustand und Reservierung
  gesichert sind. Unbegründet fehlende Datensätze oder falsche Erfolgszustände sind ein Verlust.
- **SC-008**: Zwei unterschiedliche Herstellerverträge werden erfolgreich genutzt. Ein
  zusätzlicher kompatibler Zugang samt Modell kann allein durch Konfiguration für erste
  Anfrage und Folgeanfrage verwendet werden. Eine Erweiterung für einen dritten, abweichenden
  Testvertrag besteht dieselben Sitzungsprüfungen ohne Änderungen an Sitzungsverwaltung
  oder konsumierender Anwendung.
- **SC-009**: Bei zehn erzwungenen Neustarts mit jeweils zwei Sitzungen, zwei bereits
  abgeschlossenen, drei wartenden und einer laufenden Anfrage bleiben 100 % der bestätigten
  Anfragen und abgeschlossenen Ergebnisse auffindbar. Alle drei wartenden Anfragen einer
  nicht gesperrten Sitzung werden in Reihenfolge weiterbearbeitet; die laufende Anfrage
  wird als ungewiss ausgewiesen und in keinem Lauf automatisch erneut gestartet.
  Je Durchlauf liegt ein abgeschlossenes Ergebnis in Sitzung A und eines in Sitzung B;
  alle drei Queued-Anfragen liegen in A, die einzige Running-Anfrage liegt in B.
  A und B haben getrennte Arbeitsbereiche, das Ausführungslimit beträgt zwei. Nach
  Neustart kann A den verbleibenden freien Slot nutzen; B bleibt bis zur Klärung blockiert.
- **SC-010**: Bei zwei kontrollierten Provider-Sitzungen mit je zwei Folgeanfragen bleibt
  die externe Sitzungszuordnung in 100 % der Übergaben unverändert. Eine providerseitig
  verwaltete Verdichtung löst null zusätzliche Zusammenfassungen durch die Bibliothek aus.
  Eine simulierte Kontextüberschreitung wird der richtigen Anfrage zugeordnet und
  verändert null bereits gespeicherte Verlaufseinträge. Es erfolgen null bibliothekseigene
  Werkzeugausführungen oder Ersatz-Agent-Schleifen.
- **SC-011**: Ein isolierter konsumierender Integrationsnachweis eröffnet über die
  öffentlichen Bibliotheksverträge zwei parallele Sitzungen, sendet je eine erste und
  eine Folgeanfrage, liest deren Status, bricht eine kontrolliert verzögerte Anfrage ab
  und beendet die Verarbeitung. Dabei sind null Änderungen am Bibliotheksquellcode und
  null zusätzliche Multisession-Prozesse erforderlich. Der Nachweis prüft die Voraussetzung
  für die spätere SharpClaw-Einbindung, nicht deren bereits erfolgte Produktmigration.

Die kontrollierten Abnahmeläufe prüfen Orchestrierung und Lebenszyklus mit deterministischen
Test-Agents; Antwortqualität und schwankende Herstellerlaufzeiten sind keine Leistungszusicherung.
Für mindestens zwei im technischen Plan ausgewählte Referenzhersteller ist zusätzlich
ein echter Integrationsnachweis für Sitzungseröffnung, erste Anfrage und Folgeanfrage
auf beiden Zielplattformen erforderlich. Die Erweiterbarkeit wird separat nachgewiesen.

## Assumptions

- Primäre Nutzer sind Integratoren und Betreiber. Endnutzer erhalten später über einen
  getrennten Host Zugang; eine eigene Oberfläche ist nicht Bestandteil dieses Features.
- Alle Aufrufer einer Instanz gehören zu einem gemeinsamen Vertrauensbereich. Der Host
  MUSS unbefugte Aufrufer vor Zugriff auf die Bibliothek abweisen. Für unterschiedliche
  Vertrauensbereiche verantwortet er getrennte Instanzen, Zustandsspeicher und passende
  Zugriffsrechte. Instanztrennung innerhalb eines Prozesses ist keine Sicherheits-Sandbox
  gegen bösartigen Host- oder Erweiterungscode; solche Isolation wird nicht zugesichert.
- Lieferumfang sind Bibliotheken, Paketartefakte und Integrationsdokumentation. Ein kleiner
  konsumierender Abnahmenachweis ist vorgesehen, aber kein produktiver Dienst, Installer,
  Netzwerkzugang, Authentifizierungsdienst oder Transportprotokoll.
- SharpClaw soll die Bibliothek langfristig selbst als Teil seiner Anwendung konsumieren.
  Diese Anforderung betrifft die eigenständige Einbettungsfähigkeit und Erweiterbarkeit,
  nicht die Übernahme seiner Architektur oder Funktionen. Die produktive Integration,
  eine mögliche Ablösung bestehender Verwaltung und die Übernahme vorhandener Zustände
  benötigen einen eigenen späteren Umfang; Bestandskompatibilität wird nicht pauschal zugesichert.
- „Permanent auf Anfragen lauschen“ bedeutet hier: Die Bibliothek bleibt innerhalb des
  laufenden Hosts für neue Aufträge nutzbar. Der spätere Host verantwortet deren Eingang.
- Agents arbeiten zunächst unabhängig. Automatische Delegation, Agent-zu-Agent-Kommunikation,
  dynamisches Erzeugen weiterer Agents und gemeinsame Entscheidungsfindung sind nicht enthalten.
- Nachrichteninhalt und Kontext bleiben je Sitzung getrennt. Ein Agent-Wechsel innerhalb
  derselben Sitzung und die Migration nativer Hersteller-Sitzungen sind nicht enthalten.
- Der Integrator stellt gültige Zugänge, erforderliche Laufzeitvoraussetzungen und
  Netzwerkverbindungen bereit. Herstellerunabhängigkeit bedeutet einen einheitlichen
  fachlichen Zugang mit konfigurierbaren und erweiterbaren Anbindungen, nicht die automatische
  Unterstützung aller unbekannten Herstellerprotokolle oder Anmeldeverfahren.
- Das erste Lieferziel ist die herstellerübergreifende Verwaltung von Provider-Sitzungen,
  nicht eine eigene Agent-Laufzeit. Ein Provider ist in diesem Projekt die angebundene
  Implementierung, die Anfragen einer Sitzung verarbeitet und deren Funktionen verantwortet;
  er ist nicht zwingend mit einem reinen Modell-Endpunkt gleichzusetzen.
  Die zusätzliche Fernsteuerung beliebiger installierter Agent-Programme, die Übernahme
  sämtlicher OpenCode-Provider und eine vollständige OpenCode-kompatible Agent-Laufzeit
  sind nicht automatisch Bestandteil dieses Features. Die konkrete Anbindungstechnik bleibt
  dem technischen Plan vorbehalten; OpenCode wird nicht als zwingende Laufzeit vorausgesetzt.
- Werkzeugbereitstellung und -ausführung bleiben gegebenenfalls beim Host und dessen
  Provider-Anbindung. Die Multisession-Verwaltung aktiviert oder implementiert keine eigene
  Werkzeuglaufzeit und erzeugt keine allgemeine Shell-, Dateisystem- oder Netzwerkfreigabe.
- Integratoren deklarieren gemeinsam verwendete Arbeitsbereiche konsistent. Eine Sperre über
  mehrere Host-Prozesse sowie eine Sicherheits-Sandbox für untrusted Agent-Code sind nicht enthalten.
- Ein erneuter Aufruf gilt als neue Anfrage. Dauerhafte Deduplizierung, garantierte
  Genau-einmal-Ausführung bei Herstellern und automatische Wiederholung sind nicht zugesichert.
- Neustartfestigkeit umfasst denselben logischen Host mit weiter verfügbarem Zustandsspeicher,
  nicht Hochverfügbarkeit, parallele aktive Hosts oder Wiederherstellung nach Datenträgerverlust.
  Native externe Prozesse werden nicht als automatisch wiederverbindbar vorausgesetzt.
- Aufbewahrungsfristen werden vom Integrator ausdrücklich konfiguriert; während der
  Aufbewahrung bleiben wiederherstellbare Inhalte verfügbar. Eine Löschung externer
  Herstellerdaten oder hostseitiger Backups kann die Bibliothek nicht zusichern und MUSS
  als Verantwortungsgrenze dokumentiert werden.
- Fachliche Sitzungsidentität, externe Provider-Sitzungskennung und gespeicherter Kontext
  sind unterschiedliche Verantwortlichkeiten. Eine externe Kennung allein ist kein
  Nachweis für einen vollständig wiederherstellbaren Sitzungsverlauf.
- Dauerhafte Annahme, Reservierung, externer Start und gesicherter Abschluss sind
  unterschiedliche Zustandsübergänge. Arbeitsbereichs- und Kapazitätsreservierungen dürfen
  bei einem Neustart nicht allein wegen des verschwundenen Host-Prozesses freigegeben werden.
- Diagnoseprotokolle sind nicht der maßgebliche Sitzungs- oder Anfragezustand. Ein beendeter
  Prozess oder eine erfolgreiche Transportverbindung allein belegt keinen Agent-Erfolg.
- Eine gemeinsame Bibliothek mit abhängigen Windows-/Linux-Bibliotheken ist die bevorzugt
  zu prüfende Aufteilung. Gemeinsame Verträge und Fachlogik dürfen keine Rückabhängigkeit
  auf konkrete Plattformbibliotheken besitzen. Es werden weder leere Plattformpakete noch
  duplizierte Sitzungslogik allein zur Erfüllung eines Namensschemas vorausgesetzt.
- Provider-Anbindungen und Persistenz werden nicht pauschal als betriebssystemspezifisch
  behandelt. Transport, UI, Dienstinstallation und lokale Geheimnisverwaltung gehören
  nicht automatisch in die gemeinsame Bibliothek. Ihre konkreten Grenzen legt der Plan fest.
- Der spätere Remote-Client konsumiert einen gezielt freigegebenen Host-Vertrag; er erhält
  dadurch nicht automatisch Provider-Zugangsdaten oder beliebige Kontrolle über Agent-Prozesse.
  Ein Verbindungsprofil ist von einem fachlichen Agent-Profil zu unterscheiden.
- Temporär untersuchte Fremdprojekte erzeugen keine Laufzeit- oder Quellcodeabhängigkeit.
  Deren spezifische Implementierungen, Protokolle und Funktionsumfänge werden nicht
  automatisch übernommen. Abgeschlossene temporäre Ideenfindung ist von dauerhaft
  erforderlichen Architektur-/Zielhostnachweisen nach Constitution II zu unterscheiden.
  Diese Nachweise bleiben unter evidence/architecture mit Quellstand, relativen Datei-/
  Symbolbelegen und Befunden erhalten, ohne fremden Produktcode oder private Laufzeitdaten
  zu übernehmen. SharpClaw bleibt der ausdrücklich benannte Zielkonsument. Nicht erforderliche
  temporäre Rechercheablagen begründen weder eine Abhängigkeit noch eine Aufbewahrungspflicht.
- Die Constitution v2.0.0 gilt, einschließlich generischer Agent-/Providergrenze, mindestens 87 % Zeilenabdeckung und
  verbindlichem offiziellen Codemetr-CLI-Guard. TDD mit beobachtetem Red–Green–Refactor ist für neue oder
  geänderte Verhaltensanforderungen verbindlich; bestehende Refactorings benötigen eine
  zuvor grüne relevante Suite. Diese Entwicklungsvorgabe ist kein zusätzliches
  Sitzungsfeature. C#/.NET und NuGet sind vorgegebene Projektrahmen;
  Paketaufteilung, konkrete SDK-/Betriebssystemversionen und interne Architektur gehören
  in die technische Planung. Das Ratifikationsdatum bleibt offen. Genehmigt sind
  Codemetr.CSharp.Tool 0.1.0 und die Standardgrenzwerte; es gibt keine Debt-Baseline.
- Das erste Lieferformat unterstützt nur seinen eigenen explizit versionierten
  Speicher-/Binding-Vertrag. Unbekannte oder inkompatible Formate sind ohne Veränderung
  zurückzuweisen. Automatische Migrationen, Formatdowngrades und Wiederherstellung aus
  Backups sind nicht Teil dieser ersten Lieferung; eine spätere Formatänderung benötigt
  den in der Constitution geforderten eigenen Migrations- und Rückkehrplan.
- OpenCode V2 wurde anhand der offiziellen Dokumentation vom 2026-10-26 als fachliche
  Referenz geprüft: [Providers](https://opencode.ai/v2/docs/providers/),
  [Models](https://opencode.ai/v2/docs/models/) und
  [Agents](https://opencode.ai/v2/docs/agents/). Relevant sind getrennte Agent-Profile,
  Provider und Modelle sowie konfigurierbare kompatible Zugänge und Provider-Erweiterungen.
  Es wurde kein Repository geklont und kein Quellcode-Deep-Dive durchgeführt; dies war
  zur Klärung des fachlichen Umfangs nicht nötig. OpenCodes technische Umsetzung und
  Wiederholungsregeln werden dadurch nicht als Projektentscheidung übernommen.
