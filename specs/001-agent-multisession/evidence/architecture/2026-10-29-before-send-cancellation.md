# T076 regulärer Stop zwischen Reservierung und Vorabprüfung

Bestandsprüfung2026-10-29T17:11:25Z an9f48f54, RequestDispatcher SHA256
92bb6f9a77ffe861f1882aa459d6ad6efd231447d1de7f6e82e90cb5925ea859.
Volltor27f597763ad14438be6e21ef915314b3 scheiterte in bestehendem
RuntimeStatisticsMonitorTests.SamplesWhileRunningAndWithdrawsAtStop: StopStorageUnavailable.
Dispatcher behandelt OCE bei GetReservedDispatch ausdrücklich als Cancelled/NotStarted,
CancelBeforeSendAsync fängt die unmittelbar folgende GetRequestabfrage dagegen pauschal
alsStorageUnavailable. RuntimeWorker macht daraus eine dauerhafte Workerfault.

Entscheidung vor Korrektur: gleicher begrenzter Vor-Sendvertrag wie beim unmittelbar
benachbarten Read. Nur OCE bei tatsächlich abgebrochenem Caller-/StopToken wird als
CancelledNotStarted persistiert; bekannter Commit ohne abgebrochenes CallerToken.
Originale Session-/Requestkennung, keine Provideraktion und keine Unknownreservierung.
Fremde OCE/echteSpeicherfehler bleiben StorageUnavailable. Keine längere Frist, kein
Testskip, keine Timingverzögerung. Deterministischer Testhook ausschließlich im
ControlledQueryStore direkt vor GetRequest-Tokenprüfung; keine Produkt-Testhooks.
Zuerst typisierter erwarteterCancel gegen bisherigenStorageUnavailable beobachten,
danach gleiche Commitlogik und gesamte Dispatcher-/Stop-/Metrikregression prüfen.
