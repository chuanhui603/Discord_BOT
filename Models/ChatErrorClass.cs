namespace Discord_BOT.Models;

public enum ChatErrorClass
{
    RateLimited = 0,
    QuotaExceeded = 1,
    Timeout = 2,
    TransientUpstream = 3,
    NoAnswerOrNoContext = 4,
    BadRequest = 5,
    AuthOrConfig = 6,
    Internal = 7
}