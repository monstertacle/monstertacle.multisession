# T035/T041/T044: öffentlicher nativer und hosteigener Konsum

Created: 2026-10-29T08:59:54Z
Owner: ausführende Gesamtimplementierungssitzung, feature/reserved-dispatch-contract.
Basis: 057c33c, 425 Tests/95,4658 Prozent lokal grün.

Bestandsanalyse: SessionRuntime komponiert ISessionProvider ausschließlich nach Registrierung,
Catalog friert Modelle/Settings, Scheduler serialisiert dieselbe Sitzung und Workerzahl ist
begrenzt. Dispatcher nutzt gespeicherten ReservedDispatch; native HTTPadapter besitzen
separate Protokolle und geben keine Transcripthistorie weiter. Es fehlen ein öffentlicher
Ablauf mit beiden echten Adapterimplementierungen und einem andersartigen Hostadapter
sowie ein expliziter passiver nativer Kontextfall. Kein dritter Hostadapter ist vorhanden.
ControlledProvider/InMemorySessionStore/TestLease sind nur kontrollierte Infrastruktur.

SHA256 SessionRuntime ec066b447641b78c76cde1abf2df6a53227e5062c3e72ea9823bd9a91092ec7f;
RuntimeLifecycle 0f3afb486ca6bf75bf26738665a4ad317c5784bebe0dc0a14d465100f5487429;
SessionCatalog aa1bd34ad7ab70ac5189559b5dc43a7b9a29482cbf7c4b21c0c040891d3b6962;
OpenAiResponse 56b427d9e22cae43bc2c7ee4a25edbe2180bd06d5a1935dd91dd38aa8121d5c2.

Konkreter fehlender Pfad: OpenAiResponse.Completed behandelt jedes nicht-message/reasoning-
Item als Clientaktion. Ein passives compaction-Item ist laut tatsächlich abgerufener
https://developers.openai.com/api/docs/guides/compaction ein opaquer nativer Kontextbeleg.
Der Adapter soll ihn tolerieren, ohne ihn zu interpretieren, neu anzufordern oder in spätere
Inputs zu kopieren. Zuerst NativeContextTests fachlich rot; danach nur diese passive Type
zulassen. Keine eigene Verdichtungskonfiguration und kein Aufruf des Compactendpoints.

Öffentlicher Integrationstest: zwei native Adapter mit unabhängigen synthetischen HTTP-
Zuständen plus ThirdContractProvider (AgentTask/Workspace, Bindingversion7, kein Endpoint)
über dieselbe SessionRuntime. Je eine Sitzung mit drei geordneten Eingaben, erste Sends
über getrennte TCS-Barrieren halten und drei belegte Slots vor Freigabe prüfen. Eigene
native Wirefixtures dürfen begrenzte künstliche Ressourcen antworten; sie ersetzen keine
realen Herstellerkonten. Ergebnisse/History/Modelle/IDs/NativeRoot und neuer Head passen
zueinander, keine Recoveryfälle, keine eigenen Kontext-/Toolcalls. Bestehende generische
Drittadapterfähigkeit kann bereits grün sein; keinen künstlichen Kernel-Red erfinden.
Nur konkret beobachtete neue Lücken ändern, native und hosteigene Protokolle nicht im
Commonkern unterscheiden. Fixturezustände und Hostkomposition getrennt halten, damit
keine Test-Godclass entsteht. Vollständige Regression/Codemetr/Coverage danach.

Schließen/SettingsRevision und weitere zehn US2-Gesamtabnahmen bleiben T031/T037/T043/T044,
SQLite und vollständiger Lifecycle eigene Stories. Der öffentliche Ablauf dieses Schnitts
belegt noch keinen Cancel-/Close-/NuGet-/Integratorvertrag. Keine offenen Architekturfragen
für diese eng begrenzten Red-/Integrationsfälle.
