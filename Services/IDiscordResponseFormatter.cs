using Discord_BOT.Models;

namespace Discord_BOT.Services;

public interface IDiscordResponseFormatter
{
    string Format(ChatResult result, bool showDegradedWarning);
}