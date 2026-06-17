namespace WorkDemoServer.Services;

public sealed class SocialStoreOptions
{
    public const string SectionName = "SocialStore";

    public string FilePath { get; set; } = "Data/social.json";
}
