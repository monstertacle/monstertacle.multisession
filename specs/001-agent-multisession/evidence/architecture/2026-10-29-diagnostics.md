# Datensparsame Diagnosen: erster Schnitt T074/T081

Ausgang9a3775c, Quellenmessung2026-10-29T14:48:16Z. SessionRuntime SHA256
ec066b447641b78c76cde1abf2df6a53227e5062c3e72ea9823bd9a91092ec7f,
RuntimeRequests 99669e084ca255966cb143e04f1319681a50837abab435162d627da5078b8b96,
RuntimeLifecycle 92e9b41b3ee86732df32b9e29097ee328b9864da7c3de47f474b27e6d1cf32f5,
RequestDispatcher a489606eec82d8060d4a2a26916763e91e917be444a1f89100b9da72930d7763.

## Bestand und Verantwortlichkeiten

Common referenziert bereits Logging.Abstractions; bislang weder Diagnoseklasse noch Hooks.
RuntimeRequests hat den eindeutig bestätigten Annahmezeitpunkt nach RequestAdmission;
RequestDispatcher.CommitAsync bestätigt erst nach Store.CommitCompletionAsync. PreserveAsync
kann MarkUncertain verlieren: ein bloßer lokaler Fehler ist kein bestätigter Endzustand.
ProviderReply enthält Output/Binding, ExecutionReservation einen WorkspaceKey: keines davon
gehört in Logs. Öffentlicher PublicError hat nur endliche Codes und lokale IDs.
RuntimeLifecycle besitzt bereits8Felder; neue Loggerzustände dort vermeiden. Dispatcher
liegt nahe Kopplungsgrenze40. Konstruktoren bleiben höchstens6Parameter.

## Entscheidung und Testplan vor Erweiterung

SessionDiagnostics als explizit hosteigenes IDisposable mit optionalem ILogger, eigener
Instanzkennung und eigener ActivitySource. Runtime/Dispatcher dürfen es optional benutzen;
Host entsorgt erst nach Runtimeende. Keine Logger-/Listenerexception überschreitet die
Diagnosegrenze; getrennte Senken dürfen trotz jeweils anderer fehlerhafter Senke arbeiten.
Datenpositivliste: Instanz/Sitzung/Request/Dispatch, Übergang, Gewissheit, Sekunden, öffentliche
Fehlerkategorie. Weder beliebige Fehlertexte noch Exceptionobjekte/Providerdaten übergeben.
Nur Enum.IsDefined-Werte zu endlichen Kategorien normalisieren. Keine Inhalte in Namen,
Tags, Formatstrings oder Exceptions. Ereignisactivities heißen accepted/completed, keine
rückdatierten Ausführungsspans. Dauer wird am Aufrufer monoton gemessen und als Zahl gemeldet.

Zuerst ausführbare No-op-Signatur plus Datenschutz-/Strukturtests wirklich rot beobachten;
danach Positivliste implementieren. Hooks separat mit echten Runtime-/Dispatchtests rot.
Bekannter Commit muss trotz Logger-/Activityfehler sein Receipt/Result behalten. Unknown
nur nach erfolgreich bestätigtem MarkUncertain melden, nicht nach verlorenem Storewrite.
BCL-Metriken/aktuelle Queue-/Reservierungsstände und komplette US3-Matrix bleiben nach
besonderem nachfolgenden Testschritt offen; T074/T081 erst nach gesamter Abnahme schließen.
