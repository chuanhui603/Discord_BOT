## 1. Host and dependency setup

- [x] 1.1 Add the Discord interaction/runtime packages needed to register slash commands from the existing Worker host.
- [x] 1.2 Add HTTP client and configuration registrations for Dify and Ollama endpoints, credentials, and timeout settings.
- [x] 1.3 Add configuration models for bot settings, provider settings, and fallback limits.

## 2. Application contracts and orchestration

- [x] 2.1 Implement the provider-neutral chat request, chat result, outcome, response kind, response source, failure stage, and error class models.
- [x] 2.2 Implement the chat orchestrator interface and request flow that derives session identity from user plus channel or thread context.
- [x] 2.3 Implement conversation pointer update rules so only successful primary Dify answers advance the persisted Dify conversation state.

## 3. Provider integrations and fallback policy

- [x] 3.1 Implement the Dify client for primary chat execution and normalize Dify responses into internal success, guidance, and error outcomes.
- [x] 3.2 Implement the Ollama client for degraded fallback answers with CPU-appropriate timeout and response limits.
- [x] 3.3 Implement the retry and fallback policy so general mode retries Dify once for eligible transient failures and only then falls back to Ollama.
- [x] 3.4 Implement the knowledge-mode policy so provider failures return temporary unavailability without invoking Ollama.

## 4. Discord commands and preference handling

- [x] 4.1 Implement the `/chat` command with required `mode` and prompt inputs and map command input into the chat request contract.
- [x] 4.2 Implement `/checkon` and `/checkoff` commands to persist the user's degraded-warning display preference.
- [x] 4.3 Implement Discord response rendering for `Answer`, `Guidance`, and `Unavailable` results, including optional degraded warnings when enabled.

## 5. Storage, observability, and validation

- [x] 5.1 Add storage for per-user per-channel-or-thread conversation pointers and degraded-warning preferences.
- [x] 5.2 Add structured logging and correlation metadata for primary attempts, retries, fallback attempts, and normalized error classes.
- [x] 5.3 Add tests or verification coverage for general-mode fallback, knowledge-mode no-fallback behavior, guidance responses, and preference-controlled degraded warnings.