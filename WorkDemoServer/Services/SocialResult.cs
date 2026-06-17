using WorkDemoServer.Contracts;

namespace WorkDemoServer.Services;

public enum SocialErrorCode
{
    None,
    Validation,
    AccountNotFound,
    AlreadyFriends,
    RequestAlreadyExists,
    RequestNotFound
}

public sealed class SocialResult
{
    private SocialResult(
        bool isSuccess,
        SocialErrorCode errorCode,
        string errorMessage)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess { get; }

    public SocialErrorCode ErrorCode { get; }

    public string ErrorMessage { get; }

    public static SocialResult Success()
    {
        return new SocialResult(true, SocialErrorCode.None, string.Empty);
    }

    public static SocialResult Fail(SocialErrorCode errorCode, string errorMessage)
    {
        return new SocialResult(false, errorCode, errorMessage);
    }
}

public sealed class SocialDataResult<T>
{
    private SocialDataResult(
        bool isSuccess,
        T? data,
        SocialErrorCode errorCode,
        string errorMessage)
    {
        IsSuccess = isSuccess;
        Data = data;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess { get; }

    public T? Data { get; }

    public SocialErrorCode ErrorCode { get; }

    public string ErrorMessage { get; }

    public static SocialDataResult<T> Success(T data)
    {
        return new SocialDataResult<T>(true, data, SocialErrorCode.None, string.Empty);
    }

    public static SocialDataResult<T> Fail(SocialErrorCode errorCode, string errorMessage)
    {
        return new SocialDataResult<T>(false, default, errorCode, errorMessage);
    }
}
