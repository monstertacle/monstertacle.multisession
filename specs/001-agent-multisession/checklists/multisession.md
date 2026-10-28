# Requirements Quality Checklist: Agent-Multisession

**Purpose**: Qualität der gesamten Multisession-Anforderungen prüfen, mit Schwerpunkt TDD,
Wiederanlauf und konsistenten Verantwortungsgrenzen; keine Implementierungsprüfung.
**Created**: 2026-10-27
**Feature**: [spec.md](../spec.md)
**Depth**: Standard
**Audience / Timing**: Reviewer vor `/speckit.tasks`
**Selected Focus**: Q1=A, Q2=A

**Note**: Diese benutzerdefinierte Checkliste wurde durch `/speckit.checklist` erzeugt.
**Review Ownership**: Die Bewertung gehört dem Reviewer. Neu erzeugte Punkte bleiben `[ ]`.
Ein Agent darf nur nach ausdrücklichem Bewertungsauftrag bei der Bewertung helfen.
**Marker Semantics**: `[x]` bedeutet genehmigte Anforderungsqualität, nicht abgeschlossene
Implementierung, bestandene Produkttests oder nachgewiesene TDD-Ausführung.

**Reference Documents**: [Plan](../plan.md), [Datenmodell](../data-model.md),
[öffentlicher Vertrag](../contracts/public-api.md),
[Providervertrag](../contracts/provider-contract.md),
[Validierungsleitfaden](../quickstart.md),
[Constitution](../../../.specify/memory/constitution.md).

## Requirement Completeness

- [x] CHK001 Ist der Umfang ausschließlich als Multisession-Verwaltung beschrieben und sind eigene Agent-Schleifen, Werkzeugausführung, Kontextverdichtung und Kontextrekonstruktion ausdrücklich ausgeschlossen? [Completeness, Spec §FR-024–FR-025, Spec §Assumptions]
- [x] CHK002 Sind die Verantwortlichkeiten von Bibliothek, konsumierendem Host und Provider vollständig abgegrenzt, einschließlich Zugriffskontrolle, Konfiguration und Sitzungsfunktionen? [Completeness, Spec §FR-014, Spec §FR-026–FR-027]
- [x] CHK003 Sind Anforderungen für Eröffnung, Folgeanfrage, Status, Schließung und lokale Löschung einer Sitzung einschließlich ihrer jeweiligen Voraussetzungen dokumentiert? [Completeness, Spec §FR-005, Spec §FR-021, Public Contract §2]
- [x] CHK004 Unterscheiden die Anforderungen dauerhaft bestätigte Annahme, Reservierung, mögliche externe Annahme und gesicherten Abschluss einschließlich ihrer jeweiligen Verantwortungsgrenzen? [Completeness, Spec §FR-009, Spec §FR-018–FR-020, Data Model §Zustandsübergänge]

## Requirement Clarity

- [x] CHK005 Sind lokale Sitzungskennung, native Sitzungsreferenz, einzelne externe Operationskennung und wechselnder Fortsetzungsstand begrifflich eindeutig voneinander getrennt? [Clarity, Spec §Key Entities, Data Model §Session, Provider Contract §2]
- [x] CHK006 Ist die Bedeutung von Annahmereihenfolge bei gleichzeitig eingehenden Anfragen eindeutig festgelegt, ohne Aufruferreihenfolge, Zeitstempel und bestätigte Reihenfolge gleichzusetzen? [Clarity, Spec §FR-007, Public Contract §3]
- [x] CHK007 Sind Abbruchwunsch, lokales Anfrageende und bestätigte externe Beendigung ausdrücklich unterschieden, insbesondere bei einem Abbruch während der Annahme? [Clarity, Spec §FR-010, Public Contract §3–4]
- [x] CHK008 Sind Beginn und Geltungsbereich von Warte-, Anfrage- und Abschaltfristen sowie die daraus folgenden Zustände eindeutig beschrieben? [Clarity, Spec §FR-008, Spec §FR-013, Spec §SC-004, Public Contract §5]

## Requirement Consistency

