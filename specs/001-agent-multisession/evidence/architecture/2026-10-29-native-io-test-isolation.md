# T126: unabhängige Echt-I/O-Testfälle isolieren

Vor Umsetzung gemessen2026-10-29T23:33:14Z; Basis7bd658d, Source
203fbcf8453fe8baabdf184e0ed40265bd5fbbfb7313dd16fc8a7db9998017ed.
Native CI40972345390 Versuch1:402/405Integrationfälle bestanden. BootstrapOptionsTests
scheitert zweimal bereits beim Seed-Submit mit AdmissionTimedOut, vor dem eigentlichen
Abgleich abweichender Optionen. EmbeddedConsumerTests endet mit Dateifreigabefehler
beim Fixturecleanup nach51s. Derselbe Source bestand die vollständige unabhängige
Windows-Stundenvorprüfung. Kein bestätigter neuer Fehler der produktiven Lease;
die separate Kernelwartebarriere hat600Kill-/Recoveryzyklen bestanden.

Geprüfte Quellen/SHA256:
- BootstrapOptionsTests766cc682b0c655b2869be5cf4f14dee1cc4b0a0ae068838ceb416521aa8baf1c
- EmbeddedConsumerTestsba2fed0315eaf114fe0ccefc94cea1a4e190786893ef3c9f895018ea0a9c427d
- IntegrationTests.csprojf31251c392e61aa0e2047d26471c7c080738973abe7284dc89429fdff8c2ed4a
- SqliteWorkQueueed4f1913ccb6e500ff3b5a52b23d68da7d1cc09125e2c569a30b9a03a08e7648
- RequestAdmission1be37a42532e240b21545aa0cc90ffad5aec23f4b620cb0d40cc7b42ae83806e

Kontrollfluss: unabhängige xUnit-Testkollektionen besitzen jeweils echte SQLite-
Verbindungen/Dateien, die auf denselben Runner-I/O-/Threadpoolressourcen liegen.
RequestAdmission misst das unveränderte5s-Defaultbudget ab Eintritt, auch bei
Seed-Submit. Die vorhandene Exclusive-storage-Kollektion isoliert bisher nur
Diskfull-/Prozess-/CLIgruppen, nicht alle übrigen echten SQLitefälle. Native TRX
zeigt an den beiden Bootstrapfehlern3 beziehungsweise12 zeitlich überlappende
andere Fälle, beim Embeddedfall21; externer Readonlyabgleich
artifacts/tdd/windows-io-concurrency-failure.json. Das beweist Überschneidung,
keine gemessene CPU-/Datenträgerursache und keine vollständige Ausnahmeherkunft
des durch Cleanup verdeckten Embeddedfehlers.

Entscheidung: eine Assembly-CollectionBehavior-Konfiguration nur für IntegrationTests
deaktiviert konkurrierende unabhängige Testkollektionen. xunit2.9.3 dokumentiert
DisableTestParallelization im tatsächlich restaurierten xunit.core.xml. Keine neue
Paketversion, keine fachliche Schwelle, Timeoutanhebung, Retry oder Produktionsänderung.
Die innerhalb einzelner Tests mit Barrieren erzwungenen parallelen Sessions/Anfragen,
CAS-Races, echten Prozesse und Capacityassertions bleiben unverändert. Diese prüfen
Produktparallelität; konkurrierende voneinander unabhängige Fixtures sind keine
Abnahmeanforderung. Unit-/Contract-/Plattformassemblies bleiben unverändert.
Die neue Assemblydatei wird nicht über die expliziten Host-Fixture-Compilelinks
in den ValidationHost oder die Produkte kopiert. Risiko: längere Integrationstorlaufzeit;
unveränderte vorhandene Workflowbudgets müssen tatsächlich eingehalten werden.

Prüfung: ursprünglichen nativen Fehler/TRX/Quellstand vor Änderung sichern; keine
künstliche Attributspiegeltestklasse. Alle405Fälle unverändert ausführen, native
TRX auf überlappende Testkollektionen unabhängig auswerten, volle native Quality/
Guard/Paket-/Publishregression. Alle bereits vorhandenen tatsächlichen Default-/
Frist- und Nebenläufigkeitsassertions behalten. Bei fortgesetztem Fehler weitere
Diagnose am konkreten Befund, Isolation allein ist keine behauptete Ursachenklärung.
Neuer Buildinput verlangt erneuten Source-/Stundenstand.

Lokale Bestätigung2026-10-29T23:38:13Z: vollständiges Tor
linux/ed7bf2000cec4f8b844c5dc3a2d02196,1036Tests,97,5796 % Produktzeilen.
Source d51bf9f9efd33341e34037a6a027cac887abb117111fa6afa2d56d66d6d37cbf.
Fullguard442Dateien/459Typen,0Befunde/Parsefehler/Skips. Unabhängige tatsächliche
TRX-Zeitleiste vorher:405Fälle/402PASS/3FAIL,max4konkurrierende Klassen;
danach405PASS/max1Klasse und1Testfall. Rohsummaries native-io-before.json und
native-io-linux-after.json; externer Sweepprüfer unter
/home/user1/dev/.agenttemp/multisession-trx-concurrency.ps1. Das prüft tatsächliche
Testausführung statt nur ein gesetztes Attribut. Native Windowsbestätigung folgt;
keine pauschale Behauptung fehlerfreier Hardware oder allgemeiner Flakefreiheit.
