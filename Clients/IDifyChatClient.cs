using Discord_BOT.Models;
using Discord_BOT.Services;

namespace Discord_BOT.Clients;

public interface IDifyChatClient
{
    Task<ProviderExecutionResult> ExecuteAsync(ChatRequest request, ConversationState? conversationState, CancellationToken cancellationToken);
}