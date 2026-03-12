namespace Discord_BOT.Models;

public readonly record struct ConversationScope(ulong UserId, ulong ScopeId, bool IsThread)
{
    public static ConversationScope FromRequest(ChatRequest request)
    {
        return request.ThreadId.HasValue
            ? new ConversationScope(request.UserId, request.ThreadId.Value, true)
            : new ConversationScope(request.UserId, request.ChannelId, false);
    }

    public override string ToString()
    {
        var scopeType = IsThread ? "thread" : "channel";
        return $"{UserId}:{scopeType}:{ScopeId}";
    }
}