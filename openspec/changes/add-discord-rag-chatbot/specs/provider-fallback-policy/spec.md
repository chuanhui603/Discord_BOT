## ADDED Requirements

### Requirement: Dify is the primary provider for all chat requests
The system SHALL send all `general` and `knowledge` chat requests to Dify as the primary provider before any fallback decision is made.

#### Scenario: General request starts with Dify
- **WHEN** a user submits a `general` chat request
- **THEN** the system attempts Dify before evaluating any fallback path

#### Scenario: Knowledge request starts with Dify
- **WHEN** a user submits a `knowledge` chat request
- **THEN** the system attempts Dify before evaluating any fallback path

### Requirement: Only eligible general-mode failures may fall back to Ollama
The system SHALL allow Ollama fallback only for `general` requests whose final Dify failure class is `RateLimited`, `QuotaExceeded`, `Timeout`, or `TransientUpstream`.

#### Scenario: General request falls back after retryable timeout
- **WHEN** a `general` request encounters a Dify timeout, retries once, and the retry still times out
- **THEN** the system attempts an Ollama fallback

#### Scenario: General request falls back after quota exhaustion
- **WHEN** a `general` request encounters a Dify quota exhaustion failure
- **THEN** the system attempts an Ollama fallback without retrying Dify repeatedly

### Requirement: Knowledge-mode requests shall not degrade to Ollama
The system SHALL NOT use Ollama as a fallback provider for `knowledge` requests.

#### Scenario: Knowledge request fails due to provider timeout
- **WHEN** a `knowledge` request encounters a Dify timeout and remains unsuccessful after one retry
- **THEN** the system returns temporary unavailability instead of a fallback answer

#### Scenario: Knowledge request fails due to rate limiting
- **WHEN** a `knowledge` request encounters Dify rate limiting and remains unsuccessful after one retry
- **THEN** the system returns temporary unavailability instead of a fallback answer

### Requirement: No-answer outcomes produce guidance instead of fallback
If Dify completes a request but indicates there is not enough grounded context to answer, the system SHALL return a guidance response and SHALL NOT attempt Ollama fallback.

#### Scenario: General request lacks enough context
- **WHEN** Dify returns a no-answer or no-context outcome for a `general` request
- **THEN** the system returns `Success + Guidance` and does not invoke Ollama

#### Scenario: Knowledge request lacks enough context
- **WHEN** Dify returns a no-answer or no-context outcome for a `knowledge` request
- **THEN** the system returns `Success + Guidance` and does not invoke Ollama

### Requirement: External failures are normalized before rendering
The system SHALL normalize provider-specific failures into internal error classes before deciding retry, fallback, or final Discord rendering.

#### Scenario: Provider bad request is normalized but shown as unavailable
- **WHEN** Dify returns a bad-request failure that maps to internal `BadRequest`
- **THEN** the system records the internal error class and renders a user-facing unavailable response

#### Scenario: Provider configuration failure is normalized but shown as unavailable
- **WHEN** Dify returns an authentication, authorization, or missing-resource failure that maps to internal `AuthOrConfig`
- **THEN** the system records the internal error class and renders a user-facing unavailable response