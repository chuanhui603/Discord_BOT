namespace Discord_BOT.Options;

public sealed class DiscordBotOptions
{
    public const string SectionName = "DiscordBot";

    public string Token { get; set; } = string.Empty;

    public ulong? DevelopmentGuildId { get; set; }
}