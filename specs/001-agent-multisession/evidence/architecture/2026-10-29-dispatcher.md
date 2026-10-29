# T025: einmaliger Dispatch einer frisch reservierten Anfrage

Created: 2026-10-29T05:51:43Z
Owner: aktuelle Implementierungssitzung, `feature/reserved-dispatch-contract`.
Basiscommit: `4f01cb1`. FR-002/FR-005–FR-012/FR-016/FR-018/FR-019;
T025 mit passenden lokalen Teilfällen T016–T019, keine komplette Storyabnahme.

## Bestand vor Entwurf und Implementierung

RequestAdmission validiert Submit und persistiert über ISessionStore. RequestScheduler
bezieht ausschließlich frische Reservierungen aus TryReserveHeadAsync; ReservationPolicy
serialisiert Sitzung/Workspace und zählt alle unfreigegebenen Slots. T112 hat ReservedDispatch
und GetReservedDispatchAsync ergänzt. Der kontrollierte Store erfasst unveränderliche
Submission, Kontext und BindingRevision im Reservierungsgate; bestätigte Completion prüft
Revision und veröffentlicht Ergebnis/History/Binding/Freigabe gemeinsam. MarkUncertain
sperrt die Sitzung und behält den Slot. Produktive SQLite-Transaktionen fehlen weiterhin.

SessionCatalog wählt Provider ordinal aus eingefrorenen Registrierungen. ISessionProvider
besitzt SendAsync mit awaitable Beobachtungscallback, ProviderCall trägt neue Eingabe,
Timeout und Outputlimit. ProviderReplyPolicy.NormalizeOutput prüft bereits UTF-8-Output.
Diese Regeln wiederverwenden; keine zweiten Limits, keine Historienrekonstruktion.

RequestDispatcher existiert nicht; SessionRuntime bleibt ein Gerüst ohne Hintergrundworker.
ControlledProvider kann bisher nur Sendstart und Endantwort per Barriere steuern; weder
Binding-/Callbackbeobachtung noch wechselnde Folgeantworten sind vorhanden. Der kontrollierte
Store hat noch keinen Observationcommit. Bestehende Tests prüfen Auswahl/Reservierungen
und Outputpolicy, aber noch keinen wirklichen Send durch den Dispatcher. Alle Erweiterungen
bleiben generisch und referenzieren Common, keine SharpClaw- oder Herstellerassemblies.

## Untersuchte SHA256

| Datei / Symbol | SHA256 |
| --- | --- |
| Contracts/ISessionStore.cs | 68c0497d26ea52ef921ef5bd6248aaf43353eca207e946503777eab11fb78e3e |
| Contracts/ProviderCall.cs | 2e2513c4c49c1f544b8b65c4db3bef5ff26c5bf5d82d7c47ad7e7f9f1d1399c0 |
| Contracts/ISessionProvider.cs | 3f0653cdbe51a1e6004b860ee78d09cf4d43b417ba54f2034b83ebb180f21fce |
| Contracts/ReservedDispatch.cs | fdb3a331eb86828b29ca916c39588267c733bf93ee80939ca161976674860294 |
| Requests/ProviderReplyPolicy.cs | 96ab0748603a0c8e04f2a13f4188f6d71b4347fa921a8029414c30796524600c |
| Fixtures/ControlledProvider.cs | 655245f4633b38674017add05bdc2044ef767fcdc76d7b063663546b8035b604 |
| Fixtures/InMemorySessionStore.cs | 694c70aba966dec32db2848bc8b4b61f0a53417e92a974bde4174d020aecd5f5 |
| Fixtures/ControlledDispatchState.cs | e377ad6cd31469299ea79e896e88faf835047b3a90d4d4fbe87b4abba6217d9c |

Contracts/Requests liegen unter src/Monstertacle.Multisession.Common, Fixtures unter
UnitTests. Aufrufer/Abhängigkeiten: SessionCatalog → Store/Provider, RequestAdmission → Store,
RequestScheduler → Store/ReservationPolicy; neue Dispatchtests werden erste Dispatcheraufrufer.

## Architekturentscheidung vor Umsetzung

RequestDispatcher.TryDispatchAsync erwirbt selbst eine frische Reservierung und liest nur
deren ReservedDispatch. Keine Methode zum Wiederholen beliebiger alter Kennungen, keine
unbegrenzte Liste abgearbeiteter IDs. Registrierung ordinal fixieren, Outputlimit beim
Konstruktorstart validieren/fixieren. Fehlender Kopf oder widerrufene Projektion liefert
false ohne Send; Storefehler ist keine leere Queue. Erfolgreich persistierter lokaler
Abschluss liefert true, auch bei einem bestätigten Fehlerergebnis; dies ist kein
providerseitiges Exactly-once-Versprechen.

Send bekommt unveränderte neue Eingabe und eingefrorene Session/Binding. Callback wird
dispatchgebunden an CommitObservationAsync weitergeleitet; fehlgeschlagene oder noch
laufende Callbackbestätigung darf nicht durch eine spätere Antwort zum Erfolg werden.
Beobachtung veröffentlicht keinen Fortsetzungskopf. Opaque bestehende native Identität,
Adapter und Format dürfen nicht wechseln; lazy Identität wird erst nach Beleg festgelegt.

