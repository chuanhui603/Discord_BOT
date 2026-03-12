namespace Discord_BOT.Options;

public sealed class FallbackOptions
{
    public const string SectionName = "Fallback";

    public int PrimaryRetryCount { get; set; } = 1;

    public int DifyTimeoutSeconds { get; set; } = 20;

    public int OllamaTimeoutSeconds { get; set; } = 35;

    public string GuidanceMessage { get; set; } = "目前沒有足夠依據可以可靠回答，請提供更具體的文件名、條文或關鍵字。";

    public string UnavailableMessage { get; set; } = "主要服務暫時無法使用，請稍後再試。";

    public string DegradedWarningMessage { get; set; } = "警告：目前使用備援模式，以下回覆可能較簡化，且不一定依據知識庫內容。";
}