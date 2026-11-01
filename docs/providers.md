# Native providers

The library registers ordinary `ISessionProvider` objects per host instance.
Profiles select providers and opaque model/deployment identifiers explicitly. The core
has no model catalog and does not switch vendor or model automatically.
A `ModelResponse` is not evidence of an external agent task.

## Conversations and Responses

`ConversationSessionProvider` implements the bounded REST contract `/v1`:
open a Conversation via POST, reopen the same reference via GET, execute new input
via Responses POST with `conversation`, `background=true`, `store=true`.
`previous_response_id`, transcript replay, custom tools or compaction are not
added. The adapter supports `ModelResponse`, not `AgentTask` execution.

```csharp
using var provider = new ConversationSessionProvider(
    new ConversationOptions
    {
        Endpoint = new Uri("https://api.openai.com/v1/"),
        CredentialReference = "host-owned-reference"
    },
    hostCredentialResolver);
```

The consuming host provides the resolver and holds the authorization
to use the configured endpoint. The resolver receives the reference anew on every HTTP call;
credential values are added neither to the binding nor to the profile state.
HTTPS is required. HTTP is permitted only with `AllowLocalHttp=true` for loopback test endpoints.
Endpoints with UserInfo, query or fragment are rejected. A compatible
access must offer this Conversations/Responses contract; Chat Completions alone
is not sufficient. Model names are passed through unchanged. Permitted profile setting field:
`instructions`; unknown fields are rejected before I/O.

Operation IDs are confirmed via the awaitable callback before polling or returning a result.
Polls start with an interval of one second. Temporary GET failures 429/503 may
be retried within the overall deadline with Retry-After. Executing POSTs are
never retried automatically. Inspect is a single functional GET access (with bounded
transport retries); Cancel sends POST once and reads the final status afterwards.
None of these operations decides on its own about local recovery or slot release.

Header deadline: 30 seconds. Overall send deadline: `ProviderCall.Timeout`, including
callbacks and polling. Open/Reopen/Inspect/Cancel have a finite 600-second deadline;
the host core may set a shorter outer deadline. `MaximumResponseBytes` limits native
JSON responses to 32 MiB by default; `MaxOutputBytes` separately limits the final UTF-8
text output. An output that is too large becomes an empty failure, not a truncated success.
Timeout or connection loss provides no external evidence of the end. Unknown requires the
intended recovery decision. A confirmed native final response remains distinguishable from a
local cancellation request.

Without an injected handler, the adapter owns and disposes of an HttpClient with disabled
redirects/cookies. An explicitly injected HttpMessageHandler remains host-owned and must
itself respect the transport boundary; it serves in particular deterministic fixtures.
The host must dispose of the provider after its runtime use has ended.

The contract was checked deterministically with synthetic responses. No statement about
real account authorization, model availability or vendor acceptance. Data is stored at the
vendor; local retention deletes no Conversations/Responses and no
backups. The authoritative terms come from the approved provider account.
Sources: [Conversation state](https://developers.openai.com/api/docs/guides/conversation-state),
[Background mode](https://developers.openai.com/api/docs/guides/background).

## Gemini Interactions

`InteractionSessionProvider` implements the stable `/v1/interactions` contract with
a fresh `x-goog-api-key` per HTTP call. Open creates exclusively a local unbound
binding. The first Send establishes the first native interaction ID; this remains
`SessionReference`. The separate `ContinuationState` is exclusively the last
confirmed completed interaction head. Follow-up sends pass its ID as
`previous_interaction_id`, each time only new input and a fixed `system_instruction`.
Unknown settings and explicitly configured v1beta paths are rejected.
No custom tool/continuation-token/compaction flow, no transcript history in the request.
ModelResponse is supported; AgentTask requires a suitably registered external adapter.

`InteractionOptions` configures Endpoint, CredentialReference, AllowLocalHttp and
MaximumResponseBytes with the same security/body defaults as above. Native resources
are read via steps/model_output/content/text; this is the current stable contract.
A completed head remains known even with a local OutputLimitExceeded/empty-text error
message. A first native failed execution without a completed head is not permission for a
second unbound root: Send/Reopen refuse this state locally.

After an uncertain Send, `RequiresOperationAwareRecovery` requires the separate
`IExecutionRecoveryProvider`. It reads the persisted uncertain operation, checks
known IDs and existing predecessor evidence and adopts only a confirmed new
head. An old readable head alone does not permit continuation. Native failed/incomplete/
cancelled can reuse the old head only after an additional check; open
or client-dependent states keep recovery blocked. Local case decision/CAS remains the
task of the core. Inspect/Cancel do not release a local reservation and do not open a session.

Unknown native HTTP 400 codes remain ExecutionFailed/NotStarted instead of a heuristic
over free-form error messages. The concluding specific Gemini context code acceptance is
explicitly still open (T034/T105). Deterministic fixtures are not vendor live evidence.
Native storage/retention is governed by the approved account; local deletion
deletes no interactions or backups at the vendor.
Sources: [stable Interactions reference](https://ai.google.dev/api/interactions-api-v1),
[Interactions overview](https://ai.google.dev/gemini-api/docs/interactions-overview).

Further implementation and open checks: [Overall implementation](../specs/001-agent-multisession/evidence/implementation-progress.md).