Externe Sendwartezeit endlich über Submission.Timeout und Caller-Token begrenzen;
TimeProvider ist injizierbar. Bei Exception, Abbruch, unbestätigtem oder ungültigem
Endsignal konservativ Unknown speichern, Sitzung/Slot halten und bereinigten Fehler
melden. Nach lokalem Ende kein späteres Ergebnis committen oder erneut senden. Bestätigte
Completion wird mit eigener Storefrist statt einem inzwischen abgebrochenen Caller-Token
gesichert. Bei Completionfehler kein zweiter Send und kein anschließender blinder
MarkUncertain-Overwrite: Commit könnte schon erfolgt sein. RevisionConflict bleibt
sichtbar ohne Teilcommit. Vollständige Zustands-/Stop-/Recoveryautomaten folgen später.
Lokal erkannte Registrierungs-/Bindingfehler vor Send werden als Failed/NotStarted
abgeschlossen, ohne Ausweichadapter. Das Double braucht dafür einen kontrollierten
NotStarted-Abschluss, nicht vorgetäuschte SQLite-Dauerhaftigkeit.

Callbacks und Antwortvalidierung dürfen bei wachsendem Umfang eigene kleine
Verantwortlichkeiten bilden; keine künstliche God-Class und keine neue Agentlaufzeit.
Änderungen an bestehenden Doubles müssen deren grüne Regression erhalten. Kein neuer
Vertrag erfordert eine persistierte Migration; Storage ist weiterhin nicht implementiert.

## Testplan und Risiken

Vor jeweiligem Green ausführbare Red-Fälle: reservierter Zustand vor Send, exakte
Eingabe/Binding/Modell/Workspace/Timeout, keine Arbeit bei leerer Queue oder widerrufenem
Snapshot, bestätigter Abschluss und Folgeanfrage nur mit neuem Input/aktualisiertem Kopf;
zwei Adapter überlappen, gleiche Sitzung/Workspace serialisiert, Kapazität zählt während
Send und Completion; Callbackbarriere vor externem Weiterlauf, Callbackfehler trotz
anschließendem Erfolgsreply, Identitätswechsel, Zwischenstatus/Unknown/Outputlimit,
Providerexception, kooperativer Abbruch und nichtkooperatives spätes Ende; Storefehler
bei Query und Completion, kein Retry, InvalidOptions/fehlende Registrierung.
Deterministische Barrieren statt Sleeps; Timerprobe mit kontrolliertem TimeProvider.

Risiken sind doppelte Sends, vorzeitige Freigabe, verspätete Erfolge, Fehlertextleaks,
Bindingdrift und Commit-Ungewissheit. Reine Outputregel wiederverwenden, Zustandsaktionen
über Storeverträge ausführen. Kein SQL-/Provider-/Hostsourcezugriff im Dispatcher.
Keine implementierungsrelevante offene Frage für diesen Schnitt. Reale Persistenz,
Runtimeworker/Start/Stop/Paging, gesamte SC-001/SC-002/SC-005-Abnahme und native CI bleiben
T026/T027, T051/T059/T060, T070–T080 und T104/T111 mit deren konkreten Abnahmen.

## Refactorentscheidung nach 187 grünen Unitfällen

Voller Guard: 86 Dateien/87 Typen, ein Verstoß. DispatchConcurrencyTests koppelt durch
wiederholtes Host-/Profil-/Registrierungssetup an 41 Typen (Grenze 40). Die Setupfabriken
Provider/Host werden unverändert in DispatchLoadHost unter Fixtures verschoben. Die Tests
behalten die fachlichen Assertions und Szenarios, die Fixture verantwortet Komposition.
Keine Regel-/Scope-/Pinänderung. Anschließend Regression und Full-Guard erneut ausführen.

## Abschlussprüfung der Speichergrenzen vor weiterem Green

Nach erstem vollständigem Linux-PASS und Full-Guard-PASS werden die bereits geplanten
Storefehlerpfade konkretisiert: Reservierungsfehler darf keinen rohen Infrastrukturtext
zeigen; Verlust der Bestätigung nach tatsächlich erfolgreicher Completion darf keinen
nachfolgenden MarkUncertain-Overwrite auslösen; RevisionConflict muss ohne Erfolgsteile
und ohne Retry sichtbar bleiben. Zusätzliche kontrollierte Double-Hooks vor MarkUncertain
und nach Completion modellieren ausschließlich diese Grenzen. Abbruch im Snapshotread
liegt nach Reservierung, aber vor jedem Send: daher Cancelled/NotStarted committen statt
StorageUnavailable zu behaupten. Keine Änderung der Transaktions-/Provider-API oder eines
Persistenzformats; zuerst diese Fälle ausführen, nur fehlendes Verhalten ergänzen.

Selbstprüfung der Callbacklebensdauer: Zusätzlich bereits implementierte Regeln durch
Regression für unawaitete und verspätete Callbackaufrufe prüfen. Ein bei Sendrückgabe noch
unbestätigter Callback verhindert Completion; ein erst nach Abschluss beginnender Callback
darf gar keinen Storewrite mehr starten. Eine lazy Native-ID ohne zuvor bestätigte
SessionEstablished-Beobachtung bleibt ungesichert. Dafür keine neue Implementierung
vorwegnehmen oder bereits grünes Verhalten als neuen Red-Nachweis ausgeben.
