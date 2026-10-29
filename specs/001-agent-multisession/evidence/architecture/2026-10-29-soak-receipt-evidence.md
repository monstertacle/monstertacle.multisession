# Monotone Receiptstichproben im Soak-Nachweis

Bestandsprüfung 2026-10-29T20:43:15Z. Ausgangshashes:
SoakSchedule.cs e6a6bc3ad43ce85e57d6fe35cbe3eb9068207f1dca54d0ad92553f0e32c25194;
SoakProbeFlow.cs 8526808eba4534d4251f3d31c7790f934ac041221806b79bc75ac0207e530679.
Gelesen wurden Zeitplan, Receiptpfad, JSON-Nachweis, SQL-Annahmetransaktion und
der unabhängige Readonly-Lauf des echten Stundenexperiments.

Der Zeitplan prüft die monotone Receiptzeit korrekt, speichert aber nur die
Gesamtdauer. AcceptedUtc liegt innerhalb der SQL-Transaktion und ist eine
Wanduhrmessung; daraus dürfen keine monotonen Minutenfenster rekonstruiert
werden. Kein Produktzeitstempel oder Schema wird dafür verändert.

Entscheidung vor Erweiterung: Schedule hält den exakt geprüften letzten
Annahmeoffset. SoakProbeFlow zeichnet pro Receipt RequestId und diesen Offset
auf und ergänzt den Rohbeleg, während die bisherige RequestIds-Liste kompatibel
bleibt. Ein BCL-only PowerShell-Prüfer kontrolliert Dauer, eindeutige IDs,
Zuordnung zu sämtlichen Minuten 0..59 und Assemblydigest. Der Runtime-Reopen-
Nachweis der gespeicherten Endzustände bleibt erhalten. Keine Fakezeitoption
im echten CLI. Erst fehlenden Offset im kontrollierten Zeitplantest rot prüfen,
dann minimale Aufzeichnung und Folgelauf mit tatsächlicher Stunde. T120/T100.

2026-10-29T20:49:48Z: Ein fehlender monotoner Einzeloffset wurde fachlich rot
beobachtet, drei Zeitplanregressionen blieben grün; Redarchiv vor Änderung
gesichert. Danach sieben Zeitplan-/BCL-Timerfälle grün. Der neue unabhängige
Prüfer weist den alten gültigen Stundenbeleg wegen fehlender Einzelsamples
zurück; dies wertet den alten Lauf nicht rückwirkend um. Seine positive
Stundenabnahme steht bis zu einem tatsächlich neuen Lauf aus.

Für vorhandene Zielsysteme wird ein eigener endlicher CI-Stundenlauf vorgesehen.
Er startet nur durch ausdrücklichen Workflowdispatch oder Änderung einer
soak-request-Datei, nicht bei jedem Codepush. Vor der Stunde müssen vollständige
native Qualität, Paketkonsum und Publish desselben Kandidaten bestehen. Der
angeforderte Sourcehash wird mit dem tatsächlichen Qualitätslauf verglichen;
Abweichung beendet den Job vor dem Experiment. Native Daten sind ausschließlich
synthetische noncebenannte private Ablagen; keine Providerzugänge. Artefakte
enthalten Quellstand, Pakete, Stundenbeleg, Verifikation und Testdatenbank.
Windows Server 2025/Ubuntu sind verfügbar, Windows 11 bleibt ausdrücklich offen.
