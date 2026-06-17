namespace WorkDemoServer.Models;

public sealed class AccountRecord
{
    public string AccountName { get; set; } = string.Empty;

    public string NormalizedAccountName { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string PlayerId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}
