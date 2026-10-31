# Kontrollierte Timer für deterministische Fristtests

Bestandsprüfung 2026-10-29T20:24:23Z gegen 4c46e07 plus Start/Dispose-Schnitt.
ManualTimeProvider.cs a784f0f02dc1c4233a523cd9edb203f5398792060b6c91d5e14d33e84514abe8;
UnitTests.csproj 4b55fc7b58f9f7cf6337e885b351003ea425fa254ba1d8da91bbb9ddffedb98c;
Directory.Packages.props 8c536549c8b93f1aebefd0f628c85ae12a56d6477d37e0b009417bc99877086e.
Geprüft: alle ManualTimeProvider-Aufrufer, Testprojektgrenzen und die getrennte
DispatchClock-Fixture. Der aktuelle Provider bewegt nur UTC/Timestamp. CreateTimer
bleibt Systemimplementierung; Advance löst deshalb keine CTS-/Task.Delay-Fristen
aus. Sein eigener Kommentar bezeichnet Timerunterstützung noch als ausstehend.

Entscheidung vor Umsetzung: zuerst reale BCL-Timerpfade mit Advance fachlich rot
prüfen, ohne eine Stunde Wanduhr abzuwarten. Dann den bestehenden Fixture-Namen als
kleinen UnixEpoch-Wrapper um Microsofts FakeTimeProvider erhalten. Ausschließlich
UnitTests erhalten die neue zentrale feste Testpaketversion
Microsoft.Extensions.TimeProvider.Testing 10.7.0; keine bestehende Version wird
geändert, kein Produktionspaket erhält diese Abhängigkeit. Keine eigene Timerqueue
oder fragile Nachbildung der BCL-Timersynchronisation. Bestehende UTC-/Elapsed-
Semantik und alle bisherigen Clock-Verbraucher werden regressiert. Der echte Soak
verwendet weiterhin ausschließlich TimeProvider.System und bleibt eine echte Stunde.

Primärquellen geprüft: https://www.nuget.org/packages/Microsoft.Extensions.TimeProvider.Testing/10.7.0
und https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.time.testing.faketimeprovider.-ctor
sowie der verlinkte dotnet/extensions-Quellcode. FakeTimeProvider(DateTimeOffset)
und Advance sind dokumentiert; Timer werden beim Vorschieben ausgelöst. Die
bekannte xUnit-v2-Kontextgrenze wird beachtet: neue Tests prüfen synchron die
Timer-/Taskzustände und warten nicht in ConfigureAwait(false)-Fortsetzungen.
Kein globaler SynchronizationContext wird geändert. Neue Abhängigkeit wird im
Locked-Restore/Audit und vollständigen Guard geprüft. T011/T071/T076.

Zwei echte Rotfälle und ein bereits korrekter Grenzfall: CTS wird nach einer
kontrollierten Stunde nicht cancelled, Task.Delay nicht abgeschlossen. Archiv
controlled-timer-red-* wurde vor Green gesichert. Mit dem expliziten Testpaket
sind sämtliche 359 Unit-Tests grün; keine globale Uhr oder SynchronizationContext-
Änderung. Vorherige Testerwartungen für UnixEpoch/Elapsed bleiben erhalten.