- [x] CHK009 Ist die Forderung nach Ablehnung nicht unterstützter Fähigkeiten vor einer Ausführung mit der späteren Weitergabe providerseitiger Funktionsfehler vereinbar, ohne einen ausgeschlossenen vollständigen Featurekatalog vorauszusetzen? [Consistency, Conflict, Spec §Edge Cases, Spec §FR-004, Spec §FR-024]
- [x] CHK010 Stimmen die Anforderungen zur Schließung einer Sitzung bei ungewisser Arbeit mit den Voraussetzungen für Reservierungsfreigabe und endgültig geschlossene Zustände überein? [Consistency, Spec §FR-005, Spec §FR-020–FR-021, Data Model §RecoveryDecision, Data Model §Zustandsübergänge]
- [x] CHK011 Ist der Unterschied zwischen ausdrücklich beendeten wartenden Anfragen beim kontrollierten Stop und wiederaufzunehmenden wartenden Anfragen nach einem Absturz widerspruchsfrei beschrieben? [Consistency, Spec §FR-013, Spec §FR-018, Plan §Project Structure, Public Contract §1]
- [x] CHK012 Ist die Zusage unabhängiger Weiterarbeit mit weiter belegter Kapazität durch ungewisse Anfragen vereinbar, einschließlich des Falls vollständig reservierter Kapazität? [Consistency, Spec §FR-008, Spec §FR-011, Spec §FR-020, Plan §Project Structure]

## TDD Requirement Quality

- [x] CHK013 Verlangen die Entwicklungsvorgaben für jede neue oder geänderte Verhaltensanforderung zuerst einen beobachteten fachlichen Red-Nachweis, danach minimales Green und anschließend verhaltenserhaltendes Refactoring? [Completeness, Constitution §IV, Plan §TDD-Umsetzungsstrategie]
- [x] CHK014 Definieren die Vorgaben eindeutig, warum Compilerfehler, fehlende Voraussetzungen, nicht entdeckte Tests und fehlende Zugänge keinen fachlichen Red-Nachweis darstellen? [Clarity, Constitution §IV, Quickstart §Test-first-Zyklus vor jeder Verhaltensimplementierung]
- [x] CHK015 Ist die Abgrenzung zwischen Verhaltensänderung, reinem Refactoring und Dokumentations-/Buildmetadatenänderung eindeutig genug, um TDD-Ausnahmen nicht still auszuweiten? [Clarity, Constitution §IV, Plan §TDD-Umsetzungsstrategie]
- [x] CHK016 Ist für Fehlerkorrekturen ausdrücklich ein vorher fehlschlagender Regressionstest gefordert und für reine Refactorings eine zuvor grüne relevante Suite? [Completeness, Constitution §IV, Plan §TDD-Umsetzungsstrategie]
- [x] CHK017 Sind die TDD-Einheiten nachvollziehbar den Anforderungen und Erfolgskriterien zugeordnet und schließen sie Persistenz, Recovery, Datenschutz, Providergrenzen und konsumierende Hosts ausdrücklich ein? [Traceability, Plan §TDD-Umsetzungsstrategie, Spec §FR-001–FR-027, Spec §SC-001–SC-011]
- [x] CHK018 Ist festgelegt, welche Angaben die zeitliche Red–Green-Reihenfolge belegen müssen und warum spätere grüne Abnahmen beziehungsweise Fakes keine früheren Red- oder echten Herstellernachweise ersetzen? [Measurability, Constitution §IV, Plan §Q-06, Quickstart §8]

## Acceptance Criteria Quality

- [x] CHK019 Sind Messbeginn, Voraussetzungen und erwartete Beobachtung für Zwei-Sekunden-Abbruch und Zehn-Sekunden-Abschaltung einschließlich der Einschränkung auf kooperative Anbindungen definiert? [Measurability, Spec §SC-004, Plan §Performance Goals]
- [x] CHK020 Sind Ausgangszustand, Sitzungszuordnung wartender Anfragen und erwartete Zustände nach jedem Neustart im Wiederanlaufkriterium eindeutig beschrieben? [Measurability, Spec §SC-009, Spec §User Story 5]
- [x] CHK021 Sind Last, Dauer, Verlustbegriff und Zielsystembezug der Parallelitäts-, Zuordnungs-, Kapazitäts- und Dauerbetriebskriterien so präzise angegeben, dass verschiedene Reviewer dieselbe Zusicherung verstehen? [Measurability, Spec §SC-001–SC-003, Spec §SC-005, Spec §SC-007]
- [x] CHK022 Ist objektiv abgegrenzt, welche Einbettungsfähigkeit der konsumierende Nachweis belegt und welche tatsächliche SharpClaw-Migration ausdrücklich noch nicht zugesichert wird? [Acceptance Criteria, Spec §FR-027, Spec §SC-011, Spec §Assumptions]

