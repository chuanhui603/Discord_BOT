using Discord_BOT.Models;
using Discord_BOT.Services;

namespace Discord_BOT.Clients;

public interface IOllamaChatClient
{
    Task<ProviderExecutionResult> ExecuteAsync(ChatRequest request, CancellationToken cancellationToken);
}