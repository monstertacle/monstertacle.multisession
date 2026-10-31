# Failure contract
Closed phases distinguish storage preparation, preflight, open, execution, transaction body, commit, admission lookup and admission commit. Numeric SQLite primary/extended and OS codes are optional.
No provider text, exception type/message/stack, input, SQL, path or credential enters diagnostics. Adding details cannot change Code, AdmissionStatus, candidate identity or Certainty.

Details are optional init properties on existing SessionOperationException and
AdmissionCancellationException. Preserve their original constructors. PublicError
and StoreSerialization remain unchanged; this contract does not promise durable
diagnostics or details on every asynchronous ProviderReply/Unknown result.
