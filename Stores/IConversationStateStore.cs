using Discord_BOT.Models;

namespace Discord_BOT.Stores;

public interface IConversationStateStore
{
    Task<ConversationState?> GetAsync(ConversationScope scope, CancellationToken cancellationToken);

    Task UpsertAsync(ConversationScope scope, ConversationState state, CancellationToken cancellationToken);
}