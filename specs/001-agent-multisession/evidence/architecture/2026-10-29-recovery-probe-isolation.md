# Native Recoveryproben: begrenzte Ressourcenisolation und Diagnose

Gemessen 2026-10-29T21:56:54Z. Basisd2a0c2f mit separater Geminiressourcenkorrektur.
PersistenceRecoveryTests.cs SHA256 d3a035e33cb4df5e4ed5ab2dc84e492c7b7313a7b06ef2b5849cc46dc6423cbd.
CrashBoundaryTests.cs 0a809e242c8f20a088858a3bea14382e394bc2151f44cb5fc3fc207597120d31.
CrashJournalTests.cs d99fff35bbf46e5783766632f2a951c037f117be1c9ce69ca07dbc3685d14e75.
SqliteRuntimeFixture.cs 9617b3cfcc573eb7bddd8a2500c4b6c4f97481284fca96313e57605618a8f7ff.

Windowsjob123752184166 im Stundenworkflow40960641218 scheitert vor Stundenbeginn
in PersistenceRecoveryTests.PreservesExactStateAcrossForcedRestart(iteration1), Zeile55.
CompletedAsync läuft an seinen5s-Wächter, OperationCanceledException ohne Zustandsdiagnose.
404 andere Integrationfälle bestehen. Originaljoblog unter
artifacts/tdd/model-candidate-windows-hour-failure.log. Dies ist kein Produkt-SLAbeleg
und kein beobachteter Modell-/Bindingfehler; genaue Ursache dieses Einzelverlaufs fehlt.

Der geprüfte Test killt echte Kindprozesse, liest/öffnet dieselbe private DB und lässt
anschließend drei A-Requests mit einem freien Slot neben dem erhaltenen Unknown-B zu.
Parallel laufen andere Collections mit ebenfalls vielen synchronen EXTRAcommits und
Kindprozessen. Die existierende DisableParallelization-Collection schützt bisher echte
Volumes und komplette CLIhost-Szenarien, aber noch nicht diese schweren Crashprüfungen.

Entscheidung: PersistenceRecoveryTests, CrashBoundaryTests und CrashJournalTests derselben
vorhandenen exklusiven Collection zuordnen. Keine Assertion, Workerzahl, Requestfrist,
5s-Testfrist oder SC-003/004-Grenze ändern. CompletedAsync hält bei Deadlinefehler den
letzten bereits gelesenen Zustand/Gewissheit/Fehlerkategorie und Taskstatus in einer
bereinigten Timeoutdiagnose fest; kein neuer unbeschränkter Store-/Providerread, kein
Prompt/Output/Binding/Rohfehler. Nur Testinfrastruktur, keine Produktfunktion oder neue
TDDbehauptung. Der echte rote CI-Verlauf ist der Ausgangspunkt, kein synthetischer Red.
Lokal vollständige Regression/Fullguard und native Neubestätigung. Bei erneutem Fehler
Diagnose auswerten statt dauerhaft bloß zu requeuen. T055/T099 behalten diesen Befund
bis erfolgreicher nativer Bestätigung als bestehenden konkreten Folgeauftrag.
