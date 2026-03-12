using System.Reflection;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Discord_BOT.Options;
using Microsoft.Extensions.Options;

namespace Discord_BOT.Services;

public sealed class DiscordBotService : BackgroundService
{
    private readonly DiscordSocketClient _discordClient;
    private readonly InteractionService _interactionService;
    private readonly IServiceProvider _serviceProvider;
    private readonly DiscordBotOptions _options;
    private readonly ILogger<DiscordBotService> _logger;
    private bool _commandsRegistered;

    public DiscordBotService(
        DiscordSocketClient discordClient,
        InteractionService interactionService,
        IServiceProvider serviceProvider,
        IOptions<DiscordBotOptions> options,
        ILogger<DiscordBotService> logger)
    {
        _discordClient = discordClient;
        _interactionService = interactionService;
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_options.Token))
        {
            _logger.LogWarning("Discord token is not configured. Bot runtime is disabled until DiscordBot:Token is provided.");
            return;
        }

        _discordClient.Log += OnDiscordLogAsync;
        _interactionService.Log += OnDiscordLogAsync;
        _discordClient.Ready += OnReadyAsync;
        _discordClient.InteractionCreated += OnInteractionCreatedAsync;

        await _interactionService.AddModulesAsync(Assembly.GetExecutingAssembly(), _serviceProvider);
        await _discordClient.LoginAsync(TokenType.Bot, _options.Token);
        await _discordClient.StartAsync();

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Host shutdown.
        }
        finally
        {
            await _discordClient.StopAsync();
            await _discordClient.LogoutAsync();
        }
    }

    private Task OnDiscordLogAsync(LogMessage message)
    {
        var level = message.Severity switch
        {
            LogSeverity.Critical => LogLevel.Critical,
            LogSeverity.Error => LogLevel.Error,
            LogSeverity.Warning => LogLevel.Warning,
            LogSeverity.Info => LogLevel.Information,
            LogSeverity.Verbose => LogLevel.Debug,
            LogSeverity.Debug => LogLevel.Trace,
            _ => LogLevel.Information
        };
        _logger.Log(level, message.Exception, "Discord: {Message}", message.Message);
        return Task.CompletedTask;
    }

    private async Task OnReadyAsync()
    {
        if (_commandsRegistered)
        {
            return;
        }

        if (_options.DevelopmentGuildId.HasValue)
        {
            await _interactionService.RegisterCommandsToGuildAsync(_options.DevelopmentGuildId.Value, true);
            _logger.LogInformation("Registered slash commands to guild {GuildId}", _options.DevelopmentGuildId.Value);
        }
        else
        {
            await _interactionService.RegisterCommandsGloballyAsync(true);
            _logger.LogInformation("Registered slash commands globally.");
        }

        _commandsRegistered = true;
    }

    private async Task OnInteractionCreatedAsync(SocketInteraction interaction)
    {
        try
        {
            var context = new SocketInteractionContext(_discordClient, interaction);
            var result = await _interactionService.ExecuteCommandAsync(context, _serviceProvider);
            if (!result.IsSuccess && interaction.Type is InteractionType.ApplicationCommand)
            {
                _logger.LogWarning("Interaction command execution failed: {Reason}", result.ErrorReason);
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unhandled Discord interaction execution failure.");
            if (!interaction.HasResponded)
            {
                await interaction.RespondAsync("主要服務暫時無法使用，請稍後再試。", ephemeral: true);
            }
        }
    }
}