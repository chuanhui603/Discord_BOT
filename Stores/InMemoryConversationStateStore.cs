using System.Collections.Concurrent;
using Discord_BOT.Models;

namespace Discord_BOT.Stores;

public sealed class InMemoryConversationStateStore : IConversationStateStore
{
    private readonly ConcurrentDictionary<ConversationScope, ConversationState> _states = new();

    public Task<ConversationState?> GetAsync(ConversationScope scope, CancellationToken cancellationToken)
    {
        _states.TryGetValue(scope, out var state);
        return Task.FromResult(state);
    }

    public Task UpsertAsync(ConversationScope scope, ConversationState state, CancellationToken cancellationToken)
    {
        _states[scope] = state;
        return Task.CompletedTask;
    }
}