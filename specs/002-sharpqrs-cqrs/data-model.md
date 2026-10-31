# Data model
No durable model or schema changes.
Six command messages: CreateSession, CloseSession, DeleteSession, SubmitRequest, CancelRequest, ResolveRecovery.
Eight query messages: GetSession, ListSessions, ReadHistory, GetRequest, ListRequests, GetRecoveryCase, ListRecoveryCases, InspectRecovery.
LookupResult<T> carries an optional existing snapshot in a non-null envelope.
CommandAcknowledgement confirms local handler return only, with no remote termination promise.

