# Ergänzende native Linux-Mount-/Filesystemprüfung

T088/T092, tatsächlicher Lauf2026-10-29T22:15:13.7334194Z. Ausgeführt gegen das
Linux-NuGet aus nativem Ubuntu-CI40962572345, Sourcehash95780bfa75c745cd0adb804e0a1ac3dfcea6474169900c8ab402990248e40f5b.
Private neue Konsumprobe mit ausschließlich PackageReference außerhalb des Produktrepos,
frischer eigener Cache, erster und Locked-Restore sowie Releasebuild ohne Warnungen/Fehler.
Archivhash des restaurierten Pakets stimmt exakt mit dem heruntergeladenen Produzenten-
archiv überein:38261f651b57267779ab45076424616f2818322b1b59ddfc96263f6ce753088d.
Eigener C#-Probecode separat mit Codemetr geprüft:1Datei/1Typ, keine Befunde/Parsefehler/Skips.

`sudo -n timeout --kill-after=2s 30s unshare --mount --propagation private` erstellt nur
in dieser privaten Namespace einen Bindmount und ramfs; `setpriv` führt die eigentliche
.NET-Probe anschließend als ursprüngliche UID/GID aus. Kein Dienst, kein Netzwerkserver,
kein Mount im Hostnamespace und keine globalen Rechte-/Konfigurationsänderungen.
Das externe Messgerüst bleibt gemäß Arbeitsumgebungsregel außerhalb des Produktrepos.
Quell-/Skript-/Lock-/Paket-/Assemblydigests und Originalpfade im [Manifest](linux-mount-proof.json).

Tatsächliche Ergebnisse auf Debian13x64, kein umbenannter Ubuntu-Test:

- Zwei Bindpfade derselben privaten Directory können nicht gleichzeitig besitzen:
  der zweite erhält StoreAlreadyOwned. Nach expliziter Freigabe ist der Alias erwerbbar;
  ein zweites Dispose der alten Lease löst den Nachfolger nicht. Ursprüngliche Datei ist
  über beide Pfade identisch lesbar. Kein unsicherer pfadbasierter eigener Lockbestand.
- Ein korrekt privat berechtigtes, selbst besessenes Verzeichnis auf ramfs wird mit
  StorageUnavailable abgewiesen; es entsteht keine Ownershipdatei und der Marker bleibt
  unverändert. Tatsächliches fstatfs-Magic858458f6 liegt außerhalb der geschlossenen Liste.
- Derselbe geschlossene Sourcevergleich wird unabhängig gegen installierte Linux-ABI-
  Konstanten geprüft: NFS6969, CIFSFF534D42, SMB2FE534D42,9p01021997,Ceph00c36400
  sind sämtlich ausgeschlossen. Die Quelle ist unter ihrem SHA gesichert. Dies ist ein
  deterministischer Nichtzulassungsbeleg, kein behaupteter realer NFS-/SMB-/9p-/Cephmount.

Disposition der zuvor unspezifisch offenen Mount-/Netzwerkmatrix: kanonische Pfade,
Symlinks, echte Eigentümer, Hardlinks, File-/Directorytausch sowie Zweitprozess/Kill/
Recovery sind im tatsächlichen Ubuntu-CI mit31Plattformfällen geprüft. Bindalias und
repräsentative unbekannte Filesystemklasse ergänzen die oben benannte reale Linuxprobe;
Netzwerkpfade sind lexikalisch abgewiesen und bekannte Netzwerktypen durch die genaue
Allowlist/ABIprüfung ausgeschlossen. Der Task verlangt keine Unterstützung oder einen
Live-Mount jedes einzelnen verbotenen Netzwerkfilesystems. Deshalb ist diese technische
Abnahme belegbar, während die nicht ausgeführten Netzwerkserverexperimente ausdrücklich
NOT RUN bleiben. Kein Ersetzen des separaten Windows11-/Stunden-/Livegates.
