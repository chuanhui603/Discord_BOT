## Context

The current project is a minimal .NET 8 Worker host with no Discord runtime, no provider clients, and no existing chat-domain contract. The change introduces a Discord-first chatbot architecture that must support two distinct query paths: general chat that may degrade to Ollama when the primary provider is unavailable, and knowledge-grounded chat that must remain tied to Dify as the RAG authority.

The design needs to stabilize boundaries before implementation so command handling, orchestration, provider clients, and user preference storage can evolve independently. The key constraints are a single slash-command entry point, CPU-only Ollama capacity, and a requirement that degraded answers never masquerade as grounded knowledge answers.

## Goals / Non-Goals

**Goals:**
- Define a single, explicit Discord interaction model using `/chat mode:general|knowledge` plus preference commands for degraded warnings.
- Define stable application-layer contracts for chat request input, chat result output, outcomes, response kinds, and internal error classes.
- Define deterministic retry and fallback rules with Dify as the primary provider and Ollama as a degraded fallback only for eligible general-chat requests.
- Define conversation update behavior so only successful Dify turns advance the Dify conversation pointer.
- Define user-facing response categories that separate successful answers, guidance, and temporary unavailability.

**Non-Goals:**
- Building the Discord gateway/runtime, provider HTTP clients, or persistence implementation in this change.
- Designing a local RAG stack for Ollama.
- Adding automatic query-mode classification or multi-provider routing beyond Dify primary and Ollama fallback.
- Designing advanced streaming UX, attachments, or multi-turn local-memory behavior for Ollama.

## Decisions

### 1. Use a single `/chat` slash command with explicit mode selection
The bot SHALL expose one primary command, `/chat`, with a required `mode` option set to `general` or `knowledge`.

Rationale:
- Keeps the interaction surface simple while still making routing intent explicit.
- Avoids model- or heuristic-based query classification, which would introduce false positives and make fallback behavior hard to trust.

Alternatives considered:
- Separate `/ask` and `/kb` commands: simpler internally, but increases command sprawl.
- Automatic mode classification: rejected for v1 because misclassification would either deny fallback unnecessarily or produce ungrounded answers for knowledge requests.

### 2. Keep a provider-neutral orchestration contract
The application layer SHALL use provider-neutral chat request and chat result contracts. Discord handlers provide input context; provider clients provide provider metadata; the orchestrator derives outcome, response kind, and conversation update behavior.

Rationale:
- Prevents Dify- or Ollama-specific fields from leaking into the Discord interaction layer.
- Makes future refactoring possible without rewriting command handlers.

Alternatives considered:
- Passing provider-specific DTOs through the application layer: rejected because it couples user interactions directly to vendor APIs.

### 3. Model result state with `Outcome` and `ResponseKind`
The orchestrator SHALL separate execution state from response meaning:
- `Outcome`: `Success`, `Degraded`, `Failed`
- `ResponseKind`: `Answer`, `Guidance`, `Unavailable`

Rationale:
- Allows "no answer" situations to complete successfully while returning guidance rather than a fabricated answer.
- Keeps Discord rendering rules simple and predictable.

Alternatives considered:
- A single status enum: rejected because it becomes either too coarse or too large to maintain.

### 4. Use Dify as the sole authority for knowledge-grounded queries
All knowledge-mode requests SHALL go to Dify first and SHALL NOT fall back to Ollama.

Rationale:
- Dify owns the RAG pipeline and source grounding.
- CPU-only Ollama cannot provide equivalent retrieval guarantees.

Alternatives considered:
- Allowing knowledge-mode fallback to Ollama: rejected because it would silently degrade grounded answers into ungrounded ones.

### 5. Allow degraded fallback only for eligible general-chat failures
General-mode requests SHALL retry Dify once for transient failure classes, then MAY fall back to Ollama if the final Dify failure is eligible.

Eligible fallback classes:
- `RateLimited`
- `QuotaExceeded`
- `Timeout`
- `TransientUpstream`

