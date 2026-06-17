using WorkDemoServer.Contracts;

namespace WorkDemoServer.Services;

public enum OnlineInviteErrorCode
{
    None,
    Validation,
    AccountNotFound,
    NotFriends,
    TargetOffline
}

public sealed class OnlineInviteResult
{
    private OnlineInviteResult(
        bool isSuccess,
        OnlineInviteErrorCode errorCode,
        string errorMessage,
        OnlineInviteResponse? invite)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
        Invite = invite;
    }

    public bool IsSuccess { get; }

    public OnlineInviteErrorCode ErrorCode { get; }

    public string ErrorMessage { get; }

    public OnlineInviteResponse? Invite { get; }

    public static OnlineInviteResult Success(OnlineInviteResponse invite)
    {
        return new OnlineInviteResult(true, OnlineInviteErrorCode.None, string.Empty, invite);
    }

    public static OnlineInviteResult Fail(OnlineInviteErrorCode errorCode, string errorMessage)
    {
        return new OnlineInviteResult(false, errorCode, errorMessage, null);
    }
}
