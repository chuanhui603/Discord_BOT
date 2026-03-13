using Discord_BOT.Clients;
using Discord_BOT.Models;
using Discord_BOT.Options;
using Discord_BOT.Stores;
using Microsoft.Extensions.Options;

namespace Discord_BOT.Services;

public sealed class ChatOrchestrator : IChatOrchestrator
{
    private static readonly HashSet<ChatErrorClass> RetryableErrors =
    [
        ChatErrorClass.RateLimited,
        ChatErrorClass.Timeout,
        ChatErrorClass.TransientUpstream
    ];

    private static readonly HashSet<ChatErrorClass> FallbackEligibleErrors =
    [
        ChatErrorClass.RateLimited,
        ChatErrorClass.QuotaExceeded,
        ChatErrorClass.Timeout,
        ChatErrorClass.TransientUpstream
    ];

    private readonly IDifyChatClient _difyChatClient;
    private readonly IOllamaChatClient _ollamaChatClient;
    private readonly IConversationStateStore _conversationStateStore;
    private readonly ILogger<ChatOrchestrator> _logger;
    private readonly FallbackOptions _fallbackOptions;

    public ChatOrchestrator(
        IDifyChatClient difyChatClient,
        IOllamaChatClient ollamaChatClient,
        IConversationStateStore conversationStateStore,
        IOptions<FallbackOptions> fallbackOptions,
        ILogger<ChatOrchestrator> logger)
    {
        _difyChatClient = difyChatClient;
        _ollamaChatClient = ollamaChatClient;
        _conversationStateStore = conversationStateStore;
        _logger = logger;
        _fallbackOptions = fallbackOptions.Value;
    }

    public async Task<ChatResult> ExecuteAsync(ChatRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return CreateUnavailableResult(request, ChatErrorClass.BadRequest, ChatFailureStage.Orchestrator, null, false, 0);
        }

        var correlationId = string.IsNullOrWhiteSpace(request.CorrelationId)
            ? Guid.NewGuid().ToString("N")
            : request.CorrelationId;
        var normalizedRequest = request with
        {
            CorrelationId = correlationId,
            UserLocale = ChatLocaleHelper.NormalizeLocale(request.UserLocale)
        };
        var conversationScope = ConversationScope.FromRequest(normalizedRequest);
        var startedAt = DateTimeOffset.UtcNow;

        using var logScope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = correlationId,
            ["ConversationScope"] = conversationScope.ToString(),
            ["QueryMode"] = normalizedRequest.QueryMode.ToString()
        });

        var conversationState = await _conversationStateStore.GetAsync(conversationScope, cancellationToken);
        var primaryResult = await _difyChatClient.ExecuteAsync(normalizedRequest, conversationState, cancellationToken);
        _logger.LogInformation("Primary provider completed with response kind {ResponseKind}", primaryResult.ResponseKind);

        if (primaryResult.IsSuccess)
        {
            return await HandlePrimarySuccessAsync(normalizedRequest, conversationScope, primaryResult, startedAt, cancellationToken);
        }

        var primaryError = primaryResult.ErrorClass ?? ChatErrorClass.Internal;
        if (ShouldRetry(primaryError))
        {
            for (var retry = 0; retry < _fallbackOptions.PrimaryRetryCount; retry++)
            {
                _logger.LogInformation("Retrying primary provider due to {ErrorClass}. Attempt {Attempt}", primaryError, retry + 1);
                primaryResult = await _difyChatClient.ExecuteAsync(normalizedRequest, conversationState, cancellationToken);
                if (primaryResult.IsSuccess)
                {
                    return await HandlePrimarySuccessAsync(normalizedRequest, conversationScope, primaryResult, startedAt, cancellationToken);
                }

                primaryError = primaryResult.ErrorClass ?? ChatErrorClass.Internal;
            }
        }

        if (normalizedRequest.QueryMode == QueryMode.General && ShouldFallback(primaryError))
        {
            _logger.LogInformation("Falling back to Ollama due to {ErrorClass}", primaryError);
            var fallbackResult = await _ollamaChatClient.ExecuteAsync(normalizedRequest, cancellationToken);
            if (fallbackResult.IsSuccess)
            {
                return new ChatResult(
                    ChatOutcome.Degraded,
                    ChatResponseKind.Answer,
                    ChatResponseSource.Fallback,
                    fallbackResult.Text,
                    SessionUpdated: false,
                    UserLocale: normalizedRequest.UserLocale,
                    ErrorClass: primaryError,
                    FailureStage: ChatFailureStage.Fallback,
                    ProviderConversationId: fallbackResult.ProviderConversationId,
                    ProviderRequestId: fallbackResult.ProviderRequestId,
                    LatencyMs: GetElapsedMilliseconds(startedAt),
                    CorrelationId: correlationId);
            }
        }

        return CreateUnavailableResult(normalizedRequest, primaryError, ChatFailureStage.Primary, primaryResult.ProviderRequestId, false, GetElapsedMilliseconds(startedAt));
    }

    private async Task<ChatResult> HandlePrimarySuccessAsync(
        ChatRequest request,
        ConversationScope conversationScope,
        ProviderExecutionResult primaryResult,
        DateTimeOffset startedAt,
        CancellationToken cancellationToken)
    {
        var sessionUpdated = false;
        if (primaryResult.ResponseKind == ChatResponseKind.Answer && !string.IsNullOrWhiteSpace(primaryResult.ProviderConversationId))
        {
            await _conversationStateStore.UpsertAsync(
                conversationScope,
                new ConversationState(primaryResult.ProviderConversationId, DateTimeOffset.UtcNow),
                cancellationToken);
            sessionUpdated = true;
        }

        var responseText = primaryResult.ResponseKind == ChatResponseKind.Guidance && string.IsNullOrWhiteSpace(primaryResult.Text)
            ? ChatLocaleHelper.GetGuidanceMessage(request.UserLocale, _fallbackOptions)
            : primaryResult.Text;

        return new ChatResult(
            ChatOutcome.Success,
            primaryResult.ResponseKind,
            ChatResponseSource.Primary,
            responseText,
            sessionUpdated,
            UserLocale: request.UserLocale,
            ErrorClass: primaryResult.ErrorClass,
            FailureStage: null,
            ProviderConversationId: primaryResult.ProviderConversationId,
            ProviderRequestId: primaryResult.ProviderRequestId,
            LatencyMs: GetElapsedMilliseconds(startedAt),
            CorrelationId: request.CorrelationId);
    }

    private ChatResult CreateUnavailableResult(
        ChatRequest request,
        ChatErrorClass errorClass,
        ChatFailureStage failureStage,
        string? providerRequestId,
        bool sessionUpdated,
        long latencyMs)
    {
        return new ChatResult(
            ChatOutcome.Failed,
            ChatResponseKind.Unavailable,
            ChatResponseSource.System,
            ChatLocaleHelper.GetUnavailableMessage(request.UserLocale, _fallbackOptions),
            sessionUpdated,
            UserLocale: request.UserLocale,
            ErrorClass: errorClass,
            FailureStage: failureStage,
            ProviderConversationId: null,
            ProviderRequestId: providerRequestId,
            LatencyMs: latencyMs,
            CorrelationId: request.CorrelationId);
    }

    private static bool ShouldRetry(ChatErrorClass errorClass) => RetryableErrors.Contains(errorClass);

    private static bool ShouldFallback(ChatErrorClass errorClass) => FallbackEligibleErrors.Contains(errorClass);

    private static long GetElapsedMilliseconds(DateTimeOffset startedAt)
    {
        return (long)(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds;
    }
}