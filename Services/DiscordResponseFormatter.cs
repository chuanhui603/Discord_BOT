using Discord_BOT.Models;
using Discord_BOT.Options;
using Microsoft.Extensions.Options;

namespace Discord_BOT.Services;

public sealed class DiscordResponseFormatter : IDiscordResponseFormatter
{
    private const int MaxDiscordMessageLength = 1900;
    private readonly FallbackOptions _options;

    public DiscordResponseFormatter(IOptions<FallbackOptions> options)
    {
        _options = options.Value;
    }

    public string Format(ChatResult result, bool showDegradedWarning)
    {
        var message = result.ResponseText ?? _options.UnavailableMessage;

        if (result.Outcome == ChatOutcome.Degraded && showDegradedWarning)
        {
            message = $"{_options.DegradedWarningMessage}\n\n{message}";
        }

        return message.Length <= MaxDiscordMessageLength
            ? message
            : message[..MaxDiscordMessageLength];
    }
}