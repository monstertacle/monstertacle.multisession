# Endliche Prozessbudgets und erhaltene Fehlerursachen

Ausgangshashes gemessen 2026-10-29T19:46:51Z gegen 9632f7e. Diese Dateien
waren beim Änderungsschnitt auf 4bf6c50 unverändert; keine Produktregeländerung.
ProbeProcess.cs ab951be108a210b99c6baf398b68bc3c7dcbe2f9c81ec419783a854746889ece;
ValidationScenarioTests.cs 96f28b7c5c638c8da65144ef7cafb40ba9d01a54e6b244e91b7762222698c963;
PrivateStoreDirectory.cs ae66b7db8260faa4cd057b63ac253e505532415af2da875954292fa52aaf28fc.

Windows-CI 40936649050 und 40941309691 brach die Routing-Probe nach ungefähr
einer Minute ab; die nachfolgende Directory.Delete-Exception zur noch gesperrten
.multisession.lock verdeckte die ursprüngliche Prozessausnahme. 40940097923
meldete stattdessen den endlichen Report StorageUnavailable. Zwischenliegende
native Läufe waren grün. Das belegt Intermittenz und Diagnoseverlust, noch keine
bestimmte Produktursache. CrashHarness behandelt bereits bekannte Windows-
Sharing-Verzögerungen nach einem Kill; der allgemeine Verzeichnisfixture nicht.

Entscheidung vor Änderung: Routing mit 100 dauerhaften Requests erhält wie die
zehn echten Recovery-Neustarts ein endliches Fünfminuten-Prozessbudget. SC-002
fordert Zuordnung/FIFO, keine 60-Sekunden-Latenz; SC-003/SC-004 bleiben unverändert.
PrivateStoreDirectory wartet ausschließlich bei Windows-Fehler 32/33 höchstens
30 Sekunden auf Freigabe und wirft dann weiterhin. Keine Rechtekorrektur,
Prozesssuche oder Unterdrückung von Fehlern. Bereits beobachtete CI-Fehler sind
der tatsächliche Red-Beleg; es wird kein Linux-Red für Windowsfreigaben behauptet.

WaveProbeFlow ergänzt bei fehlgeschlagener Admission nur eine feste Stufenkennung
zur vorhandenen öffentlichen Fehlerkategorie. Keine Rohfehler, Inhalte oder Pfade.
Dadurch lässt sich ein weiterer StorageUnavailable bei Submit von späteren
Abfragen unterscheiden. Gezielte CLI-/Crashregressionen und Vollguard folgen;
Windows-CI muss erneut laufen. Ein einzelnes Grün erklärt die Intermittenz nicht.
