using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Discord_BOT.Models;
using Discord_BOT.Services;
using Discord_BOT.Stores;

namespace Discord_BOT.Commands;

public sealed class ChatModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IChatOrchestrator _chatOrchestrator;
    private readonly IUserPreferenceStore _userPreferenceStore;
    private readonly IDiscordResponseFormatter _responseFormatter;

    public ChatModule(
        IChatOrchestrator chatOrchestrator,
        IUserPreferenceStore userPreferenceStore,
        IDiscordResponseFormatter responseFormatter)
    {
        _chatOrchestrator = chatOrchestrator;
        _userPreferenceStore = userPreferenceStore;
        _responseFormatter = responseFormatter;
    }

    [SlashCommand("chat", "Send a chat request to the bot.")]
    public async Task ChatAsync(
        [Summary("mode", "general or knowledge")] [Choice("general", "general")] [Choice("knowledge", "knowledge")] string mode,
        [Summary("prompt", "The question to send to the bot.")] string prompt)
    {
        await DeferAsync();

        var queryMode = mode.Equals("knowledge", StringComparison.OrdinalIgnoreCase)
            ? QueryMode.Knowledge
            : QueryMode.General;
        ulong? threadId = Context.Channel is SocketThreadChannel threadChannel ? threadChannel.Id : null;
        var request = new ChatRequest(
            Prompt: prompt,
            UserId: Context.User.Id,
            ChannelId: Context.Channel.Id,
            ThreadId: threadId,
            QueryMode: queryMode,
            GuildId: Context.Guild?.Id,
            CorrelationId: Guid.NewGuid().ToString("N"),
            UserLocale: Context.Interaction.UserLocale,
            CommandName: "/chat");

        var result = await _chatOrchestrator.ExecuteAsync(request, CancellationToken.None);
        var preference = await _userPreferenceStore.GetAsync(Context.User.Id, CancellationToken.None);
        var message = _responseFormatter.Format(result, preference.ShowDegradedWarnings);

        await FollowupAsync(message, allowedMentions: AllowedMentions.None);
    }

    [SlashCommand("checkon", "Show degraded fallback warnings in future replies.")]
    public async Task CheckOnAsync()
    {
        await _userPreferenceStore.SetShowDegradedWarningsAsync(Context.User.Id, true, CancellationToken.None);
        await RespondAsync("已啟用備援模式警告提示。", ephemeral: true);
    }

    [SlashCommand("checkoff", "Hide degraded fallback warnings in future replies.")]
    public async Task CheckOffAsync()
    {
        await _userPreferenceStore.SetShowDegradedWarningsAsync(Context.User.Id, false, CancellationToken.None);
        await RespondAsync("已停用備援模式警告提示。", ephemeral: true);
    }
}