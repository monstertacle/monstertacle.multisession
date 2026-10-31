# T117/T043: Ausführungsende und nutzbare Fortsetzung trennen

Created: 2026-10-29T09:15:42Z
Owner: ausführende Gesamtimplementierungssitzung, feature/reserved-dispatch-contract.
Basis 6344816, öffentliche Adapterintegration lokal grün, 427 Tests/95,4658 Prozent.

Deep-Dive: ProviderReply enthält Outcome/Certainty/Output/Binding/Error, aber kein Signal,
dass nach sicher beendetem Aufruf kein nutzbarer Fortsetzungsstand existiert. Gemini kann
nach erster failed/incomplete/cancelled-Interaktion einen bekannten Root ohne completed-
Head besitzen. Die native Sendverweigerung verhindert zwar neue Wurzeln, aber die Sitzung
bleibt bis dahin Open/None. Common darf ContinuationState nicht selbst interpretieren:
eine stabile Conversation hat denselben null-ContinuationState und ist trotzdem nutzbar.
Auch ein abgeschlossener clientseitiger Functioncall im anderen Adapter ist nicht durch
eigene Tools fortsetzbar. Native Expiry hat schon eine stabile Fehlerkategorie, die der
kontrollierte Abschluss bisher nicht in eine Sitzungssperre projiziert.

SHA256 ProviderReply 84028139aadb03b5d752ecd37075ba26cfa0b4219df19a99906ef6acac66fb3b;
GeminiResponse e7358098ec9d3bb77ed3a1a678f422d32341e0da95b7ee27a223a25757cfac63;
InMemorySessionStore 47f6785301f6bf36de2ce7d98a4a3953c34fb21949f1eab0cd39c8e5685abf07.
CommitCompletionAsync schreibt Request/History/Binding/Freigabe bereits unter demselben
Gate und ist der richtige Erweiterungspunkt. RequestSubmissionRules und ReservationPolicy
weisen gesperrte Sitzungen bereits ab; keine zweite Queue oder Herstellerbedingung nötig.

Entscheidung: optionales nullable ProviderReply.ContinuationAvailable. false ist expliziter
bekannter Fortsetzungsblocker, null lässt bestehende Policy unverändert; true ist kein
Ausführungsendbeleg. Keine neue positionale Konstruktorabhängigkeit und keine Interpretation
opaker Bindingstrings in Common. SessionBindingPolicy bestimmt ProviderUnavailable bei
false oder ProviderSessionExpired; vorhandenes RecoveryRequired hat Vorrang. Abschluss
publiziert Blockgrund, Result/Binding/History und sichere Freigabe atomar. Unknown läuft
weiter über MarkUncertain, hält Slot und RecoveryRequired, unabhängig vom Fortsetzungssignal.
Fehlende/inkompatible Registrierung beim Dispatch meldet ebenfalls explizit keine nutzbare
Fortsetzung. Native Adapter geben das Signal nur aus ihrem tatsächlich bekannten Zustand.

Red vor Green: generischer bestätigter Fehler/NotStarted mit false sperrt Annahme und
weitere Reservierung, alte/null/true-Produzenten bleiben kompatibel, Unknown/false hält
Reservierung, Expiry und fehlende Registrierung sperren. Danach native Flagfälle: erste
sichere Gemini-Fehlausführung ohne Head, completed trotz lokaler Outputgrenze mit Head,
clientabhängiger completed-Functioncall und native Expiry. Erst fachliche Assertions, dann
minimale Store-/Parserpolicy, volle Regression/Codemetr/Coverage. Produktive atomare
Speicherung bleibt ausdrücklich T060/T065; T117 ist Vertrag plus kontrollierte Integration.
Close/Recovery für diese Blocker bleiben T037/T043/T079, kein impliziter neuer nativer Bezug.
Keine ungeklärten Architekturfragen für diesen Schnitt.

Native Flagphase vor Implementierung, gemessen 2026-10-29T09:22:04Z:
Vier generische Fehler waren rot, drei positive/Unknownfälle schon grün; nach Policyintegration
247/247 Unitfälle grün. Jetzt zuerst native nullable-Flagassertions: drei erste sichere
Gemini-Endfehler ohne Head liefern false, zwei completed-Textgrenzen mit neuem Head true,
clientabhängiger completed-Functioncall false, native Expiry false und stabile Conversation
trotz lokaler Textgrenze true. Reine Inspectantwort ohne bekannten Sitzungsbezug darf
keine positive Fortsetzungsbehauptung erfinden. Bestehende Outcome-/Gewissheitsregeln
bleiben erhalten. Danach ganze Adapter-/Runtime-/Storevertragsregression und Guard.

Verhaltenserhaltendes Teststore-Refactor vor Änderung, gemessen 2026-10-29T09:25:28Z:
247 Unit-/194 Contractfälle grün. Full-Guard meldet Kopplung41 im kontrollierten Writer
bei unverändertem Limit40. CommitObservation/CommitCompletion/MarkUncertain sind bereits
eine zusammengehörige dispatchgebundene Verantwortung mit eigenem DispatchState, getrennt
von Session-/Acceptance-/Reservationwrites. Sie werden mitsamt bestehendem Hookzugriff
in ControlledCompletionStore (Basis von InMemorySessionStore) verschoben. State/Gate bleiben
dieselben, keine verteilte Transaktion und kein neues Produktverhalten. Alle Testklassen
bleiben im vollständigen Guard; neues Basisfixture auch in ContractTests linken. Danach
beide grünen Suiten und Full-Guard erneut. Keine Grenzwert-/Scope-/Baselineänderung.

Öffentliche Integrationsregression vor Ergänzung, gemessen 2026-10-29T09:27:17Z:
Nach Refactor beide Suiten grün und Full-Guard 193 Dateien/195 Typen sauber. Zusätzlicher
öffentlicher Runtimefall verknüpft den echten Gemini-Parser mit kontrollierter atomarer
Abschlussprojektion: ersten HTTPsend halten, Follower annehmen, unabhängige Sitzung im
zweiten Slot fertigstellen, dann sichere erste Fehlausführung freigeben. Erwartet sind
ProviderUnavailable ohne Recoverycase/Slotbelegung, Follower bleibt Queued, neue Annahme
abgewiesen und nur zwei tatsächliche native Posts. Dies ist Integration der bereits roten
Teilfälle, kein weiterer erfundener Red. Fristen/Barrieren statt Sleep.

Zweites Testrefactor vor Änderung, gemessen 2026-10-29T09:35:09Z:
Die neue öffentliche Integration ist grün, Full-Guard meldet deren Kopplung44. Der
Qualitätswrapper stoppte deshalb vor dem kompletten Lauf; kein Quality-PASS behauptet.
HTTPdrahtantwort/JSON-/Headerbarriere sind vom Runtimeassertionsablauf trennbar und werden
als UnavailableContinuationWire gekapselt. Keine Verhaltensänderung oder neue Behauptung,
beide Fixtures/Test bleiben im Full-Guard. Danach Integration und kompletter Guard/Quality.
