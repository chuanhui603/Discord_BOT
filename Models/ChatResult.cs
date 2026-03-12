namespace Discord_BOT.Models;

public sealed record ChatResult(
    ChatOutcome Outcome,
    ChatResponseKind ResponseKind,
    ChatResponseSource ResponseSource,
    string? ResponseText,
    bool SessionUpdated,
    ChatErrorClass? ErrorClass = null,
    ChatFailureStage? FailureStage = null,
    string? ProviderConversationId = null,
    string? ProviderRequestId = null,
    long? LatencyMs = null,
    string? CorrelationId = null);