# T119 keine stille Zeichenersetzung an Nachrichtengrenzen

Ausgang39e1446, Messung2026-10-29T15:56:08Z: Utf8MessageLimits SHA256
daf9f29787bf2a4a30098337983f6a6adea6a281682097ce53afda3475549311,
ProviderReplyPolicy96ab0748603a0c8e04f2a13f4188f6d71b4347fa921a8029414c30796524600c,
RequestSubmissionRules819c294ddbc54f29674b59209e9de48aa5b6a438b42a51697267a4cf9a204fd9.
Utf8MessageLimits verwendet Encoding.UTF8.GetByteCount mit Defaultfallback, bestehende
Tests prüfen leere Texte/Emoji/inklusive Grenzen, bislang keine unpaarigen UTF16-Surrogate.
RequestAdmission prüft vor Kandidat/Commit; SQLiteannahme verwendet dieselbe Regel.
ProviderReplyPolicy behandelt fehlenden Output bei bereits fehlgeschlagenem Reply bewusst
anders als fehlenden erfolgreichen Output; diese Kategorie darf nicht pauschal überschrieben
werden. Generischer Adapter kann unmittelbar eine ungültige .NET-Zeichenfolge liefern,
unabhängig davon, ob ein nativer JSONparser solche Antworten bereits verwirft.

Primärquellen vor Umsetzung: MicrosoftLearn UTF8Encoding(Boolean,Boolean) beschreibt
throwOnInvalidBytes sowie EncoderFallbackException für ungültige Zeichenfolgen;
https://learn.microsoft.com/en-us/dotnet/api/system.text.utf8encoding.-ctor?view=net-10.0
https://learn.microsoft.com/en-us/dotnet/api/system.text.encoderfallbackexception?view=net-10.0 .
Entscheidung: gemeinsamer privater UTF8Encoding(false,true), ByteCount ohne Bufferkopie,
Fallbackexception nur bereinigte InvalidInput beziehungsweise ExecutionFailed mit originaler
Ausführungsgewissheit. Nichtleerer ungültiger Output wird auch bei bereits Failed geleert,
OutputCompletefalse, KategorieExecutionFailed; leerer legitimer Fehlerreply behält seine
spezifische Kategorie. Kein Trim/Replacement/Retry, keine Gewissheit aus Zeichenfehler.

TDD zuerst: einzelne hohe/niedrige Surrogate zur Laufzeit aus Codepoints erzeugen, keine
ungültigen Literale/Metadaten in Source/TRX. Input vor Annahme nullCommits/nullSends,
OutputFailed mit NotStarted/ConfirmedEnded/Unknown, tatsächlicher Dispatcherabschluss
als bereinigtes Failed statt Succeeded. Gültige Emoji/Whitespace und exakte7Bytegrenze
zusätzlich erhalten. Nach Green vollständige Source-/Coverage-/Guardregression und native
CI; T119 erst nach passenden nativen Nachweisen schließen. Historische Inhalte werden
nicht stillschweigend rekonstruiert oder geändert.
