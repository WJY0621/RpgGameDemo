namespace WorkDemoServer.Contracts;

public sealed record RegisterAccountRequest(
    string AccountName,
    string Password,
    string? DisplayName);

public sealed record LoginAccountRequest(
    string AccountName,
    string Password);

public sealed record UpdateDisplayNameRequest(string DisplayName);
