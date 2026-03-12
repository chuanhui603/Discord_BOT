using Discord_BOT.Models;

namespace Discord_BOT.Stores;

public interface IUserPreferenceStore
{
    Task<UserPreference> GetAsync(ulong userId, CancellationToken cancellationToken);

    Task SetShowDegradedWarningsAsync(ulong userId, bool enabled, CancellationToken cancellationToken);
}