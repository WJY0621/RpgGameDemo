namespace WorkDemoServer.Contracts;

public sealed record AccountProfileResponse(
    string AccountName,
    string PlayerId,
    string DisplayName,
    DateTimeOffset CreatedAt);

public sealed record AccountAuthResponse(
    AccountProfileResponse Profile,
    string SessionToken);

public sealed record ApiError(
    string Code,
    string Message)
{
    public static ApiError Validation(string message)
    {
        return new ApiError("validation_error", message);
    }

    public static ApiError Conflict(string message)
    {
        return new ApiError("conflict", message);
    }

    public static ApiError NotFound(string message)
    {
        return new ApiError("not_found", message);
    }
}