## Scenario Coverage

- [x] CHK023 Decken die Anforderungen sowohl separate native Sitzungseröffnung als auch erst mit der ersten Nachricht entstehende Providerbindungen einschließlich eines ungewissen ersten Auftrags ab? [Coverage, Spec §FR-005, Spec §FR-020, Provider Contract §2–4]
- [x] CHK024 Sind Anforderungen für ungültige Konfiguration, fehlende Berechtigung, Provider-Ausfall, abgelaufene native Sitzung und Kontextgrenze jeweils beschrieben, ohne einen stillen Ersatzweg zuzulassen? [Coverage, Spec §FR-004, Spec §FR-011–FR-012, Spec §FR-025, Spec §Edge Cases]
- [x] CHK025 Sind normale Annahme, Überlastungsablehnung und länger blockierte Sitzungen als unterschiedliche Szenarien beschrieben und sind etwaige Wartezeit-/Fairnesszusagen ausdrücklich festgelegt oder ausgeschlossen? [Coverage, Gap, Spec §FR-007–FR-008, Plan §Project Structure]
- [x] CHK026 Sind kontrolliertes Beenden, unerwarteter Host-Abbruch und ausdrücklich angestoßene Recovery jeweils mit ihren unterschiedlichen Fortsetzungsregeln beschrieben? [Coverage, Spec §User Story 3, Spec §User Story 5, Public Contract §1, Public Contract §4]

## Edge Case Coverage

- [x] CHK027 Ist festgelegt, wie eine bereits dauerhaft angenommene Anfrage bei verlorener Annahmebestätigung auffindbar bleibt und welche Grenzen für erneutes Einreichen beziehungsweise Deduplizierung gelten? [Coverage, Spec §FR-019, Spec §Edge Cases, Spec §Assumptions]
- [x] CHK028 Sind die Fälle möglicher externer Wirkung ohne gesicherte Operationskennung sowie vorhandener Antwort ohne gesicherten Abschluss/Fortsetzungsstand ausdrücklich berücksichtigt? [Coverage, Spec §FR-018–FR-020, Data Model §Speicherung und Transaktionsgrenzen, Provider Contract §5]
- [x] CHK029 Sind Voraussetzungen und Konfliktregeln für eine Recovery-Entscheidung beschrieben, einschließlich veralteter Entscheidungen und der Trennung von Beendigungsbestätigung und Erfolgsnachweis? [Coverage, Public Contract §4, Data Model §RecoveryDecision]
- [x] CHK030 Sind Anforderungen für Aufbewahrung und Löschung bei wartenden, laufenden oder ungeklärten Aufträgen definiert und sind lokale Löschung, externe Providerdaten und Backups klar getrennt? [Coverage, Spec §FR-021, Spec §Assumptions, Data Model §Wiederanlauf und Aufbewahrung]

## Non-Functional Requirements

- [x] CHK031 Ist der gemeinsame Vertrauensbereich je Instanz einschließlich hostseitiger Zugriffspflichten und ausdrücklich nicht zugesicherter Isolation gegen bösartigen Code eindeutig beschrieben? [Security, Spec §FR-026, Spec §Assumptions, Public Contract §5]
- [x] CHK032 Sind Vertraulichkeitsanforderungen an gespeicherte Inhalte, Zugangsdaten, Fehlermeldungen und Diagnosen mit den Anforderungen an beobachtbaren Verlauf und Aufbewahrung vereinbar? [Privacy, Consistency, Spec §FR-015, Spec §FR-018, Spec §FR-021, Constitution §V]
- [x] CHK033 Ist die geforderte Gleichheit der fachlichen Zusicherungen auf Windows und Linux von den Grenzen des zugesicherten Plattformumfangs und nicht ersetzbaren Zielsystemnachweisen abgegrenzt? [Coverage, Spec §FR-014, Spec §FR-023, Spec §SC-007, Plan §Target Platform]
- [x] CHK034 Sind für begrenzte Eingabe-/Ausgabegrößen und Warteschlangen neben den Grenzen auch Ablehnung, unvollständige Ergebnisse und Auswirkungen auf unveränderte Verlaufsdaten ausdrücklich beschrieben? [Completeness, Gap, Spec §FR-008, Spec §FR-025, Public Contract §5, Data Model §RequestResult und HistoryEntry]

