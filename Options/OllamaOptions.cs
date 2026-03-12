namespace Discord_BOT.Options;

public sealed class OllamaOptions
{
    public const string SectionName = "Ollama";

    public string BaseUrl { get; set; } = "http://localhost:11434/";

    public string GeneratePath { get; set; } = "api/generate";

    public string Model { get; set; } = "qwen2.5:7b-instruct";

    public int MaxOutputCharacters { get; set; } = 1200;
}