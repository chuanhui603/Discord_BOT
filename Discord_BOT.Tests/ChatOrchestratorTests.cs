using Discord_BOT.Clients;
using Discord_BOT.Models;
using Discord_BOT.Options;
using Discord_BOT.Services;
using Discord_BOT.Stores;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Discord_BOT.Tests;

public sealed class ChatOrchestratorTests
{
    [Fact]
    public async Task GeneralMode_FallsBackToOllama_AfterRetryablePrimaryFailures()
    {
        var difyClient = new FakeDifyChatClient(
        [
            ProviderExecutionResult.Failure(ChatErrorClass.Timeout),
            ProviderExecutionResult.Failure(ChatErrorClass.Timeout)
        ]);
        var ollamaClient = new FakeOllamaChatClient(ProviderExecutionResult.Success("fallback answer"));
        var orchestrator = CreateOrchestrator(difyClient, ollamaClient, out _);

        var result = await orchestrator.ExecuteAsync(CreateRequest(QueryMode.General), CancellationToken.None);

        Assert.Equal(ChatOutcome.Degraded, result.Outcome);
        Assert.Equal(ChatResponseKind.Answer, result.ResponseKind);
        Assert.Equal(ChatResponseSource.Fallback, result.ResponseSource);
        Assert.Equal(ChatErrorClass.Timeout, result.ErrorClass);
        Assert.False(result.SessionUpdated);
        Assert.Equal(2, difyClient.CallCount);
        Assert.Equal(1, ollamaClient.CallCount);
    }

    [Fact]
    public async Task KnowledgeMode_DoesNotFallback_WhenPrimaryFails()
    {
        var difyClient = new FakeDifyChatClient(
        [
            ProviderExecutionResult.Failure(ChatErrorClass.Timeout),
            ProviderExecutionResult.Failure(ChatErrorClass.Timeout)
        ]);
        var ollamaClient = new FakeOllamaChatClient(ProviderExecutionResult.Success("fallback answer"));
        var orchestrator = CreateOrchestrator(difyClient, ollamaClient, out _);

        var result = await orchestrator.ExecuteAsync(CreateRequest(QueryMode.Knowledge), CancellationToken.None);

        Assert.Equal(ChatOutcome.Failed, result.Outcome);
        Assert.Equal(ChatResponseKind.Unavailable, result.ResponseKind);
        Assert.Equal(ChatResponseSource.System, result.ResponseSource);
        Assert.Equal(ChatErrorClass.Timeout, result.ErrorClass);
        Assert.Equal(0, ollamaClient.CallCount);
    }

    [Fact]
    public async Task NoAnswer_ReturnsGuidance_WithoutFallback()
    {
        var difyClient = new FakeDifyChatClient([ProviderExecutionResult.Guidance(null)]);
        var ollamaClient = new FakeOllamaChatClient(ProviderExecutionResult.Success("fallback answer"));
        var orchestrator = CreateOrchestrator(difyClient, ollamaClient, out _);

        var result = await orchestrator.ExecuteAsync(CreateRequest(QueryMode.General), CancellationToken.None);

        Assert.Equal(ChatOutcome.Success, result.Outcome);
        Assert.Equal(ChatResponseKind.Guidance, result.ResponseKind);
        Assert.Equal(ChatResponseSource.Primary, result.ResponseSource);
        Assert.Equal(0, ollamaClient.CallCount);
        Assert.False(string.IsNullOrWhiteSpace(result.ResponseText));
    }

    [Fact]
    public async Task PrimarySuccess_UpdatesConversationPointer()
    {
        var difyClient = new FakeDifyChatClient([ProviderExecutionResult.Success("answer", "conv-123")]);
        var ollamaClient = new FakeOllamaChatClient(ProviderExecutionResult.Success("fallback answer"));
        var orchestrator = CreateOrchestrator(difyClient, ollamaClient, out var store);
        var request = CreateRequest(QueryMode.General);

        var result = await orchestrator.ExecuteAsync(request, CancellationToken.None);
        var storedState = await store.GetAsync(ConversationScope.FromRequest(request), CancellationToken.None);

        Assert.Equal(ChatOutcome.Success, result.Outcome);
        Assert.True(result.SessionUpdated);
        Assert.NotNull(storedState);
        Assert.Equal("conv-123", storedState!.DifyConversationId);
    }

    [Fact]
    public void Formatter_PrependsWarning_WhenDegradedWarningsEnabled()
    {
        var formatter = new DiscordResponseFormatter(Microsoft.Extensions.Options.Options.Create(new FallbackOptions
        {
            DegradedWarningMessage = "warning",
            UnavailableMessage = "unavailable"
        }));

        var result = formatter.Format(new ChatResult(
            ChatOutcome.Degraded,
            ChatResponseKind.Answer,
            ChatResponseSource.Fallback,
            "fallback answer",
            SessionUpdated: false), true);

        Assert.StartsWith("warning", result);
        Assert.Contains("fallback answer", result);
    }

    private static ChatRequest CreateRequest(QueryMode mode)
    {
        return new ChatRequest(
            Prompt: "test prompt",
            UserId: 1,
            ChannelId: 100,
            ThreadId: null,
            QueryMode: mode,
            GuildId: 999,
            CorrelationId: "corr-1",
            CommandName: "/chat");
    }

    private static ChatOrchestrator CreateOrchestrator(
        FakeDifyChatClient difyClient,
        FakeOllamaChatClient ollamaClient,
        out InMemoryConversationStateStore store)
    {
        store = new InMemoryConversationStateStore();
        return new ChatOrchestrator(
            difyClient,
            ollamaClient,
            store,
            Microsoft.Extensions.Options.Options.Create(new FallbackOptions
            {
                PrimaryRetryCount = 1,
                GuidanceMessage = "guidance",
                UnavailableMessage = "unavailable"
            }),
            NullLogger<ChatOrchestrator>.Instance);
    }

    private sealed class FakeDifyChatClient : IDifyChatClient
    {
        private readonly Queue<ProviderExecutionResult> _results;

        public FakeDifyChatClient(IEnumerable<ProviderExecutionResult> results)
        {
            _results = new Queue<ProviderExecutionResult>(results);
        }

        public int CallCount { get; private set; }

        public Task<ProviderExecutionResult> ExecuteAsync(ChatRequest request, ConversationState? conversationState, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(_results.Count > 0 ? _results.Dequeue() : ProviderExecutionResult.Failure(ChatErrorClass.Internal));
        }
    }

    private sealed class FakeOllamaChatClient : IOllamaChatClient
    {
        private readonly ProviderExecutionResult _result;

        public FakeOllamaChatClient(ProviderExecutionResult result)
        {
            _result = result;
        }

        public int CallCount { get; private set; }

        public Task<ProviderExecutionResult> ExecuteAsync(ChatRequest request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(_result);
        }
    }
}