## Dependencies & Assumptions

- [x] CHK035 Sind Abhängigkeiten von nativer Sitzungsspeicherung und modell-/providerbedingten Grenzen dokumentiert, ohne aus lokalem Verlauf eine Wiederherstellbarkeit extern abgelaufener Sitzungen abzuleiten? [Dependency, Spec §FR-018, Spec §FR-025, Spec §Assumptions, Provider Contract §3–4]
- [x] CHK036 Ist die langfristige Einbettung in SharpClaw mit der Eigenständigkeit der Bibliothek und der ausdrücklich getrennten Bestands-/Produktmigration konsistent beschrieben? [Assumption, Consistency, Spec §FR-001, Spec §FR-027, Spec §Assumptions]
- [x] CHK037 Sind Governance-Voraussetzungen wie Ratifikationsdatum und genehmigte Codemetr-Toolversion/Standardgrenzwerte ohne Debt-Baseline mit Zuständigkeit beziehungsweise erforderlicher Entscheidung und Sperrwirkung dokumentiert? [Dependency, Gap, Constitution §Sync Impact Report, Constitution §III, Plan §Constitution Check]
- [x] CHK038 Sind fehlende Providerzugänge oder Zielsysteme als ausstehende zusätzliche Abnahmen behandelt, ohne dass sie einen fachlichen Red-Nachweis liefern oder durch simulierte Ergebnisse als erfüllt gelten dürfen? [Dependency, Consistency, Constitution §IV, Plan §TDD-Umsetzungsstrategie, Quickstart §6]

## Ambiguities & Conflicts

- [x] CHK039 Sind Anfragezustand, Ausführungsgewissheit, Sitzungslebenszyklus und Blockierungsgrund begrifflich konsistent verwendet und sind zulässige Kombinationen wie lokales Ende bei ungewisser externer Wirkung widerspruchsfrei beschrieben? [Ambiguity, Spec §FR-009–FR-010, Data Model §Session, Data Model §Request, Data Model §Zustandsübergänge]
- [x] CHK040 Ist eindeutig festgelegt, ob Fehler während einer Änderung des gespeicherten Formats samt Wiederanlauf/Rückkehr zum vorherigen Zustand zum aktuellen Umfang gehören oder ausdrücklich später spezifiziert werden? [Gap, Spec §FR-018–FR-021, Data Model §StoreMetadata, Constitution §Technische Rahmenbedingungen]

## Notes

- Bei Erstellung waren alle 40 Punkte absichtlich unbewertet. Der Nutzer hat die
  Bewertung und Behebung am 2026-10-27 ausdrücklich beauftragt; Ergebnis siehe Review unten.
- Diese Checkliste prüft Vollständigkeit, Klarheit, Konsistenz, Messbarkeit und Abdeckung
  der Dokumente; sie enthält keine Produkt-Testfälle und keinen Auftrag zur Implementierung.
- `[Gap]`, `[Ambiguity]` und `[Conflict]` markieren Review-Fragen, keine neuen genehmigten
  Produktanforderungen. Antworten und Fundstellen ergänzt der Reviewer nach der Bewertung.
- Abschnittsverweise bezeichnen die Überschriften und stabilen FR-/SC-Kennungen der
  verlinkten Dokumente. `Plan §Q-06` bezeichnet das TDD-Freigabegate innerhalb der TDD-Strategie.
- `/speckit.implement` liest den Zustand der Checkliste als Gate und darf Marker nicht ändern.
- `checklists/requirements.md` hat einen getrennten eingebauten Lebenszyklus durch
  `/speckit.specify` und `/speckit.clarify` und wurde hier nicht verändert.
- Kein Anwendungscode, Testlauf oder Red-/Green-Nachweis wird mit dieser Checkliste behauptet.

## Review 2026-10-27

**Auftrag**: Ausdrückliche Nutzerfreigabe „ja bitte bewerten und lösen“.
**Methode**: KI-gestützter Dokumentenreview gegen die genannten Quellen, danach erneute
Bewertung der korrigierten Anforderungen. Keine Produkt-, Provider- oder Laufzeittests.
**Ergebnis**: Bei Erstbewertung 27 Kriterien erfüllt und 13 mit Dokumentationsbefund;
nach Korrektur 40/40 erfüllt, kein Anforderungsqualitätskriterium mehr offen.
Die 40 zuvor unbewerteten Marker wurden erst im ausdrücklich beauftragten Review gesetzt.

