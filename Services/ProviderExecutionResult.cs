using Discord_BOT.Models;

namespace Discord_BOT.Services;

public sealed record ProviderExecutionResult(
    bool IsSuccess,
    ChatResponseKind ResponseKind,
    string? Text,
    string? ProviderConversationId = null,
    string? ProviderRequestId = null,
    ChatErrorClass? ErrorClass = null)
{
    public static ProviderExecutionResult Success(string text, string? conversationId = null, string? requestId = null)
        => new(true, ChatResponseKind.Answer, text, conversationId, requestId);

    public static ProviderExecutionResult Guidance(string? text, string? requestId = null)
        => new(true, ChatResponseKind.Guidance, text, null, requestId, ChatErrorClass.NoAnswerOrNoContext);

    public static ProviderExecutionResult Failure(ChatErrorClass errorClass, string? requestId = null)
        => new(false, ChatResponseKind.Unavailable, null, null, requestId, errorClass);
}