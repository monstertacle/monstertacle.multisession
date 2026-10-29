# T116: operationsgebundene Fortsetzung nach ungewisser Ausführung

Created: 2026-10-29T08:14:43Z
Owner: ausführende Gesamtimplementierungssitzung, feature/reserved-dispatch-contract.
Basis: d795ee3, 355 Tests/95,8991 Prozent lokal grün.

Aktueller Deep-Dive vor Erweiterung: ISessionProvider.ReopenAsync erhält nur bestätigtes
Binding, nicht die ungewisse Operation. RecoveryService.ReopenAsync ruft diesen Port vor
ResolveRecovery-CAS auf und prüft native Identität/Adapter/Version. Bei nativen stabilen
Conversations reicht derselbe Bezug. Bei einer Interaktionskette kann dagegen der alte
bestätigte Kopf weiter lesbar sein, während die ungewisse neue Operation inzwischen einen
neuen bestätigten Kopf erzeugt hat. Die alte Prüfung würde dann die Sitzung entsperren,
ohne diesen Kopf zu prüfen. Providervertrag §4 verbietet stilles Branching nach Unknown.
Gemini ist noch nicht implementiert; die Lücke wird vorher im generischen Vertrag geschlossen.

ISessionProvider SHA256 3f0653cdbe51a1e6004b860ee78d09cf4d43b417ba54f2034b83ebb180f21fce;
ProviderCapabilities f4a7d161ad36361a69cb84625c00ee32f0a1538e1916904e882adb02d3af1989;
RecoveryService 354b93d30a7f38cc73590991116177e2b97ca345a5334976680d64468d8f359d;
ControlledRecoveryState 621dd377ae12131478c062097d9a005c6cd4e2459f77efb78ee02ded23f200df;
ControlledRequestFinalization 48336743fcd432050021903bf2a2937031da52a4f49ce311d2fcfbd76db300c0.

Zusätzlich ist SessionEstablished im Double dispatchgebunden gespeichert, wird aber nicht
in RecoveryCase.Binding projiziert. Die Session bleibt richtigerweise bis Abschluss beim
bestätigten Stand. Ein Recoveryfall darf den bereits bestätigten Beobachtungsbezug als
bekannte Identität zeigen, ohne selbst einen neuen bestätigten Fortsetzungskopf zu erfinden.
Bei erster Lazy-Ausführung braucht die Klärung genau diesen Bezug. Keine Dummy-Requests.

Entscheidung: optionaler generischer IExecutionRecoveryProvider mit
ReopenAfterExecutionAsync(context, knownBinding, operationReference?, token). Capability
RequiresOperationAwareRecovery ist explizit; fehlt der Port, bleibt Recovery gesperrt.
Der Adapter darf ausschließlich gespeicherte Bezüge lesen und daraus einen belegten
Fortsetzungsstand liefern; kein Open/Send, keine alte-Kopf-Fallbackfreigabe. Fehlt ein
für ihn notwendiger Operationsbezug, muss er UnsupportedProviderAction/Unknown melden.
Andere Provider behalten ReopenAsync. Kein Herstellerzweig in Common. Close/Closing
bleibt ohne Fortsetzungsöffnung möglich. Alle Ergebnisse unterliegen demselben CAS und
Identitäts-/Versionvergleich, historischer Interrupted-Zustand wird nicht zu Erfolg.

Lazy-Fall: erfolgreich persistiertes SessionEstablished darf im Recoveryfall die zuvor
leere SessionReference konkretisieren; noch kein neuer Chainhead und keine Veröffentlichung
im SessionSnapshot. Native Klärung erhält diesen bekannten Fallbezug. Store-CAS prüft ihn
gegen bestätigt zurückgegebenes Binding. Ohne gespeicherte Identität kein Neueröffnen.

Redplan in kleinen Zyklen: (1) operation-aware Port statt altem Reopen, fehlender Port/
Bezug und Nativefehler halten Sperre, falsche Identität wird abgewiesen und CAS schützt
konkurrierende Entscheidung; (2) erster Lazy-Bezug im Case, keine Session-Headänderung vor
Klärung, danach dieselbe bekannte Identität. Bestehende Recovery- und Adapterregression,
Full-Guard, native Qualität. T116 ergänzt T113 vor Gemini-Recovery/SQLite und ist ein
konkreter Folgeauftrag dieser Sitzung. Kein offen gelassener Implementierungsentscheid.

Nächster Zyklus vor Green, gemessen 2026-10-29T08:17:17Z:
Fünf operation-aware Fälle waren rot, 15 Recovery-Vertragsfälle sind nach minimaler
Portauswahl grün. Nativefehler bleiben bereinigt, fehlende Fähigkeit/Beleg ist Unsupported.
Der initiale Lazy-Fall ist noch nicht angepasst: Case.Binding kopiert weiter die leere
bestätigte Sessionbindung. Jetzt zuerst konkrete Fallprojektion rot prüfen, danach nur
bestätigte SessionEstablished-Beobachtung in den Fallsnapshot übernehmen und dessen
bekannte Identität in Service/CAS vergleichen. Zusätzlich Port-I/O/CAS-Race als Regression.
