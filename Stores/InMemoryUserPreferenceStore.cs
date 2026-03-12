using System.Collections.Concurrent;
using Discord_BOT.Models;

namespace Discord_BOT.Stores;

public sealed class InMemoryUserPreferenceStore : IUserPreferenceStore
{
    private readonly ConcurrentDictionary<ulong, UserPreference> _preferences = new();

    public Task<UserPreference> GetAsync(ulong userId, CancellationToken cancellationToken)
    {
        var preference = _preferences.GetOrAdd(userId, _ => new UserPreference(ShowDegradedWarnings: true));
        return Task.FromResult(preference);
    }

    public Task SetShowDegradedWarningsAsync(ulong userId, bool enabled, CancellationToken cancellationToken)
    {
        _preferences[userId] = new UserPreference(enabled);
        return Task.CompletedTask;
    }
}