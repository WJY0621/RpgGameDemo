namespace WorkDemoServer.Services;

public enum ChatErrorCode
{
    None,
    Validation,
    AccountNotFound,
    NotFriends
}

public sealed class ChatResult
{
    private ChatResult(bool isSuccess, ChatErrorCode errorCode, string errorMessage)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess { get; }

    public ChatErrorCode ErrorCode { get; }

    public string ErrorMessage { get; }

    public static ChatResult Success()
    {
        return new ChatResult(true, ChatErrorCode.None, string.Empty);
    }

    public static ChatResult Fail(ChatErrorCode errorCode, string errorMessage)
    {
        return new ChatResult(false, errorCode, errorMessage);
    }
}
