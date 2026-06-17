using WorkDemoServer.Contracts;

namespace WorkDemoServer.Services;

public enum AccountErrorCode
{
    None,
    Validation,
    AccountAlreadyExists,
    AccountNotFound,
    WrongPassword
}

public sealed class AccountResult
{
    private AccountResult(
        bool isSuccess,
        AccountProfileResponse? profile,
        AccountErrorCode errorCode,
        string errorMessage)
    {
        IsSuccess = isSuccess;
        Profile = profile;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess { get; }

    public AccountProfileResponse? Profile { get; }

    public AccountErrorCode ErrorCode { get; }

    public string ErrorMessage { get; }

    public static AccountResult Success(AccountProfileResponse profile)
    {
        return new AccountResult(true, profile, AccountErrorCode.None, string.Empty);
    }

    public static AccountResult Fail(AccountErrorCode errorCode, string errorMessage)
    {
        return new AccountResult(false, null, errorCode, errorMessage);
    }
}
