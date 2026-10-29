# Lokaler Ausgang und unbekanntes externes Ende

Gemessen 2026-10-29T12:37:48Z, Ausgang 81c0617.
UnknownTransactions.cs SHA256 0b3df1cc54ee8578caaeb3ff899b33dafcef41ace376a8b396bf65b9383f44f2;
RequestDispatcher.cs 3ed1c7703c87ea1c2345b06a1de3403e72c278e051e2d77258e53818d46b868e;
DispatchReplyPolicy.cs e40593d85f113c9769e0c8c2d06b05cf0380e82d33340599dc85942ad2c285b4;
ControlledRequestFinalization.cs e02086634ce1b5901b83171fb687d8033ec7b6a9f9f885b43f654f016f03a703.

## Bestand und Entscheidung vor Code

T070/T071/T075/T077/T080/T082. RequestDispatcher.SendAsync unterscheidet im Fehlercode
bereits Deadline, Caller-Cancel und fehlenden Endnachweis. PreserveAsync übergibt diesen
Code an MarkUncertain. SQL UnknownTransactions und die Unitprojektion schreiben trotzdem
immer Interrupted; DispatchDeadlineTests behaupten derzeit denselben alten Zustand.
RecoveryTransactions ruft bei Wiederanlauf explizit ErrorCode.Interrupted auf.
RecoveryDecisionTransactions ändert nur Gewissheit, Snapshot/Result, keine Historie;
RequestStateMachine erlaubt dieselbe Outcome-Unknown-zu-ConfirmedEnded-Klärung bereits.

Eine reine Common-Failure-Regel ordnet den stabilen lokalen Code zu: Cancelled/TimedOut/
Interrupted gleichnamig, übrige Fehler Failed. Beide Unknown-Finalisierer verwenden diese
Regel, native Providerinterpretation bleibt im Adapter. Unverändert bleiben Unknown,
Reservationsbelegung, Recoverycase, Bindung und fehlendes Replay. Keine Migration oder
zusätzlichen Ports. Eingefrorene Historie bleibt ursprüngliches Ergebnis, Recovery darf
keinen Erfolg nachtragen. Absicht ist weiterhin separat, kein abgeleiteter Endnachweis.

Zuerst ausführbare SQL-Theorie für Cancel/Timeout/Execution/Crash mit Wiederöffnung und
anschließender expliziter ConfirmEnded; bestehende Deadlinefälle auf die verbindlichen
Sollzustände umstellen und echtes Red beobachten. Danach minimale Regel/Projektion.
Bisherige Crash- und Schedulerproben müssen Interrupted behalten. Fullregression/Guard/
Coverage. Der folgende laufende Cancelzyklus T071/T077 hat gesonderte Architektur/TDD
für Intentbeobachtung und native Endbestätigung; hier keine 2-s-Zusage behaupten.
