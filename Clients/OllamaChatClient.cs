using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Discord_BOT.Models;
using Discord_BOT.Options;
using Discord_BOT.Services;
using Microsoft.Extensions.Options;

namespace Discord_BOT.Clients;

public sealed class OllamaChatClient : IOllamaChatClient
{
    private readonly HttpClient _httpClient;
    private readonly OllamaOptions _options;
    private readonly ILogger<OllamaChatClient> _logger;

    public OllamaChatClient(HttpClient httpClient, IOptions<OllamaOptions> options, ILogger<OllamaChatClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ProviderExecutionResult> ExecuteAsync(ChatRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.Model))
        {
            return ProviderExecutionResult.Failure(ChatErrorClass.AuthOrConfig);
        }

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(_options.GeneratePath, new
            {
                model = _options.Model,
                prompt = BuildPrompt(request),
                stream = false
            }, cancellationToken);

            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ProviderExecutionResult.Failure(MapStatusCode(response.StatusCode));
            }

            var text = TryExtractJsonString(payload, "response");
            if (string.IsNullOrWhiteSpace(text))
            {
                return ProviderExecutionResult.Failure(ChatErrorClass.Internal);
            }

            if (text.Length > _options.MaxOutputCharacters)
            {
                text = text[.._options.MaxOutputCharacters];
            }

            return ProviderExecutionResult.Success(text.Trim());
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ProviderExecutionResult.Failure(ChatErrorClass.Timeout);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "Ollama request failed.");
            return ProviderExecutionResult.Failure(ChatErrorClass.TransientUpstream);
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "Ollama response could not be parsed.");
            return ProviderExecutionResult.Failure(ChatErrorClass.Internal);
        }
    }

    private static string BuildPrompt(ChatRequest request)
    {
        return $"You are a concise Discord assistant. Answer briefly and clearly.\nMode: {request.QueryMode}\nUser question: {request.Prompt}";
    }

    private static ChatErrorClass MapStatusCode(HttpStatusCode statusCode)
    {
        return (int)statusCode switch
        {
            400 => ChatErrorClass.BadRequest,
            401 or 403 or 404 => ChatErrorClass.AuthOrConfig,
            408 => ChatErrorClass.Timeout,
            429 => ChatErrorClass.RateLimited,
            >= 500 => ChatErrorClass.TransientUpstream,
            _ => ChatErrorClass.Internal
        };
    }

    private static string? TryExtractJsonString(string json, string propertyName)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        return null;
    }
}