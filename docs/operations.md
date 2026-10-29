# Betrieb und sichere Wiederaufnahme

Die Bibliothek läuft im Prozess des Hosts. Der Host besitzt Konfiguration,
Providerobjekte, Credentialresolver und gegebenenfalls Diagnostik. Jede Runtime
benötigt einen eigenen privaten lokalen Zustandsordner und eine passende native
Lease. Zwei Runtimeinstanzen dürfen keinen Store teilen. Keine statischen
Providerregister oder globalen Credentials sind erforderlich.

## Zustand und Ausführungsgewissheit

Ein Request hat einen lokalen Zustand (`Queued`, `Running`, `Succeeded`, `Failed`,
`Cancelled`, `TimedOut`, `Interrupted`) und separat die Gewissheit `NotStarted`,
`InProgress`, `ConfirmedEnded` oder `Unknown`. Ein lokales Cancel-, Timeout- oder
Stop-Ergebnis beweist kein Ende im Backend. Insbesondere Unknown bleibt belegt.
Ein später eintreffender Providererfolg überschreibt kein bereits gesichertes
lokales Endergebnis.

`SubmitAsync` liefert ein Receipt erst nach bestätigtem Annahmecommit. Die
Request-ID und Sequence sind anschließend dauerhaft. Bei verlorenem Receipt
mit unklarer Annahme nicht blind erneut senden: `ListRequestsAsync` und die
Sitzungshistorie erlauben die lokale Untersuchung. Ein Fehler mit `NotAccepted`
belegt dagegen, dass dieser Versuch nicht angenommen wurde. Fehler enthalten
keine Provider-Rohantworten; `SessionOperationException.Failure` führt den
öffentlichen Code und die getrennte Annahme-/Ausführungsgewissheit.

## Fristen und Kapazität

| Option | Standard | Beginn/Bedeutung |
| --- | --- | --- |
| AdmissionTimeout | 5 Sekunden | Eintritt in Submit, einschließlich lokaler Annahmearbeit |
| RequestTimeout | 600 Sekunden | Ausführung ab tatsächlichem Dispatch, keine Queue-TTL |
| ShutdownTimeout | 10 Sekunden | Standard für kontrollierten Shutdown |
| DbBusyTimeout | 1 Sekunde | SQLite-Busy-Grenze; derzeit exakt dieser Wert unterstützt |
| MaxConcurrentRequests | 4 | Belegte Slots einschließlich ungewisser Reservierungen |
| MaxQueuedRequests | 100 | Dauerhaft wartende Requests |
| DbWorkQueueCapacity | 256 | Begrenzte lokale Infrastrukturarbeit |
| MaxInputBytes | 1 MiB | Inklusive Grenze für strikt gültiges UTF-8 |
| MaxOutputBytes | 4 MiB | Inklusive Grenze für endgültige Textausgabe |

Ein `SubmitRequest.Timeout` legt die endliche Ausführungsfrist dieser Anfrage
fest. Keine automatische Queue-Verfallszeit, Providerwiederholung oder Ausweichwahl
ist impliziert. Ungültige UTF-16-Eingaben werden vor Annahme abgewiesen. Eine zu
große Ausgabe ist ein Fehler mit leerer Ausgabe, kein abgeschnittener Erfolg.

Pro Sitzung ist höchstens eine nicht freigegebene Ausführung möglich. Schreibende
Agentaufträge benötigen den vom Host ausdrücklich deklarierten WorkspaceKey.
Gleiche Schlüssel serialisieren innerhalb der Runtime. Der Host muss tatsächlich
gleiche Arbeitsbereiche auf denselben kanonischen Schlüssel abbilden; die
Bibliothek entdeckt keine Dateisystemaliasse von Workspace-Namen. Unabhängige
Sitzungsköpfe können freie Slots nutzen, ohne die FIFO-Reihenfolge innerhalb einer
Sitzung zu ändern. Unknown-Slots sind keine zusätzliche freie Parallelität.

## Cancel, Close, Stop und Dispose

`CancelRequestAsync` persistiert zunächst den Abbruchwunsch. Queued-Arbeit kann
als Cancelled/NotStarted enden. Bei Running wird der kooperative Providerpfad
abgebrochen und begrenzt auf Endbestätigung gewartet. Ohne solche Bestätigung
bleiben Sitzung/Workspace und Slot reserviert. Der Kern führt keine erzwungene
Backendbeendigung, Prozesssuche oder Tötung unbekannter Prozesse durch.

`CloseSessionAsync` sperrt neue Annahme, beendet wartende Arbeit und behandelt
laufende Arbeit kontrolliert. `Closing` und `RecoveryRequired` bleiben sichtbar,
wenn das externe Ende ungeklärt ist. Closed wird erst bei erfüllter lokaler
Abschlussbedingung gesetzt. Close löscht weder lokale History noch Providerdaten.

