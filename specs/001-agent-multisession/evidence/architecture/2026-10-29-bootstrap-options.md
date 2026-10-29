# Eine wirksame Optionskonfiguration vor Runtime-Startup

Gemessen 2026-10-29T13:27:05Z, vor Signatur/Implementierung, fortgesetzt auf9e4dfa0.
RuntimeLifecycle.cs SHA2560174e9e82078edc68f71f987cad2faaa8a0aec647787d5a169d969e068ce9d39;
SqliteStoreState.cs f92d93d308d87f7934a1648fb4afd6e15b5a8ff453df416fcf1fdb01a4798f26;
RuntimeOptions.cs ead00eb91a74caf0c4898dc96ce6f74be3506a90c9899eaf18301edb45205371;
IStoreBootstrap.cs3ecabc26c90b56e388b072308bd8a85ac90edbba25488b0768f0216906e7a21e.

## Bestand und verbindlicher Folgeauftrag

T118 aus Close-/Retentionanalyse vorT064. FactoryCreateNew/OpenExisting validiert Options
und speichert das immutable Record im SqliteStoreState. RuntimeLifecycle.StartAsync validiert
weitere Options, prüft Hostlease und ruft Store.StartAsync(lease) auf. Store.Start kennt
nur sein Factoryrecord; RuntimeComponents/Dispatcher benutzen das andere Record. Beides ist
jeweils gültig, kann aber etwa Retention/Limits/Fristen unterschiedlich definieren. Start
macht danach Recoverymutationen und startet Workers; Abweichungen werden nicht erkannt.
Keine Optionsmetadaten sind im DBschema eingeführt, ein explizites Reopen mit neuer
Konfiguration ist deshalb legitim und kein Schemawechsel. StateDirectory wird durch Lease
und LocalDirectoryPath geprüft, nicht anhand bloßer Strings als Besitzeridentität.

## Entscheidung vor Code

IStoreBootstrap erhält ValidateRuntimeOptionsAsync(options,token), vor Start/Recovery
verbindlich awaited. Kein weiteres breites Interface, keine überschriebenen Optionen,
keine DBmigration. SQLstore vergleicht alle immutable RuntimeOptionswerte einschließlich
Retention durch Recordgleichheit, normalisiert dabei nur StateDirectory über den bereits
geprüften Leasebezug. Factorylease muss weiterhin gehalten sein und auf die Zielablage
passen; abweichender Wert InvalidConfiguration, bereits gestarteter Besitzer bleibt
StoreAlreadyOwned. Prüfung über vorhandenen serialisierten Queueport, ohne Nutzerzustand
zu schreiben. Direkter Storeconsumer hat bereits die ausdrücklich validierte Factory-
konfiguration; dessen bestehendes Startprotokoll bleibt nutzbar. Neue Factoryoptionen bei
explizitem OpenExisting sind erlaubt, danach muss dieselbe Runtimekonfiguration gelten.

Kontrollierter Store hat keine separate Factorykonfiguration: er validiert Hostoptions
und bietet eine awaitbare Prüfbarriere für den Startupvertrag. Das ist kein Ersatz für
SQLgleichheits-/Datenerhaltungsnachweise. Failed Runtimevorbereitung bleibt host-owned;
Prüfungsfehler dürfen weder Store noch fremde Lease entsorgen.

Tests zuerst: echte SQLruntime mit abweichender Retention, Eingabelimit, Queuelimit,
DBqueuekapazität, Request-/Shutdownfrist und Nebenläufigkeit wird abgewiesen, ursprüngliche
Queued-/Sessiondaten unverändert, null Provideraufrufe. Identische und kanonisch gleiche
Ablagekonfiguration starten; neue übereinstimmende Factory-/Runtimeoptionen beim Reopen
ebenfalls. Getrennte Vertragsbarriere beweist Await vor Recovery/Workers. Zuerst minimal
verhaltenslose Portsignatur, tatsächliche fachliche Redfälle dann Green; Bootstrap-/Crash-/
Ownership-/Cancel-/Close-/Konsumregression, kompletter Guard/Coverage und nativeCI. Kein
offener Entwurfsblocker; vollständige Closure-/Retention-/Freigabematrix bleibt separat.
