# T067 Kultur-/UTC-Roundtrips

Ausganga4cb321, Messung2026-10-29T16:46:56Z: StoreSerialization SHA256
bf571442de20180295187df168a4eb5f9e089c0bb2604f5e348a00406bf91c6e,
SessionTransactionsccf820dbc8e399d1a0224fce59a0568df22d3f0fa9c11fdb43b66c1b35a34fad,
SqliteSchema7115d18b65f01b46b28f1a836008e3047e48a33d5551073f9b88ca1c6e9920e6.
Utc(value) schreibt kanonische UTCspalten; generisches Write<T> serialisiert DateTimeOffset
bisher mit Originaloffset. Direkter akzeptierter SessionSnapshot mit nicht-null Offset kann
so UTCspalten und nicht-UTC-JSON erzeugen; aktueller Startguard lehnt diesen eigenen
Writeroutput später ab. Public SessionCatalog normalisiert Created bereits, das ersetzt
keinen konsistenten allgemeinen Persistenzwriter.

Vor Erweiterung entschieden: Speicherspezifischer JsonConverter<DateTimeOffset> normalisiert
nur neue Writes über ToUniversalTime und Utf8JsonWriter.WriteStringValue. Instant bleibt
gleich, Strings/opaque IDs/Settings/Input/Output unverändert. Read behält ursprünglichen
Offset aus JSON, damit vorhandene inkonsistente Daten weiterhin erkannt werden und keine
stille Migration entsteht. Keine Schemanummeränderung für unveränderte gültige UTCdaten.

Primärquellen: MicrosoftLearn JSONconverter
https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/converters-how-to
und plattformübergreifende IANA-Zonen mit ICU
https://learn.microsoft.com/en-us/dotnet/api/system.timezoneinfo.findsystemtimezonebyid .
Keine globale Hosttimezone-/NLSänderung. Tests benutzen isolierte CurrentCulture/CurrentUICulture
im Ausführungskontext und explizite echte TimeZoneInfoobjekte Berlin/NewYork/Istanbul,
Winter/Sommer; diese Eingabewerte werden mit de-DE/en-US/tr-TR persistiert. Fehlende native
ICU-/Zonendaten wären Fehler, kein Skip oder erfundenes OS-Zeitzonenresultat.

TDD zuerst: jeweils Offset0 und gleicher Instant vor/nach Wiederöffnung, dann Metadata-
Store-/Trustscope-ID, Unicode-I/Settings, vollständige Inputs/Outputs/History und Cursor
über Kulturwechsel erhalten. Zuerst tatsächliche Farbe beobachten; kein künstliches Red.
Vorhandene negative NonUTC-/Malformed-/Unknownhistoryfälle bleiben grün. Volltor/Guard und
native Matrix nötig, Produkt-/TDDgesamtfreigaben bleiben eigenständig offen.