`StopAsync(deadline, token)` sperrt Annahme sofort; seine Frist beginnt beim
Eintritt, nicht erst nach einem internen Lock. Wartende Arbeit wird gesichert
Cancelled/NotStarted; unbestätigte laufende Arbeit bleibt Unknown. Bereits
begonnene Open-/Recoveryoperationen werden als Laufzeitarbeit verfolgt. Läuft
lokale Arbeit über die Wartefrist hinaus, bleiben Store und Lease bis zum Ende
der Bereinigung geschützt. Eine abgelaufene Wartefrist ist kein Entsorgungsbeleg.
`Completion` und das beobachtete Stop-/Dispose-Ergebnis gehören zur Hostaufsicht.
Dispose während eines laufenden Starts wartet auf dessen tatsächlichen Abschluss
und verhindert eine anschließende Worker-Freigabe. Gleichzeitige Disposeaufrufe
teilen die laufende Bereinigung; vor übernommener Store-Vorbereitung bleiben
Abhängigkeiten hostbesessen.
Provider und Diagnostik erst nach ihrer tatsächlichen letzten Runtimebenutzung
entsorgen.

## Neustart und Recovery

Den erwarteten Store ausdrücklich mit `OpenExistingAsync` öffnen. Ein fehlender
Store wird nicht ersetzt. Derselbe private Ordner, derselbe `InstanceTrustScopeId`
und kompatible Providerregistrierungen sind nötig. Der Host verwahrt die
TrustScope-ID separat als nicht geheime, verlässliche Zuordnung; keine neue UUID
für einen vorhandenen Store erzeugen. Native Bindings und Profilrevisionen sind
persistierte Identitäten, keine frei austauschbaren Konfigurationswerte.

Nach einem ungeplanten Neustart wird Queued fortgesetzt. Angefangene, nicht sicher
beendete Arbeit wird Interrupted/Unknown und erhält einen Recoveryfall. Sie wird
nicht automatisch erneut ausgeführt. Fehlende lokale Adapterkonfiguration blockiert
betroffene Sitzungen; sie autorisiert weder Providerwechsel noch neue native IDs.
Die aktuelle Implementierung prüft dabei lokale Registrierung/Capabilities. Eine
vollständige erneute Prüfung nativer Credentials/Modellverfügbarkeit ist noch
Teil der offenen Abnahme T080; lokaler Konfigurationsabgleich beweist sie nicht.

1. `ListRecoveryCasesAsync` oder `GetRecoveryCaseAsync` lesen und Revision sichern.
2. Mit `InspectRecoveryAsync` native Evidenz untersuchen. Inspect ändert allein
   weder Ergebnis noch Reservierung. Nicht unterstützte Providerpfade bleiben Fehler.
3. Verantwortliche Entscheidung mit eindeutiger DecisionId, Target, ExpectedRevision,
   `ConfirmEnded` oder `CloseSession`, nicht geheimer Begründung und Evidenzreferenz
   über `ResolveRecoveryAsync` abgeben. Veraltete Revisionen werden abgewiesen.

ConfirmEnded erfindet keinen Erfolg. CloseSession ist keine Bestätigung eines
externen Endes. Ohne ausreichende Evidenz bleiben die Reservierungen bestehen.
Die Bibliothek stellt keine automatische Formatmigration bereit: inkompatibles
Schema/Binding wird vor ungeprüften Änderungen abgewiesen. Für spätere Formate
ist ein eigener Migrations- und Rückkehrplan erforderlich.

## Datenhaltung und Rechte

Retention muss ausdrücklich konfiguriert sein: `new RetentionPolicy(true, null)`
behält Daten bis zur ausdrücklichen lokalen Löschung. Alternativ legt die Policy
ein endliches Aufbewahrungsalter ab ClosedUtc fest. `DeleteSessionAsync` verlangt Closed,
erfülltes Alter und konfliktfreie Daten; offene Fälle oder ungewisse Reservierungen
werden nicht mitgelöscht. Es gibt keinen automatischen Löschdienst. Lokale
Löschung bedeutet weder physische sichere Datenträgerlöschung noch Provider- oder
Backup-Löschung. Der Host entscheidet gesondert über Providerretention und Backups.

SQLite nutzt lokales DELETE-Journaling, EXTRA-Synchronisierung und Fremdschlüssel.
Transaktionen sichern Annahme, Dispatch/Fence, Beobachtung und Ergebnis/Binding.
Keine Netzlaufwerke oder geteilten Stores. Linux verlangt eigene 0700-Verzeichnisse,
0600-Dateien, aktuellen Eigentümer und symlinkfreie kanonische Pfade. Windows
verlangt lokale unterstützte Datenträger, private User-DACL und keine Reparse-/
Pfadaliasse. Vorhandene falsche Rechte werden abgewiesen und nicht repariert.
Lease-Dateien werden beim Dispose nicht gelöscht. Produktzustand niemals mit den
absichtlich markierten Crash-/Diskfull-Testworkern bearbeiten.

