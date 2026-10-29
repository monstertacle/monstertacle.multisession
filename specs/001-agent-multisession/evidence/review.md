# T108: Sicherheits- und Architekturreview

Prüfung 2026-10-29T22:50:59Z durch die ausführende Gesamtimplementierungssitzung.
**Ausdrückliche Selbstprüfung**, kein unabhängiger Reviewer behauptet.
Quellstand 7489450 / Sourcehash
`dba419f35bf40fd7282a5b2f3bfe7c685576e2c62ceb570d5fec2d0f4f84498b`.
Urteil: implementierte Kontrollen technisch geprüft, Produktfreigabe weiterhin gesperrt.

| Prüfbereich / konkrete Quellen | Befund und Prüfung |
| --- | --- |
| Datenminimierung: SessionDiagnostics, SessionMeasurements, PublicError, NativeHttpErrors | Diagnostik über IDs/Enums/Dauer, keine Prompt-/Output-/Credential-/rohen Ausnahmeinhalte; Listener-/Loggerfehler isoliert. Payload/Binding wird bewusst im privaten Store persistiert; keine Verschlüsselungs- oder physische Löschzusage. Diagnostics-/Error-/Retentiontests |
| SQL: SqliteStatements, AcceptanceTransactions, SqlitePageQueries, RetentionTransactions | Laufzeitdaten über AddWithValue-Parameter; feste Tabellen-/Spalten-/PRAGMAtexte, keine Benutzereingabe als SQL. Pagecursor bereichsgebunden und begrenzt. Retention nur Closed und ohne aktive/ungewisse Arbeit, vollständiger Graph atomar |
| Credential/HTTP: NativeHttpEndpoint, NativeHttpTransport, NativeModelAccess | HTTPS, explizites Loopback-HTTP nur Testoption; UserInfo/Query/Fragment abgewiesen; Standardhandler ohne Redirect/Cookies; aktueller Resolver pro Read/Send, kein Credential im Binding. Hosteigener Handler bleibt Vertrauensgrenze. Header-/Gesamtfristen, Bodylimit, nur sichere Readretrys; keine POSTwiederholung |
| Instanz/Workspace: RuntimeHost, RuntimeOperations, RequestScheduler, ReservationTransactions | Keine globale Sitzungsregistry, ein Trustscope je Instanz; gemeinsame schreibende Workspaces/Session/Slots atomar reserviert. Unknown behält Ressourcen, lokale Cancellation beweist kein externes Ende. Instance-/Admission-/Recoverytests |
| Linux: LinuxDirectoryPath, LinuxFileIdentity, LinuxOwnershipLock | openat/nofollow, Eigentümer/0700/0600/nlink, gepinnte Objekte, geschlossene FSallowlist; echter flock statt PIDdatei, explizites Unlock und idempotentes Dispose. 31 native Fälle plus externe Bind-/ramfsprobe; Windows11 daraus nicht abgeleitet |
| Windows: WindowsDirectoryPath, WindowsFileIdentity, WindowsPrivateSecurity, WindowsOwnershipLock | Private DACL, kanonische lokale DOSpfade, NTFS/ReFS/fixed drive; Reparse/Hardlink/UNC/ADS/Subst-/Aliasprüfungen, gehaltene Vorfahren und exklusiver Handle. 37 Server2025-native Fälle; Win11 ausdrücklich noch NOT RUN |
| Lifecycle: RuntimeLifecycle, RuntimeShutdown, RecoveryCoordinator | Öffentliche laufende Operationen und Cancellationcallbacks vor Store-/Leasefreigabe drainen; Stop hat endliche öffentliche Frist, unkooperative Arbeit hält Besitz bis sicherer Bereinigung; positives natives Reopen/Modellread vor Wiederaufnahme persistierter Queue. Regressionsbelege tdd-us3/us5 |
| Herstellergrenze: ISessionProvider, optionale Facetten, OpenAI-/Geminiadapter, NativeHttp | Kern entscheidet nach Vertrag/Capabilities, keine Herstellernamen-Sonderfälle. Keine eigene Werkzeugschleife, Replayhistorie, Compaction, Modellsubstitution oder automatischer Restart. Native IDs/Heads vor Fortschritt persistiert; Callback-/Binding-/Recoverytests |

Bestandssuche `rg 'TODO|NotImplementedException|NotSupportedException' src` ohne Treffer;
das ist ergänzende statische Suche, kein Vollständigkeitsbeweis.
Vollständiger Codemetrscope und Tests siehe codemetr-results.md/build-audit.md.
Neue Befunde nicht als genehmigte Ausnahme behandelt: historische Q06-Lücke,
unklare Gemini-Kontextklassifikation, DEP-001, Produktlizenz und fehlende externe
Abnahmen sind mit Owner/Schritten/Abnahme in remaining-acceptance.md zugeordnet.
Es liegt keine formelle Risikoannahme vor. Kein Sicherheitsversprechen aus
allein grünem Auditexitcode abgeleitet.

Nachabgleich 2026-10-29T22:55:03Z: Windows-Recovery-CLI scheiterte in40966955521
mit StoreAlreadyOwned nach einem Kindprozess-Kill. Derselbe Sourcehash2c5fd5
bestand vollständig in40966982840. Die Schleife disponiert die Setup-Lease vor
Childstart und wartet nach Kill auf WaitForExitAsync; Windows-SecurityAttributes
erlauben keine Handlevererbung. Das Lesen dieser Stellen beweist keine Ursache
des intermittierenden Fehlers. Rohlog artifacts/tdd/windows-recovery-40966955521.log.
Kein Produktfix ohne Reproduktion, keine Behauptung, Probenisolation habe diesen
späteren Befund beseitigt. T125/remaining-acceptance.md sperrt endgültige Q03-Abnahme.

