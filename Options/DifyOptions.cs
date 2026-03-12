namespace Discord_BOT.Options;

public sealed class DifyOptions
{
    public const string SectionName = "Dify";

    public string BaseUrl { get; set; } = "https://api.dify.ai/v1/";

    public string ApiKey { get; set; } = string.Empty;

    public string ChatPath { get; set; } = "chat-messages";

    public string UserPrefix { get; set; } = "discord";

    public string[] NoAnswerMarkers { get; set; } = [];
}