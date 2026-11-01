# Translation notes

Notes recorded by the translators of feature 011 (literal renderings of ambiguous passages, source inconsistencies). Line
numbers refer to the translated files at the time of translation.

## README and docs

Line numbers refer to both `orig/` and `out/`; every translated file has the same line count as its original.

### General conventions

- The originals contain no uppercase normative keywords (MUSS, SOLL, KANN). Lowercase normative German was
  rendered in lowercase with the same force: `muss`/`müssen` → "must", `darf/dürfen nicht`, `dürfen keinen`,
  `darf weder … noch` → "must not" / "must neither … nor", permissive `darf`/`dürfen` → "may". German
  "ist/sind … zu <Verb>" was rendered as "is/are to be …".
- Code blocks are unchanged byte for byte, including German comments and string literals (README.md
  123–131; docs/embedding.md 94, 112, 118; docs/cqrs.md 7, 18). Placeholders inside inline code also stay
  German: README.md 73 `artifacts/quality/<plattform>/<lauf>/`, docs/embedding.md 165
  `<absoluter-privater-testpfad>`.
- README.md 1–3 was already English and is unchanged.
- Anchors: none of the five files contains an in-file anchor link, and no file under `orig/` links to an
  anchor in these five files. Headings were translated; no link needed updating.

### Ambiguous passages (translated literally)

1. README.md 161 — „Frühere Aussagen „kein Git-Repository“ in historischen Nachweisen …“. The quote is from
   earlier evidence records, not clearly a person's statement; kept verbatim with an English rendering in
   parentheses.
2. README.md 186–187 — „Wieder vorhandene Registrierung löst nur eigene Konfigurationssperren.“ Rendered as
   "A registration that is present again releases only its own configuration locks." Whose locks "eigene"
   refers to (the registration's or the runtime's) is not stated.
3. docs/operations.md 14 — „Insbesondere Unknown bleibt belegt.“ „belegt“ can mean "occupied"
   (slot/reservation, matching the context of the file) or "proven/documented". Rendered as "occupied".
4. docs/operations.md 68–69 — „Wartende Arbeit wird gesichert Cancelled/NotStarted“. Rendered "securely
   Cancelled/NotStarted"; may also mean "is persisted as Cancelled/NotStarted".
5. docs/operations.md 75–76 — „vor übernommener Store-Vorbereitung bleiben Abhängigkeiten hostbesessen.“
   Rendered "before store preparation has been taken over, dependencies remain host-owned"; who takes over
   is not stated.
6. docs/embedding.md 5 — „Bestandsmigration“. Rendered "migration of existing systems"; may also mean
   migration of existing data.
7. docs/providers.md 38 — „ein einzelner fachlicher GET-Zugriff“. „fachlich“ rendered as "functional"
   (domain-level, as opposed to the transport retries in the same sentence).

### Observed inconsistencies in the source (not changed)

