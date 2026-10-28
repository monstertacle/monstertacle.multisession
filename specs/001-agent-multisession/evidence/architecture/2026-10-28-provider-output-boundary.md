# Vorabanalyse — generische Ausgabegrenze (2026-10-28)

Bezug: FR-021, provider-contract.md §1, data-model.md RequestResult, vorbereitende
Teilfälle für T034/T042. Keine abgeschlossene Adapter-/US2-/Dispatcheraufgabe.
Vor Entwurf/Implementierung erneut gelesene aktuelle Quellen und SHA256:

| Datei / Symbol | SHA256 |
| --- | --- |
| Contracts/ProviderReply.cs | `84028139AADB03B5D752ECD37075BA26CFA0B4219DF19A99906EF6ACAC66FB3B` |
| Contracts/Utf8MessageLimits.cs | `DAF9F29787BF2A4A30098337983F6A6ADEA6A281682097CE53AFDA3475549311` |
| Contracts/ISessionProvider.cs | `3F0653CDBE51A1E6004B860EE78D09CF4D43B417BA54F2034B83EBB180F21FCE` |
| Contracts/ISessionStore.cs | `68C0497D26EA52EF921EF5BD6248AAF43353ECA207E946503777EAB11FB78E3E` |
| SessionRuntime.cs | `77ED2805846C3E79CEA64793C1EA5769511655F8E6851ED75AB30B74F89E10B9` |
| UnitTests/Utf8MessageLimitsTests.cs | `437D1C68466D0D9E971C1816807173A2B48DF9A59B06E18ACC310AE146E56CDD` |
| UnitTests/Fixtures/ControlledProvider.cs | `655245F4633B38674017ADD05BDC2044EF767FCDC76D7B063663546B8035B604` |

Pfade relativ zu Common bzw. tests, wie bezeichnet. Aktueller gesamter letzter
Qualitätsstand: `60e2231396269599394119dbfe3105328fc5f44f726a339d55e146a3abaed196`.
Kein Multisession-Git vorhanden. SharpClaw bleibt Referenzstand 7854a12; kein
weiterer Zielhosteingriff nötig, keine Provider- oder OpenCode-Versionsänderung.

## Tatsächlicher Bestand und Kontrollfluss

- ISessionProvider.Send/Inspect/Cancel liefern immutable ProviderReply mit unabhängiger
  Certainty, OutputComplete und optionalem Binding/Error. HistoryEntry und RequestSnapshot
  referenzieren diesen Typ ebenfalls. Kein produktiver Dispatcher oder Completion-Store
  existiert; SessionRuntime ist weiterhin ein Gerüst. Keine funktionierende Pipeline behaupten.
- ISessionStore.CommitCompletionAsync ist die künftige atomare Grenze für Ergebnis,
  Historie, Binding und zulässige Freigabe. Im kontrollierten Teststore ist sie ein Stub.
  Diese Erweiterung führt keine Persistenz und keine Reservierungsfreigabe ein.
- ControlledProvider liefert die vorgegebene Completion unverändert; er ist keine
  produktive Ausgabepolicy. ProviderReplyPolicy fehlt bisher vollständig.
- Utf8MessageLimits.ValidateOutput zählt UTF-8 inklusiv, lehnt leeren Text ab und gibt
  stabile Fehler mit übergebener Certainty zurück. Bestehende Tests prüfen Bytes,
  aber keine Überführung einer fehlerhaften Adapterantwort in Failed/leer/false.
  Die gemeinsame Byteprüfung wiederverwenden, nicht duplizieren.

## Entscheidung nach Bestandsprüfung

ProviderReplyPolicy.NormalizeOutput(reply, maximumBytes) ist eine reine, providerneutrale
Funktion im geplanten Requests-Bereich. Neue Vertragsfälle im ContractTests-Projekt
prüfen Daten am öffentlich konsumierbaren Kernrand, ohne Netzwerk oder fremde Quellen.
Später muss der Dispatcher die Policy vor Commit nutzen; das ist ausdrücklich noch offen.

- Zu großer Text oder explizites OutputLimitExceeded: Failed/leer/false, Certainty
  unverändert. Auch eine versehentlich mitgelieferte kurze Vorschau darf nicht bleiben.
- Succeeded mit leerer/null Ausgabe oder OutputComplete=false: Failed/ExecutionFailed,
  leer/false; keine Ersatzantwort. Ein Teiltext belegt keine Größenüberschreitung.
- Andere Fehler ohne Ausgabe bleiben legitim; ContextLimitExceeded wird nicht als
  erfolgreicher Text oder Anlass zu Replay/Kürzung interpretiert.
- Gültige vollständige Ausgabe unverändert; keine Trim-/Encoding-/Normalisierungsänderung.
- Ungültiges Bytelimit wird als InvalidConfiguration mit unveränderter Gewissheit
  abgebildet. Fehlerdaten werden nur aus stabilen eigenen Kategorien erzeugt.
- Unveränderliche Eingabe/Binding-Referenzen erhalten; weder Bindingfortschreibung noch
  Freigabe ist hier autorisiert. Keine Zustandsmaschine, Certainty-Aufwertung oder
  Prüfung aller Outcome-/Certainty-Kombinationen in diesem begrenzten Ausgabeschnitt.

Alternative: nur Adapter prüfen lassen verletzt T042; eigenes erneutes UTF-8-Zählen
dupliziert Fachwissen. Eine universelle Antwort-/Recoveryengine wäre hier zu groß.
Öffentliche bestehende Signaturen/Persistenzformate ändern sich nicht. Risiken:
gekürzter Erfolg, Verlust von Unknown, Mutation historischer DTOs und Überschreiben
legitimer Kontextfehler. Keine offene implementierungsrelevante Frage im Ausgabeschnitt.

## Test-first-Plan

1. Minimal passthrough-Signatur; ausführbare Assertions für übergroße Ausgabe
   (ConfirmedEnded/Unknown), bereits gemeldetes OutputLimitExceeded mit Resttext,
   leere/null/unvollständige Erfolgsantwort und ungültige Limits tatsächlich Red beobachten.
2. Nur diese Ausgabepfade mit gemeinsamer Byteprüfung bis Green implementieren.
3. Zusätzliche bereits grüne Regressionen separat ausweisen: genau vier UTF-8-Bytes,
   Whitespace unverändert, ContextLimit-/Cancel-/Timeout-Fehler ohne Ersatztext,
   unveränderte Originalantwort und Binding. Kein historischer Inhalt wird umgeschrieben.
4. Gesamte Windowsqualität, Format, vollständiger Codemetr; Coverage-Freigabe bleibt
   bis ≥90 % und echten Windows-/Linux-CI-/weiteren Abnahmen gesperrt.
