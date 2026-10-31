# T098: Szenariodispatch und weitere Host-Proben

Bestandsprüfung am 2026-10-29T19:12:35Z gegen `12ad288`.
`ScenarioRunner.cs` hatte SHA256
`b66ae579576f495ee89a5d42505295285c609b60fdf20284f47c02bb0fe9804a`.
Die Ausgangsfassung bestand 980 Tests und den vollständigen Codemetr-Guard
(413 Dateien, 429 Typen, keine Befunde). `ExecuteAsync` hatte Komplexität 20
und duplizierte Szenarionamen in Zulassung und Ausführung.

## Entscheidung vor der Änderung

Eine unveränderliche `FrozenDictionary` ordnet feste CLI-Szenarien ihren
Methoden zu. Sie ist kein veränderliches Providerregister und führt keine
Library-DI, Authentifizierung oder Service-Locator-Logik ein. Parserfehler
bleiben FAIL, fehlende Abläufe NOT RUN; der Live-Freigabecheck bleibt vor I/O.
Root-, Fehler- und Reportbehandlung bleiben unverändert. Die anschließenden
37 CLI-, Root-, Consumer- und Szenarioregressionen sowie der vollständige Guard
waren grün. Für diese strukturtreue Änderung wird kein Red behauptet.

## Erweiterung T094

Vor Beginn am 2026-10-29T19:15:12Z gemessene SHA256:

| Datei | SHA256 |
| --- | --- |
| ProbeHost.cs | `96b3fc6a53eb4e8bff56d0f56efd62755a38b6a3c26f2368fd4f963c4cb3aacb` |
| ScenarioRunner.cs | `adeb09755439f79b48f32ad59a1eaeaea2a7dcfb9dbc46dc5b21f263757001f1` |
| ThirdContractProvider.cs | `70ab557d021c8a0284218bacc27c237113438dc54fc0927e12cc464799b9cb25` |

`ProbeHost` komponiert bereits öffentliche Runtime, native Lease und SQLite.
Seine explizite Profilkonfiguration und sein Sitzungskontext können erweitert
werden, ohne Produktregeln zu ändern. Die neue Routentabelle ist der einzige
Erweiterungspunkt für die Auswahl. Der vorhandene `ThirdContractProvider`
hängt nur von BCL/Common ab und wird als konsumierender Beispieladapter verlinkt.
Es entsteht keine Abhängigkeit auf eine Testassembly oder Xunit.

- Isolation: zwei native Stores mit unterschiedlichen Registrierungen und
  Profilen; fremde IDs sind nicht sichtbar, Stop links beeinträchtigt rechts nicht.
- Retention: erfolgreiche geschlossene Sitzung ausdrücklich lokal löschen;
  ungewisse Ausführung und Reservierung bleiben erhalten, Löschung wird abgewiesen.
- Extensions: externer Agentvertrag mit Format 7 neben einem zweiten Adapter;
  parallele Ausführung, feste Identität und native Fortsetzung überprüfen.

Regressionen prüfen zusätzlich die tatsächlichen SQLite-Daten nach Prozessende.
Risiken sind vermischte Konfiguration, erfundene Endgewissheit und überschriebene
Adapterversionen. Es werden weder neue Libraryregeln noch Live-Nachweise behauptet.
Drei neue CLI-Fälle waren fachlich rot, acht bisherige Fälle grün. Gitbasis,
Diff und Hashsatz liegen unter `artifacts/tdd/host-composition-scenarios-red-*`.
Der spätere Compilerfehler eines fehlenden Kontextparameters ist kein Red-Beleg.
Nach Ergänzung dieses Parameters bestanden alle elf Szenariotests.
