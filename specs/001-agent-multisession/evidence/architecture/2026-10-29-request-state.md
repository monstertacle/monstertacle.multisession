# Verbindliche Anfragezustände

Gemessen 2026-10-29T12:28:40Z, Ausgang cc1546a; vollständiger letzter Lauf 577 Tests grün.
DispatchReplyPolicy.cs SHA256 1d262d05da71ea29355cd3babe7e1cbcfe5c5567291118fe5807baca32546624;
CompletionTransactions.cs d819dd453dff8963a32babdd80a3561ec274271bc46e09a02d67af5486dc1a5c;
UnknownTransactions.cs 0b3df1cc54ee8578caaeb3ff899b33dafcef41ace376a8b396bf65b9383f44f2;
RecoveryDecisionTransactions.cs a0034f5595e6a0b9df1101be16ac2649f89d7f1234a5d84a0aaf4de007477f9e.

## Bestand und Kontrollfluss

T070/T075, data-model.md Request/Zustandsübergänge. Dispatcher normalisiert Reply vor
Commit; SQL-Completion normalisiert erneut an der autoritativen Grenze. Der Validator
prüft Terminalität und Gewissheit getrennt, akzeptiert deshalb Interrupted/NotStarted.
ResultTransactions.Write schreibt Result/History/Snapshot gemeinsam; Aufrufer sind
Completion, Unknown und QueuedCancellation. Reservation-Lesen und einzelne Workertransaktion
begrenzen konkurrierende Finalisierung bereits. RecoveryDecisionTransactions.ConfirmRequest
ändert Gewissheit im bestehenden Ergebnis ohne neue Historie; eine zentrale Übergangsregel
fehlt. RequestStateMachine und RequestStateTransactions existieren noch nicht.

## Entscheidung vor Umsetzung

Eine reine Common-Regel definiert gültige Kombinationen, Terminalität und erlaubte
Übergänge. Queued darf Running/InProgress oder Cancelled/NotStarted werden; Running
endet einmal, auch als NotStarted bei sicher abgewiesener Übergabe. Endzustand/Unknown
darf nur denselben Outcome/ConfirmedEnded erhalten. Interrupted/ConfirmedEnded bleibt
für explizite Recovery gültig; Interrupted/NotStarted ist ungültig. Identische Zustände
sind kein neuer Übergang. Kein Zeitstempel oder Cancel-Intent beweist ein externes Ende.
DispatchReplyPolicy benutzt dieselbe Kombinationsregel. Ein SQL-Guard vor Resultschreiben
und Recoverybestätigung verwendet dieselbe Übergangsregel; kein neues Storeport/Schema,
keine zweifache Fachregel, keine Freigabe aufgrund lokaler Terminalität.

Tests zuerst: vollständige Enum-Kombinations-/Übergangsmatrix mit ausführbarer verhaltensloser
Signatur, real ungültiger Providerreply über Validator und SQL-Grenze. Danach Regression
Known/Unknown-Race: genau ein Result/History, unbekannter Gewinner hält Reservation,
Recovery ändert historischen Outcome nicht. Bereits grüne Races ehrlich als Regression.
Getrennte Folgezyklen T071/T077/T078/T079 implementieren Cancel-Intent/Timeout-Outcome,
Endbestätigung und Stop/Close; diese Analyse schließt jene APIs nicht vorzeitig ab.
Keine offene implementierungsrelevante Frage für diesen begrenzten Schnitt.
