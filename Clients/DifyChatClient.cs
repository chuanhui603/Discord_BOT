using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Discord_BOT.Models;
using Discord_BOT.Options;
using Discord_BOT.Services;
using Microsoft.Extensions.Options;

namespace Discord_BOT.Clients;

public sealed class DifyChatClient : IDifyChatClient
{
    private readonly HttpClient _httpClient;
    private readonly DifyOptions _options;
    private readonly ILogger<DifyChatClient> _logger;

    public DifyChatClient(HttpClient httpClient, IOptions<DifyOptions> options, ILogger<DifyChatClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ProviderExecutionResult> ExecuteAsync(ChatRequest request, ConversationState? conversationState, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return ProviderExecutionResult.Failure(ChatErrorClass.AuthOrConfig);
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, _options.ChatPath)
        {
            Content = JsonContent.Create(new
            {
                inputs = new
                {
                    mode = request.QueryMode.ToString().ToLowerInvariant(),
                    guild_id = request.GuildId?.ToString(),
                    channel_id = request.ChannelId.ToString(),
                    thread_id = request.ThreadId?.ToString()
                },
                query = request.Prompt,
                response_mode = "blocking",
                conversation_id = conversationState?.DifyConversationId,
                user = $"{_options.UserPrefix}-{request.UserId}"
            })
        };

        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        try
        {
            using var response = await _httpClient.SendAsync(message, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
            var requestId = TryExtractJsonString(responseText, "message_id")
                ?? TryExtractJsonString(responseText, "task_id")
                ?? TryExtractJsonString(responseText, "request_id");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Dify request failed with status code {StatusCode}", (int)response.StatusCode);
                return ProviderExecutionResult.Failure(MapStatusCode(response.StatusCode), requestId);
            }

            var answer = ExtractAnswer(responseText);
            var conversationId = TryExtractJsonString(responseText, "conversation_id");

            if (string.IsNullOrWhiteSpace(answer) || ContainsNoAnswerMarker(answer))
            {
                return ProviderExecutionResult.Guidance(answer, requestId);
            }

            return ProviderExecutionResult.Success(answer, conversationId, requestId);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ProviderExecutionResult.Failure(ChatErrorClass.Timeout);
        }
        catch (HttpRequestException exception) when (exception.StatusCode.HasValue)
        {
            _logger.LogWarning(exception, "Dify HTTP request failed.");
            return ProviderExecutionResult.Failure(MapStatusCode(exception.StatusCode.Value));
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "Dify transient HTTP request failure.");
            return ProviderExecutionResult.Failure(ChatErrorClass.TransientUpstream);
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "Dify response could not be parsed.");
            return ProviderExecutionResult.Failure(ChatErrorClass.Internal);
        }
    }

    private bool ContainsNoAnswerMarker(string answer)
    {
        return _options.NoAnswerMarkers.Any(marker =>
            !string.IsNullOrWhiteSpace(marker) &&
            answer.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    private static ChatErrorClass MapStatusCode(HttpStatusCode statusCode)
    {
        return (int)statusCode switch
        {
            400 => ChatErrorClass.BadRequest,
            401 or 403 or 404 => ChatErrorClass.AuthOrConfig,
            402 => ChatErrorClass.QuotaExceeded,
            408 => ChatErrorClass.Timeout,
            429 => ChatErrorClass.RateLimited,
            >= 500 => ChatErrorClass.TransientUpstream,
            _ => ChatErrorClass.Internal
        };
    }

    private static string ExtractAnswer(string responseText)
    {
        return TryExtractJsonString(responseText, "answer")
            ?? TryExtractJsonString(responseText, "text")
            ?? string.Empty;
    }

    private static string? TryExtractJsonString(string json, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        using var document = JsonDocument.Parse(json);
        return TryExtractJsonString(document.RootElement, propertyName);
    }

    private static string? TryExtractJsonString(JsonElement element, string propertyName)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase) &&
                        property.Value.ValueKind == JsonValueKind.String)
                    {
                        return property.Value.GetString();
                    }

                    var nested = TryExtractJsonString(property.Value, propertyName);
                    if (nested is not null)
                    {
                        return nested;
                    }
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    var nested = TryExtractJsonString(item, propertyName);
                    if (nested is not null)
                    {
                        return nested;
                    }
                }
                break;
        }

        return null;
    }
}