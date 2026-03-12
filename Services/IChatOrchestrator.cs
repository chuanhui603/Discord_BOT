using Discord_BOT.Models;

namespace Discord_BOT.Services;

public interface IChatOrchestrator
{
    Task<ChatResult> ExecuteAsync(ChatRequest request, CancellationToken cancellationToken);
}