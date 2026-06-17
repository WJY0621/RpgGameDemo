namespace WorkDemoServer.Services;

public sealed class AccountStoreOptions
{
    public const string SectionName = "AccountStore";

    public string FilePath { get; set; } = "Data/accounts.json";
}
