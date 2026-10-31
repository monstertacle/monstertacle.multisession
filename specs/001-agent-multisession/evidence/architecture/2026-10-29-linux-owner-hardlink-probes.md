# Linux-Fremdeigentümer und Hardlinks: ergänzende Regression

Gemessen 2026-10-29T21:33:07Z. Basis1b2aba6.
LinuxOwnershipRejectionTests.cs SHA256 a690851ce743e1392a97ede48529939ddc7cd63acc53cb22808037b76c6c086a.
LinuxFileIdentity.cs 378ef98230c2924404231badf645d35041fde3be32cef3d5018319335091f0b2.
LinuxStateDirectoryLease.cs 56906947d2461703581fac6915c1ed34a12c24fcfca8146911f8d95d759f0069.

T088/T092: PrepareFile öffnet nur descriptorrelativ und prüft danach RequirePrivate.
Die gemeinsame Identität prüft effektive UID, exakte Modi und für Dateien Linkzahl1;
OpenExisting prüft die Verzeichnisidentität vor Besitzerlock. Bestehende native Tests
prüfen Modi, Symlinks, Austausch, Prozessbesitz und Kill, aber keinen tatsächlich
abweichenden Datei-/Verzeichniseigentümer oder Linuxhardlink. Keine neue Produktregel
benötigt: ergänzende Tests vorhandener Zusicherung, kein behauptetes neues TDD-Red.

Privilegierte Fixtureoperation ausschließlich sudo -n chown --no-dereference an einem
von dieser Testklasse frisch erzeugten Pfad unter linux-rejection-GUID. Original-UID
per id -u ermitteln; Fremd-UID65534 (bei identischem Host alternativ65533), im finally
sofort zurücksetzen. Keine Nutzeranlage, Gruppen-/Dienst-/Policyänderung. ln erhält
ausschließlich strukturierte Argumente für zwei eigene Fixturepfade. Prozessaufrufe
haben30s-Grenze und werden bei Überschreitung beendet; kein Shellstringinterpolieren.
Kernelleseverweigerung ist ebenfalls gültige sichere Abweisung, kein Beleg, welcher
interne Ownercheck zuerst entscheidet. Nach Restore ursprünglichen Inhalt und Modi
prüfen, keine Permissionsreparatur im Produkt. Läuft weder sudo noch die native
Fixture, ist das FAIL, kein Skip/PASS. Ubuntu-CI besitzt für die existierende Diskfull-
Fixture bereits denselben nichtinteraktiven sudo-Zugang.

Tests: Fremdeigentümer für Directory/File als zwei Fälle, Hardlink als weiterer Fall;
native Platformsuite und danach gesamter unveränderter Guard/Coveragescope. Windows
entfernt ausschließlich die bestehende Linux-Testklasse aus seinem TFM, wie bisher für
native Tests; plattformneutraler Processcode bleibt vollständig gemessen. Netzwerk-
filesystem- und Windows-11-Nachweise werden daraus ausdrücklich nicht abgeleitet.
