# Data model
No SQLite schema or stored error changes. Optional FailureDetails on existing SessionOperationException and AdmissionCancellationException; their existing constructors remain unchanged. PublicError remains byte-for-byte at its original source contract.
ValidationFailure is an internal report payload pairing a PublicError with optional operational details; cleanup payloads are an ordered list with no silent truncation.
Executor state remains waiting/running/finished plus closed; cancelled waiting work must never reach its action. Cleanup completion means the connection owner has disposed the connection, not that remote provider work ended.