## Diagnose und Datenschutz

Optionale hosteigene `SessionDiagnostics` verwenden Logger, ActivitySource und
BCL-Meter `Monstertacle.Multisession`. Aktuell werden bestätigte Annahme und
Abschluss mit IDs, Zustand, Gewissheit, Dauer und Fehlerkategorie gemeldet.
Der Ereignisumfang umfasst bestätigte Annahmen und beobachtete Dispatchabschlüsse.
Der Ergebniszähler beschreibt diese Beobachtungen; die gespeicherte Historie bleibt
die Quelle für sämtliche lokalen Ergebnisse einschließlich direkt abgebrochener Queueeinträge.
Gauge-Snapshots zeigen Queue, belegte und ungewisse Reservierungen; Unknown ist
eine Teilmenge der belegten Slots. Beobachter führen keine Datenbank-I/O aus.
Bei nicht verfügbarer Statistik wird deren Gültigkeit zurückgezogen, nicht Null
als angeblicher Leerstand erfunden. Request-/Session-IDs sind keine Metriklabels.

Keine Prompts, Ausgaben, Header, Credentialwerte, Bindings oder rohen
Ausnahmetexte in Bibliotheksdiagnosen aufnehmen. Der Host trägt dieselbe Pflicht
für seine eigenen Logs, Exporte und HTTP-Handler. Fehlerhafte Diagnosecallbacks
werden isoliert und über FailureCount sichtbar; sie ändern keine Geschäftsresultate.

Aktuelle Belege und ausdrücklich offene Grenzen stehen unter
[implementation-progress](../specs/001-agent-multisession/evidence/implementation-progress.md).

## Verfügbarkeit beim Wiederanlauf

Nach dem Storebootstrap prüft die Runtime erhaltene Open-Sitzungen vor dem Workerstart.
Registrierung, Vertrag, Bindingversion und deklarierte Fähigkeiten müssen weiter passen.
Ein Adapter mit `ISessionAvailabilityProvider` prüft zusätzlich den eingefrorenen
Modellzugang lesend; beide nativen HTTPadapter verwenden dafür den Model-Metadata-GET
mit frisch aufgelöstem Credential. Danach prüft `ReopenAsync` denselben gespeicherten
nativen Bezug. Die Gesamtprüfung ist durch `RuntimeOptions.RequestTimeout` begrenzt;
Caller-Cancel bricht den Start ab. Der HTTPadapter besitzt zusätzlich eigene endliche
Header-/Readfristen. Es gibt keinen Generierungsauftrag zur Verfügbarkeitsprüfung.

Eine Ablehnung blockiert ausschließlich diese Sitzung als `ProviderUnavailable`.
`SessionSnapshot.AvailabilityError` nennt die bereinigte Ursache, beispielsweise
`AuthenticationFailed`, `InvalidConfiguration`, `ProviderSessionExpired` oder `TimedOut`.
Bereits angenommene Anfragen behalten ihre Kennungen und bleiben Queued; es gibt weder
Ersatzmodell noch erneutes Open. Nach korrigiertem Zugang kann ein neuer Hoststart
seine eigene Verfügbarkeitssperre nach positiver Prüfung aufheben. Bei regulärem Stop
werden Queueeinträge weiterhin abgebrochen; eine alte gestoppte Anfrage wird nicht
wiederbelebt. Fremde native Fortsetzungsblockaden sowie RecoveryRequired/StorageFailure
werden durch diese Verfügbarkeitsprüfung nicht freigegeben.

Metadaten können Herstelleraliase beschreiben; die Bibliothek speichert daraus keine
neue Modellauswahl. Ein erfolgreicher Read ist ein Zugangs-/Existenzbeleg zu diesem
Zeitpunkt, keine Zusage künftiger Quote oder Generierung. Der direkte Lazy-Reopen von
Gemini bleibt ohne HTTP; die zusätzliche Verfügbarkeitsfacette ist ausdrücklich getrennt.

`SubmitRequest.Timeout` ist die maßgebliche Frist jedes ausdrücklich eingereichten
Auftrags. Ein Host kann seinen Default aus `RuntimeOptions.RequestTimeout` übernehmen;
dieser Optionswert begrenzt außerdem die Startup-Reopen-/Availability- und Recoveryreads.
Ein bereits angenommener Auftrag verbraucht seine Ausführungsfrist während Queued nicht.
