using Discord_BOT.Clients;
using Discord_BOT.Models;
using Discord_BOT.Options;
using Discord_BOT.Services;
using Discord_BOT.Stores;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Json;
using System.Text;
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
    public async Task NoAnswer_UsesEnglishGuidance_WhenUserLocaleIsEnglish()
    {
        var difyClient = new FakeDifyChatClient([ProviderExecutionResult.Guidance(null)]);
        var ollamaClient = new FakeOllamaChatClient(ProviderExecutionResult.Success("fallback answer"));
        var orchestrator = CreateOrchestrator(difyClient, ollamaClient, out _);

        var result = await orchestrator.ExecuteAsync(CreateRequest(QueryMode.General, "en-US"), CancellationToken.None);

        Assert.Equal("I do not have enough reliable context to answer that yet. Please provide a more specific document name, clause, or keyword.", result.ResponseText);
        Assert.Equal("en-US", result.UserLocale);
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

    [Fact]
    public void Formatter_UsesLocalizedWarning_WhenLocaleIsEnglish()
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
            SessionUpdated: false,
            UserLocale: "en-US"), true);

        Assert.StartsWith("Warning: fallback mode is active", result);
        Assert.Contains("fallback answer", result);
    }

    [Fact]
    public async Task OllamaClient_IncludesLocaleInstructionInPrompt()
    {
        var handler = new CapturingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"response\":\"ok\"}", Encoding.UTF8, "application/json")
        });
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:11434/")
        };
        var client = new OllamaChatClient(
            httpClient,
            Microsoft.Extensions.Options.Options.Create(new OllamaOptions
            {
                GeneratePath = "api/generate",
                Model = "qwen2.5:7b-instruct",
                MaxOutputCharacters = 1200
            }),
            NullLogger<OllamaChatClient>.Instance);

        await client.ExecuteAsync(CreateRequest(QueryMode.General, "ja-JP"), CancellationToken.None);

        var payload = await handler.GetPayloadAsync();
        Assert.Contains("Respond in Japanese", payload);
        Assert.Contains("User locale: ja-JP", payload);
    }

    [Fact]
    public async Task DifyClient_SendsLocaleMetadataInInputs()
    {
        var handler = new CapturingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"answer\":\"ok\",\"conversation_id\":\"conv-1\"}", Encoding.UTF8, "application/json")
        });
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.dify.ai/v1/")
        };
        var client = new DifyChatClient(
            httpClient,
            Microsoft.Extensions.Options.Options.Create(new DifyOptions
            {
                ApiKey = "test-key",
                ChatPath = "chat-messages",
                UserPrefix = "discord"
            }),
            NullLogger<DifyChatClient>.Instance);

        await client.ExecuteAsync(CreateRequest(QueryMode.General, "en-US"), conversationState: null, CancellationToken.None);

        var payload = await handler.GetPayloadAsync();
        Assert.Contains("\"user_locale\":\"en-US\"", payload);
        Assert.Contains("\"response_language\":\"English (United States)\"", payload);
    }

    private static ChatRequest CreateRequest(QueryMode mode, string? userLocale = null)
    {
        return new ChatRequest(
            Prompt: "test prompt",
            UserId: 1,
            ChannelId: 100,
            ThreadId: null,
            QueryMode: mode,
            GuildId: 999,
            CorrelationId: "corr-1",
            UserLocale: userLocale,
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

    private sealed class CapturingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;
        private string? _payload;

        public CapturingHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        public Task<string> GetPayloadAsync()
        {
            return Task.FromResult(_payload ?? string.Empty);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _payload = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return _responseFactory(request);
        }
    }
}