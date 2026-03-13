using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Discord_BOT.Clients;
using Discord_BOT.Options;
using Discord_BOT.Services;
using Discord_BOT.Stores;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<DiscordBotOptions>(builder.Configuration.GetSection(DiscordBotOptions.SectionName));
builder.Services.Configure<DifyOptions>(builder.Configuration.GetSection(DifyOptions.SectionName));
builder.Services.Configure<OllamaOptions>(builder.Configuration.GetSection(OllamaOptions.SectionName));
builder.Services.Configure<FallbackOptions>(builder.Configuration.GetSection(FallbackOptions.SectionName));

builder.Services.AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
{
	GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.DirectMessages,
	AlwaysDownloadUsers = false,
	UseInteractionSnowflakeDate = false
}));
builder.Services.AddSingleton(provider => new InteractionService(
	provider.GetRequiredService<DiscordSocketClient>(),
	new InteractionServiceConfig
	{
		UseCompiledLambda = true,
		DefaultRunMode = RunMode.Async,
		LogLevel = LogSeverity.Info
	}));

builder.Services.AddHttpClient<IDifyChatClient, DifyChatClient>((provider, client) =>
{
	var options = provider.GetRequiredService<IOptions<DifyOptions>>().Value;
	if (Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseAddress))
	{
		client.BaseAddress = baseAddress;
	}
	client.Timeout = TimeSpan.FromSeconds(provider.GetRequiredService<IOptions<FallbackOptions>>().Value.DifyTimeoutSeconds);
	client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
});
builder.Services.AddHttpClient<IOllamaChatClient, OllamaChatClient>((provider, client) =>
{
	var options = provider.GetRequiredService<IOptions<OllamaOptions>>().Value;
	if (Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseAddress))
	{
		client.BaseAddress = baseAddress;
	}
	client.Timeout = TimeSpan.FromSeconds(provider.GetRequiredService<IOptions<FallbackOptions>>().Value.OllamaTimeoutSeconds);
	client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
});

builder.Services.AddSingleton<IConversationStateStore, InMemoryConversationStateStore>();
builder.Services.AddSingleton<IUserPreferenceStore, InMemoryUserPreferenceStore>();
builder.Services.AddSingleton<IChatOrchestrator, ChatOrchestrator>();
builder.Services.AddSingleton<IDiscordResponseFormatter, DiscordResponseFormatter>();
builder.Services.AddHostedService<DiscordBotService>();

var host = builder.Build();
host.Run();
