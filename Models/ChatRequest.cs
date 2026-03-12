namespace Discord_BOT.Models;

public sealed record ChatRequest(
    string Prompt,
    ulong UserId,
    ulong ChannelId,
    ulong? ThreadId,
    QueryMode QueryMode,
    ulong? GuildId = null,
    string? CorrelationId = null,
    string? UserLocale = null,
    string? CommandName = null);