### Behobene Befunde

| Punkt | Befund vor Korrektur | Auflösung und Nachweis |
| --- | --- | --- |
| CHK008 | Lokale Annahmefrist, Queue-Wartezeit und Ausführungsfrist waren nicht ausreichend getrennt | Public Contract §3/§5: AdmissionTimeout fünf Sekunden ab Submit-Eingang; keine automatische Queue-Wartefrist; Anfragefrist erst ab Dispatch; Stop-Frist ab Stop-Eingang. Data Model §Wiederanlauf und Aufbewahrung und Plan sind angeglichen. |
| CHK009 | „Nicht unterstützte Fähigkeit vor Ausführung ablehnen“ konnte einen ausgeschlossenen vollständigen Featurekatalog verlangen | Spec §Edge Cases und Provider Contract §1 unterscheiden lokal bekannte Konfigurationsfehler von erst beim Provider erkennbaren Grenzen. |
| CHK010 | Die Wirkung von Recovery-Close auf Lifecycle und ungewisse Reservierungen war nur indirekt beschrieben | FR-020, Data Model §RecoveryDecision und Public Contract §2: Closing/RecoveryRequired bis zur tatsächlichen Klärung; keine Freigabe allein durch Schließabsicht. |
| CHK011 | Der Plan finalisierte wartende Anfragen beim Stop, während die Spezifikation Wiederaufnahme nur allgemein beschrieb | FR-013, Public Contract §1 und Data Model §Wiederanlauf und Aufbewahrung grenzen gespeicherte Cancelled/NotStarted beim Stop von weiterhin Queued nach Absturz ab. |
| CHK012 | Unabhängige Weiterarbeit war ohne ausdrückliche Grenze durch belegte ungewisse Slots formuliert | FR-011, Plan §Project Structure und Public Contract §3 nennen freie Restkapazität als Voraussetzung; keine Umgehung von Reservierungen. |
| CHK019 | „Nach Auslösung lokal sichtbar“ definierte Messbeginn und beobachtbaren Zustand nicht ausreichend | SC-004 nennt monotone Messung, Eingang/Fristende, dauerhaft abfragbaren Endzustand und Gewissheit; verfügbare Speicherung und kooperative kontrollierte Anfragen sind Voraussetzungen. |
| CHK020 | Die Verteilung der drei wartenden und der laufenden Anfrage auf die zwei Recovery-Sitzungen fehlte | SC-009 und Quickstart §5: drei Queued in A, Running in B, je ein abgeschlossenes Ergebnis, getrennte Workspaces, Limit zwei. |
| CHK021 | Last-/Verlustdefinitionen waren teilweise offen; das 2+3-Kapazitätsergebnis hing vom ersten Scheduler-Anlauf ab | SC-001/SC-002/SC-003/SC-007 ergänzen Voraussetzungen, Verteilung und Verlustbegriff. SC-005 startet mit zwei laufenden Anfragen und acht weiteren Eingängen, sodass drei zusätzliche Annahmen und fünf Ablehnungen eindeutig sind. |
| CHK025 | „Begrenztes Warten“ konnte eine nicht definierte Wartezeit-/Fairnessgarantie bedeuten | FR-008, Data Model §Wiederanlauf und Aufbewahrung und Public Contract §3 legen zulassungsfähige FIFO-Köpfe, Tie-Break und ausdrücklich fehlende globale Warte-/Starvation-Zusage fest. |
| CHK034 | „Große Ausgaben begrenzen und unvollständig markieren“ ließ still gekürzte Erfolgsantworten offen | FR-021, Public Contract §5, Data Model §RequestResult und HistoryEntry und Provider Contract §1: inklusive Bytegrenzen; zu große Eingaben ablehnen; Ausgabeüberschreitung als Fehler mit leerem Output/OutputComplete=false, keine Änderung früherer Historie. |
| CHK037 | Offene Governance-TODOs hatten keine ausreichend explizite Zuständigkeit und Entscheidungsgrenze im Plan | Plan §Constitution Check weist Ratifikationsbestätigung und Codemetr-Genehmigung dem Projektverantwortlichen zu. Tatsächliche Entscheidungen bleiben offen; Q-01 wird nicht als bestanden markiert. |
| CHK039 | Ein laufender Dispatch hatte keine eindeutige Ausführungsgewissheit; Interrupted war nur indirekt im öffentlichen Zustandskatalog enthalten | FR-009, Data Model §Request und Public Contract §7 nennen den vollständigen Zustandskatalog und getrennte Gewissheit einschließlich InProgress sowie zulässige Kombinationen. |
| CHK040 | Ablehnung neuer Schemata war beschrieben, der Umfang tatsächlicher Migrationen blieb aber indirekt | Spec §Assumptions, Data Model §StoreMetadata, Plan §Project Structure und Provider Contract §2: erste Lieferung öffnet nur ihr bekanntes Format; keine automatische Migration/Downgrade, unveränderte Ablehnung inkompatibler Formate, spätere Formatänderung separat planen. |

