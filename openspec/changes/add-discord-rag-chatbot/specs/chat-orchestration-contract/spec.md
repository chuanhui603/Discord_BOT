## ADDED Requirements

### Requirement: Chat requests use a provider-neutral contract
The application layer SHALL accept chat requests through a provider-neutral contract that includes `Prompt`, `UserId`, `ChannelId`, optional `ThreadId`, and `QueryMode`. The contract MAY also include optional correlation and guild metadata, but SHALL NOT require provider-specific identifiers such as Dify workflow ids or Ollama model names.

#### Scenario: Discord handler builds a request for orchestration
- **WHEN** a Discord command handler passes a user message into the application layer
- **THEN** it supplies the required provider-neutral fields without embedding provider-specific routing metadata

### Requirement: Chat results separate execution state from response meaning
The application layer SHALL return chat results that include `Outcome`, `ResponseKind`, `ResponseSource`, `ResponseText`, optional `ErrorClass`, and a flag indicating whether conversation state should be updated.

#### Scenario: Successful primary answer is returned
- **WHEN** the orchestrator completes a request through Dify with an answer
- **THEN** the result is returned as `Outcome=Success`, `ResponseKind=Answer`, and `ResponseSource=Primary`

#### Scenario: Guidance response is returned without failure
- **WHEN** the orchestrator determines that the provider completed but lacks enough context to answer
- **THEN** the result is returned as `Outcome=Success` and `ResponseKind=Guidance`

#### Scenario: Degraded fallback answer is returned
- **WHEN** the orchestrator completes a general-mode fallback through Ollama
- **THEN** the result is returned as `Outcome=Degraded`, `ResponseKind=Answer`, and `ResponseSource=Fallback`

### Requirement: Session identity is derived from Discord conversation scope
The application layer SHALL derive the session key from the Discord user id plus thread id when a thread exists, otherwise from the Discord user id plus channel id.

#### Scenario: Thread-scoped conversation uses thread identity
- **WHEN** a user invokes the chat command inside a Discord thread
- **THEN** the session identity uses the user id and thread id rather than the parent channel id

#### Scenario: Channel-scoped conversation uses channel identity
- **WHEN** a user invokes the chat command outside a thread
- **THEN** the session identity uses the user id and channel id

### Requirement: Only successful primary answers advance the Dify conversation pointer
The application layer SHALL update persisted Dify conversation state only for successful primary answers. Guidance-only results and fallback results SHALL NOT overwrite the authoritative Dify conversation pointer in v1.

#### Scenario: Primary success updates conversation pointer
- **WHEN** the orchestrator returns `Success + Answer + Primary` with a provider conversation id
- **THEN** the system persists the returned Dify conversation pointer for the current session

#### Scenario: Fallback result does not update Dify conversation pointer
- **WHEN** the orchestrator returns `Degraded + Answer + Fallback`
- **THEN** the system leaves the persisted Dify conversation pointer unchanged