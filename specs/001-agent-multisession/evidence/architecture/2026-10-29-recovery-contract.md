# T113: adressierbare Recoveryfälle mit persistierter Revision

Created: 2026-10-29T06:22:09Z
Owner: ausführende Implementierungssitzung auf `feature/reserved-dispatch-contract`.
Basis `67a6d3f`, Constitution II/IV, Orchestrierungsvertrag §2.

Vor Entwurf/Implementierung untersucht: IRecovery/RecoveryDecision adressieren nur RequestId;
ISessionQueryStore kennt weder einzelne Fälle noch Fallrevisionen. ISessionStore hat nur den
requestgebundenen ResolveReservation-Stub. SessionCatalog persistiert Opening vor Open und
blockiert ungewisse Eröffnung, kann sie aber nicht als Request ansprechen. Der Dispatcher
sichert Interrupted/Unknown und hält Reservierungen. Das einzige Double InMemorySessionStore
kennt Session-/Request-/Reservationszustände und einen gemeinsamen Gate, noch keine Recovery.
Produktive SQLite-/Recoveryimplementierung fehlt. Vorhandene Unit- und Dispatchverträge bleiben
Regression; aktuelle Sourcebasis wurde gelesen, keine frühere Analyse als neue ausgegeben.

SHA256: IRecovery `fd972ecf55a90f7dfbd4f7670be8179a792a064f455ca5c4b323d72fd1e18774`,
RecoveryDecision `5554bfb36db85d930b53eaee91ba571954f8604dcee178430cc3eca19441e99a`,
ISessionStore `68c0497d26ea52ef921ef5bd6248aaf43353eca207e946503777eab11fb78e3e`,
ISessionQueryStore `515df8fc1c8e9869afdb61949c48eff7a31aeb504fcea6b6c26b57195dddbbc5`,
SessionCatalog `7144cb426c075687e05d0ff419469ffdb13f61d5c7fd2642c635fd54675f3915`,
InMemorySessionStore `093f7f62b2b9489b0accdb881cde4aef7c8c27906d0a5daaba7e6b16ca90fae3`.
Contracts/SessionCatalog unter Common; Double unter UnitTests/Fixtures.

Entscheidung: explizite RecoveryTarget-Arten, unveränderlicher Fallsnapshot, Get/List und
CAS-Entscheidung für beide Targets. Kontrollierter Fallzustand besitzt Revision/DecisionIds;
Session/Request/Reservation werden unter dem vorhandenen Storegate gemeinsam geändert.
Keine Dummy-Requests. Inspect liest bekannte Operations-ID ohne Mutation, ohne Open/Send.
RecoveryService ist die generische öffentliche Facette. Reopen bleibt außerhalb der
Transaktion, dann erneuter Store-CAS. Damit ein Store die vorhandene Reopen-Bedingung
wirklich prüfen kann, erhält ResolveRecoveryAsync zusätzlich ein optionales confirmedBinding:
kein Operatorbeweis im öffentlichen RecoveryDecision, sondern das vom Service geprüfte
native Reopen-Ergebnis. Bei Closing wird kein Reopen benötigt. Bestehende native Identität,
Adapter und Format müssen passen; fehlender Bezug lässt den Fall offen. Diese explizite
Store-Ergänzung vermeidet einen unsicheren gesonderten Bindingcommit nach der CAS-Prüfung.
Keine vorhandene Storeimplementierung/Dateiformatmigration betroffen.

Zuerst ausführbare fachliche Red-Fälle für Opening ohne Request, Execution, Read/List,
unverändertes Inspect, Revisionskonflikte und doppelte DecisionId, Close ohne Endbeleg,
ConfirmEnded mit beziehungsweise ohne sichere Fortsetzung und fehlende Targets schreiben.
Danach minimales Green und gesamte Regression. Neue Fälle können nach Resolution erneut
geöffnet werden, CreatedUtc bleibt gleich, Revision steigt weiter. Eine Open-Entscheidung
ändert historische Fehler nicht in Erfolg. Native SQLite-/Crash-/Recoveryproben folgen T062.
Pagingcursor folgt T115. Kein Anforderungswechsel und keine offene Architekturfrage für
jetzt. Risiken: vorzeitige Freigabe, Reopen-/CAS-Rennen, Dummy-Requests, Verlust von Fehlerhistorie.

Nach Green (36 Contract- und 195 Unitfälle) meldet der volle Codemetr-Check Kopplung 42
bei Grenze 40 im gewachsenen Double. Vor Refactoring entschieden: kontrollierte Query-
projektionen und gemeinsame Datenhaltung vom atomaren Schreibdouble trennen. Eine
ControlledQueryStore-Basisklasse verantwortet die acht Store-Leseoperationen, ein gemeinsamer
ControlledStoreState hält Dictionaries/Gate/Dispatch-/Recoveryzustand. Das Schreibdouble
behält Annahme/Reservierung/Completion und Hooks; alle verwenden denselben Datenbestand
und Gate. Keine Partial-/Scope-/Baselineumgehung. Diese Trennung ist zugleich der benötigte
Erweiterungspunkt für T115-Paging. Initializer der bestehenden DispatchState/Clock-API
werden auf den gemeinsamen Zustand geleitet, damit bestehende Barrieren unverändert wirken.