### Bereits ausreichende Kriterien — Nachweisgruppen

| Kriterien | Bewertungsgrundlage |
| --- | --- |
| CHK001–CHK004 | FR-005/FR-009/FR-014/FR-018–FR-027 und Public Contract §1–4 definieren Umfang, Zuständigkeiten, Lebenszyklus und Annahmegrenzen. |
| CHK005–CHK007 | Data Model §Session und Provider Contract §2 trennen Identitäten; Public Contract §3 trennt Annahmereihenfolge, Tokenabbruch und externe Beendigungsgewissheit. |
| CHK013–CHK018 | Constitution §IV, Plan §TDD-Umsetzungsstrategie und Quickstart §Test-first-Zyklus/§8 enthalten Reihenfolge, Red-Kriterien, Refactoring-Ausnahme, Regression-first und Nachweisangaben. |
| CHK022 | FR-027 und SC-011 unterscheiden Einbettungsfähigkeit vom späteren produktiven Umbau. |
| CHK023–CHK024 | Provider Contract §2–5 sowie FR-004/FR-011/FR-012/FR-025 behandeln Lazy-Binding, Erstübergabe und zugeordnete Fehler ohne Ersatzprovider. |
| CHK026–CHK030 | User Stories 3/5, FR-018–FR-021, Public Contract §3/§4 und Data Model §RecoveryDecision/Wiederanlauf regeln Stop, Crash, verlorene Bestätigung, ungewisse Wirkung, Entscheidungsrevision und Retention. CHK026 wurde zusätzlich im Zuge von CHK011 präzisiert. |
| CHK031–CHK033 | FR-014/FR-015/FR-021/FR-023/FR-026, Constitution §V, Plan §Target Platform und Public Contract §5 definieren Vertrauensgrenze, Datenschutz und tatsächliche Plattformabnahme. |
| CHK035–CHK036 | Provider Contract §3/§4 und Spec §Assumptions begrenzen native Retention/Wiederherstellbarkeit; FR-027 grenzt Konsum und Migration ab. |
| CHK038 | Constitution §IV, Plan §TDD-Umsetzungsstrategie und Quickstart §6 schließen fehlende Credentials als Red-Ersatz und simulierte Live-Freigaben ausdrücklich aus. |

### Weiterhin offene Freigaben — keine Anforderungsqualitätsmängel

- Das ursprüngliche Ratifikationsdatum wird nicht erfunden; der Projektverantwortliche
  muss es soweit ermittelbar bestätigen. Die Constitution wurde in diesem Review nicht geändert.
- Aktualisierung 2026-10-28: Codemetr.CSharp.Tool 0.1.0 und Standardgrenzwerte sind
  genehmigt, keine Debt-Baseline. Finale Q-01-/CI-Abnahme bleibt offen; CHK037 allein
  belegt keine Werkzeug- oder Codeprüfung.
- Q-02–Q-06 benötigen weiterhin die vollständigen nativen CI-, Plattform-, Provider-,
  Paket- und TDD-Nachweise. Bereits vorhandene lokale Teilprüfungen stehen in evidence/README.md;
  diese Checkliste erteilt keine Codefreigabe.
- tasks.md liegt vor; nächster lokaler Vertragszyklus ist T112 vor Dispatcher-Green.
  Die Analysebereinigung ändert weder Produktcode noch SharpClaw und führt keine Migration aus.
