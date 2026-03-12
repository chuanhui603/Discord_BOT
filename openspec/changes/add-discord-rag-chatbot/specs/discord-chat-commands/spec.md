## ADDED Requirements

### Requirement: Chat command requires explicit query mode
The system SHALL expose a `/chat` slash command that requires a `mode` option and a user question. The `mode` option SHALL support `general` and `knowledge` values, and the selected value SHALL determine downstream orchestration policy.

#### Scenario: User submits a general chat request
- **WHEN** a user runs `/chat` with `mode=general` and a non-empty prompt
- **THEN** the system routes the request into the general chat orchestration path

#### Scenario: User submits a knowledge-grounded request
- **WHEN** a user runs `/chat` with `mode=knowledge` and a non-empty prompt
- **THEN** the system routes the request into the knowledge chat orchestration path

### Requirement: Chat command returns responses based on orchestration result kind
The system SHALL render Discord responses according to the final orchestration result classification.

#### Scenario: Command returns a normal answer
- **WHEN** the orchestrator returns `Outcome=Success` and `ResponseKind=Answer`
- **THEN** the system returns the answer as the primary command response

#### Scenario: Command returns guidance instead of an answer
- **WHEN** the orchestrator returns `Outcome=Success` and `ResponseKind=Guidance`
- **THEN** the system returns an actionable guidance message telling the user how to refine the request

#### Scenario: Command returns temporary unavailability
- **WHEN** the orchestrator returns `Outcome=Failed` and `ResponseKind=Unavailable`
- **THEN** the system returns a temporary-unavailability message instead of fabricating an answer

### Requirement: Users can control degraded warning display
The system SHALL provide `/checkon` and `/checkoff` commands to enable or disable degraded fallback warnings for that user's future Discord replies.

#### Scenario: User enables degraded warnings
- **WHEN** a user runs `/checkon`
- **THEN** the system persists a preference indicating degraded warnings SHALL be shown for that user

#### Scenario: User disables degraded warnings
- **WHEN** a user runs `/checkoff`
- **THEN** the system persists a preference indicating degraded warnings SHALL be hidden for that user

#### Scenario: Degraded reply shows warning when enabled
- **WHEN** a user with degraded warnings enabled receives a `Degraded + Answer` result
- **THEN** the Discord reply includes a clear warning that the answer came from a fallback mode and may be simplified or not grounded in the knowledge base

#### Scenario: Degraded reply hides warning when disabled
- **WHEN** a user with degraded warnings disabled receives a `Degraded + Answer` result
- **THEN** the Discord reply omits the warning banner while still returning the fallback answer