8. docs/embedding.md 16 — „Die Produktlizenz muss vor Veröffentlichung festgelegt werden.“ ("The product
   license must be determined before publication.") README.md 19–21 states the libraries are under the MIT
   License. Translated as written.
9. docs/embedding.md 23 and 90 — package/namespace names `Providers.OpenAI` / `.Providers.Gemini`, while
   README.md 16 lists `Providers.Conversations` and `Providers.Interactions`. Names kept unchanged.

## Feature 001 core documents

Scope: German → English translation of the SpecKit set under `specs/001-agent-multisession/`.
Line numbers refer to the original German files. The translations keep the same line count
per file, so the line numbers also locate the passages in the English files.

### General decisions

1. Normative force (all files): uppercase `MUSS/MÜSSEN` → `MUST`. Lowercase `darf nicht` /
   `dürfen nicht` / `dürfen keine` with a prohibitive meaning → `MUST NOT`, as instructed.
   Lowercase `müssen` → lowercase `must`, because the original deliberately distinguishes
   it from uppercase `MÜSSEN`. Permissive `darf`/`dürfen` (permission, not prohibition) →
   `may`. `ist/sind … zu <verb>` → `is/are to be …`.
2. Terminology used throughout: Hersteller → vendor; Integrator → integrator; Betreiber →
   operator; Sitzung → session; Anfrage → request; Auftrag → job; Annahme → acceptance /
   admission; Arbeitsbereich/Workspace → workspace; Zustandsspeicher → state store;
   Vertrauensbereich → trust domain; Verdichtung → compaction; Klärung → resolution;
   Ausführungsgewissheit → execution certainty; ungewiss → uncertain; Nachweis → evidence /
   proof; Abnahme → acceptance; Freigabe → release / approval (by context);
   Projektverantwortlicher → project owner; Zugangsdatenreferenz → credential reference.
3. German inside code is kept unchanged, as instructed (code, paths, identifiers):
   - plan.md:54 `SharpClaw -> NuGet-Bibliotheken` ("NuGet libraries").
   - plan.md:306 and tasks.md:23–24 placeholder `<aenderung>` ("change").
   - tasks.md:9 format template `- [ ] Tnnn [P?] [USn?] Beschreibung mit exaktem Dateipfad`
     ("description with exact file path").
   - tasks.md:33 placeholders `<im Task genanntes Testprojekt>` ("test project named in the
     task") and `<Testklasse>` ("test class").
   - tasks.md:286–295 fenced `text` block (phase graph): the German labels are kept unchanged
     because the content sits in a code block. The owner may decide whether this diagram
     should be translated as prose.
4. Quotations of people are kept verbatim with an English rendering in parentheses:
   - spec.md:506 „Permanent auf Anfragen lauschen“ ("permanently listen for requests") —
     quoted wording of the user description.
   - research.md:32 „10.0.403 nutzen“ ("use 10.0.403") — project owner's confirmation.
   - checklists/multisession.md:110 „ja bitte bewerten und lösen“ ("yes please assess and
     resolve") — user's approval.
5. Quotations of document text (not of people) are translated:
   - tasks.md (T016–T018, T030, T031, T042, T049–T051, T053, T070, T071): the quoted field
     rules are quoted from data-model.md. They are rendered with the exact wording of the
     English data-model.md, so that "quoted verbatim from data-model.md" (tasks.md:353)
     remains true. All quotes were checked against the English data-model.md.
   - checklists/multisession.md:122, 126, 129, 130: quoted wording of earlier specification
     versions („Nicht unterstützte Fähigkeit vor Ausführung ablehnen“, „Nach Auslösung lokal
     sichtbar“, „Begrenztes Warten“, „Große Ausgaben begrenzen und unvollständig markieren“)
     is translated, because it quotes documents, not people.
6. Numbers kept unchanged including the German decimal comma: `88,2576 %`
   (quickstart.md:75, tasks.md:70). In English this reads as 88.2576 %; the value was not
   altered.
7. Glued tokens in the original were separated by a space in English; identifiers, numbers
   and hashes are unchanged:
   - tasks.md:395 `Linuxvolltor74226641a1394e5f8b4d7bff0fe7610f` → "Linux full gate
     74226641a1394e5f8b4d7bff0fe7610f".
   - tasks.md:397 `aus40962572345` → "from 40962572345"; `AbhängigkeitenT095/T096/T111` →
     "Dependencies T095/T096/T111".
   - tasks.md:401 `Windows-Recovery-CLI40966955521` → "Windows recovery CLI 40966955521";
     `nachfolgender40966982840` → "subsequent 40966982840".
   - tasks.md:403 `CI40972345390 Versuch1` → "CI 40972345390 attempt 1";
     `405Integrationfälle` → "405 integration cases".
   - quickstart.md:232 `T105s` → "T105's"; orchestration-details.md:305 `T034s` → "T034's".
   - orchestration-details.md:261 `Abschnitt6` → "section 6".
   As a consequence, an ID count over `\bT[0-9]+\b` shows one more T095 (tasks.md),
   T105 (quickstart.md) and T034 (orchestration-details.md) in English than in German.

### Ambiguous passages (translated literally)

8. spec.md:289–290 „Interrupted bezeichnet einen beim Wiederanlauf ungeklärt gestarteten
   Auftrag.“ — rendered as "a job found started but unresolved at restart". The German
   leaves open whether "ungeklärt" qualifies "started" (started, outcome unresolved) or the
   state found at restart.
9. spec.md:300 „zur scheinbaren Fehlerisolation dürfen keine Limits oder Reservierungen
   umgangen werden“ — rendered as "limits or reservations MUST NOT be bypassed for the sake
   of apparent error isolation" (lowercase `dürfen keine` treated as MUST NOT, see note 1).
10. research.md:418 „keine neue Multisession-Abhängigkeit oder Lizenz zum Nachbau einer
    Agent-Laufzeit“ — „Lizenz“ is rendered literally as "license"; in context it most likely
    means "permission" rather than a legal software license.
11. data-model.md:18 „Globale veränderliche Register sind unzulässig.“ — „Register“ rendered
    as "registries" (could also mean "registers"); same choice in tasks.md:104 and 226.
12. quickstart.md:304 „folgen erst T105 nach ausdrücklicher Freigabe“ — rendered as "only
    follow T105 after explicit approval"; the German can mean "only come with T105" or
    "only follow after T105".
13. orchestration-details.md:172, 187, 216, 234 — the original numbers two sections `## 5.`
    and two sections `## 6.`. The numbering is kept unchanged. The cross-reference
    orchestration-details.md:261 „gemäß Abschnitt6“ ("according to section 6") is therefore
    ambiguous; by content it most likely refers to the second section 6 ("Store and Runtime
    Options Before Startup").
14. orchestration-details.md:255 „Ineligible SessionBlocked, fehlende Session NotFound“ —
    rendered literally as "Ineligible SessionBlocked, missing session NotFound" (meaning:
    ineligible → SessionBlocked, missing session → NotFound).
15. tasks.md:384–403 „Owner ausführende Gesamtimplementierungssitzung“ — rendered as
    "Owner: executing overall implementation session"; the colon was added for readability,
    the German has none.
16. plan.md:161–164 „Er bestätigt … Er hat …“ — the masculine pronoun refers to the project
    owner („der Projektverantwortliche“) and is kept as "he".

### Headings and links

17. No link in these files points to a heading anchor; all links point to files and are
    unchanged (link targets verified identical per file). Link texts were translated, for
    example checklists/multisession.md:17–20 [Datenmodell] → [Data model],
    [öffentlicher Vertrag] → [public contract], [Providervertrag] → [provider contract],
    [Validierungsleitfaden] → [validation guide]; quickstart.md:178 [öffentlichen Vertrag]
    → [public contract]; quickstart.md:236 [Providervertrag] → [provider contract];
    research.md and provider-contract.md source link texts likewise.
18. Textual section references (`Doc §Heading`) in checklists/multisession.md refer to
    headings that changed, and were updated to the new English headings
    (checklists/multisession.md:101 states that section references denote headings):
    - Data Model §Zustandsübergänge → §State Transitions (lines 28, 40, 90)
    - Plan §TDD-Umsetzungsstrategie → §TDD Implementation Strategy (lines 46, 48, 49, 50,
      86, 141, 147)
    - Quickstart §Test-first-Zyklus vor jeder Verhaltensimplementierung → §Test-First Cycle
      Before Every Behavior Implementation (line 47); „§Test-first-Zyklus/§8“ →
      "§Test-First Cycle/§8" (line 141)
    - Data Model §Speicherung und Transaktionsgrenzen → §Storage and Transaction Boundaries
      (line 70)
    - Data Model §Wiederanlauf und Aufbewahrung → §Restart and Retention (lines 72, 121,
      124, 129); „§RecoveryDecision/Wiederanlauf“ → "§RecoveryDecision/Restart" (line 144)
    - Data Model §RequestResult und HistoryEntry → §RequestResult and HistoryEntry
      (lines 79, 130)
    - Unchanged: `Constitution §Technische Rahmenbedingungen` (line 91) and
      `Constitution §Sync Impact Report` (line 85), because the constitution is outside
      this translation scope; the German heading name refers to the untranslated
      constitution file.
19. Other heading-name references in prose that changed: plan.md §"Konkretisierte
    Orchestrierungsverträge und Umsetzungsvoraussetzungen" and §"TDD-Umsetzungsstrategie"
    are now "Concretized Orchestration Contracts and Implementation Prerequisites" and
    "TDD Implementation Strategy"; no file refers to the first one. The plan heading
    "Project Structure", "Constitution Check", "Target Platform" and "Performance Goals"
    were already English and are unchanged.

## Corrections applied after translation (feature 011)

- Docs notes 8 and 9 were corrected against the source, not left as written: docs/embedding.md now states the MIT License
  (feature 007; `PackageLicenseExpression` MIT) and names the provider packages `Providers.Conversations` / `Providers.Interactions`;
  docs/embedding.md and docs/providers.md use the current public types `ConversationSessionProvider`, `ConversationOptions`,
  `InteractionSessionProvider`, `InteractionOptions`, and the embedding example sets the required `Endpoint`.
- German comments, strings, placeholders and the phase diagram inside code blocks were translated (README.md, docs/embedding.md,
  docs/cqrs.md, feature 001 plan.md and tasks.md); constitution heading references in the feature 001 checklist use the 2.1.0 names.
