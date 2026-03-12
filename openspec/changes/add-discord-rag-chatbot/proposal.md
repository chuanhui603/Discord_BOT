## Why

The project currently only contains a minimal .NET Worker host and does not yet define how a Discord chatbot should handle RAG queries, provider fallback, or user-facing command behavior. We need a clear specification now so implementation can proceed with stable boundaries for chat orchestration, Dify-first routing, and degraded-mode handling.

## What Changes

- Add a Discord slash-command interaction model centered on a single `/chat` command with explicit `mode` selection for general chat and knowledge-grounded queries.
- Define a chat orchestration contract that standardizes request fields, result fields, response kinds, and internal error classes used by the bot application layer.
- Define primary-provider and fallback behavior where Dify is the primary RAG endpoint and Ollama is only used as a degraded fallback for eligible general-chat requests.
- Define failure handling, retry behavior, no-answer guidance behavior, and conversation update rules so Discord responses stay predictable.
- Add user preference commands to control whether degraded fallback warnings are shown in Discord replies.

## Capabilities

### New Capabilities
- `discord-chat-commands`: Slash command behavior for `/chat`, `/checkon`, and `/checkoff`, including mode selection and degraded-warning preferences.
- `chat-orchestration-contract`: Application-layer contract for chat requests, results, outcomes, response kinds, and conversation update behavior.
- `provider-fallback-policy`: Dify-first routing, retry policy, error classification, Ollama fallback eligibility, and degraded-mode response rules.

### Modified Capabilities
- None.

## Impact

- Affects the future Discord bot application layer, command handlers, orchestration services, provider clients, and session/preference storage.
- Introduces behavioral requirements for Dify API integration, Ollama fallback integration, and Discord response formatting.
- Establishes the expected contract before adding new packages, HTTP clients, or Discord runtime services to the current Worker-based host.