Non-eligible classes:
- `NoAnswerOrNoContext`
- `BadRequest`
- `AuthOrConfig`
- `Internal` by default

Rationale:
- Preserves availability for conversational use cases while avoiding misleading answers when the system lacks grounding or is misconfigured.

Alternatives considered:
- Zero retries: simpler but less resilient to brief provider instability.
- Multiple retries or multi-hop fallback: rejected because Discord interactions and CPU-only Ollama make long recovery paths too slow.

### 6. Treat "no answer" as guidance, not failure
When Dify completes but cannot provide enough grounded context, the orchestrator SHALL return `Success + Guidance` and SHALL NOT invoke fallback.

Rationale:
- Avoids hiding retrieval gaps behind a free-form local model answer.
- Gives the user a concrete next action instead of a generic system error.

Alternatives considered:
- Fallback to Ollama on no-answer: rejected because it encourages hallucinated completions for knowledge-adjacent queries.

### 7. Scope conversations to user plus channel or thread
The session identity SHALL be derived from Discord user id plus thread id when present, otherwise user id plus channel id.

Rationale:
- Prevents unrelated discussions in different channels or threads from sharing context.

Alternatives considered:
- Per-user global conversation: rejected because it would blend unrelated contexts.
- Guild-wide conversation: rejected because it is unsafe in multi-user settings.

### 8. Update the Dify conversation pointer only after successful primary answers
Only `Success + Answer + Primary` SHALL update the persisted Dify conversation pointer. Guidance responses MAY be left out of conversation advancement in v1, and fallback results SHALL NOT update the Dify pointer.

Rationale:
- Prevents degraded or guidance-only turns from polluting the authoritative Dify conversation chain.

Alternatives considered:
- Writing all outcomes back into the same conversation stream: rejected because Dify and Ollama do not share equivalent session semantics.

### 9. Keep degraded warning display as a user preference, not part of the core DTO
The system SHALL expose `/checkon` and `/checkoff` preference commands. The renderer SHALL use this preference when deciding whether to show a degraded warning banner for `Degraded + Answer` results.

Rationale:
- Keeps the core contract focused on execution semantics rather than presentation preferences.

Alternatives considered:
- Embedding the warning preference in each chat request: rejected because it duplicates user preference state in every command invocation.

## Risks / Trade-offs

- [CPU-only fallback is slow] -> Keep fallback limited to general mode, use a single retry on Dify, and return concise fallback answers.
- [Provider error mapping may differ from assumptions] -> Normalize raw Dify/Ollama failures through a provider adapter layer and log original status/details for diagnostics.
- [Treating 400-class issues as user-facing unavailability hides root cause] -> Preserve fine-grained internal error classes while rendering a simpler external `Unavailable` response.
- [Guidance responses may feel like non-answers] -> Make guidance text actionable, asking for a narrower question, a document name, or more context.
- [Single-command UX may still confuse users] -> Keep `mode` required and document examples in command descriptions.

## Migration Plan

1. Add Discord interaction handling and register `/chat`, `/checkon`, and `/checkoff`.
2. Introduce application-layer chat contracts and the orchestrator interface.
3. Implement Dify client integration and error normalization.
4. Implement Ollama fallback client with tighter latency limits than Dify.
5. Add session and preference storage for conversation pointers and degraded-warning settings.
6. Validate status mapping and response rendering against the scenarios in the specs.

Rollback strategy:
- Disable slash command registration and fall back to the current no-op worker if the new runtime path proves unstable.
- Because the current project has no prior chatbot behavior, rollback is operationally low-risk.

## Open Questions

- Which Discord library surface will be used for slash commands and interactions beyond the currently referenced `Discord.Net.Core` package?
- Which persistence mechanism will store conversation pointers and warning preferences in v1?
- What exact Dify response payload signals `NoAnswerOrNoContext` for the chosen workflow/chat endpoint?
- What Ollama model and timeout budget should be enforced on the target CPU-only environment?