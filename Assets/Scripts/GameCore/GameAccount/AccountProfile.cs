using System;

[Serializable]
public class AccountProfile
{
    public string accountName;
    public string playerId;
    public string displayName;
    public long createdAtUnixSeconds;

    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(playerId);
    }

    public AccountProfile Clone()
    {
        return new AccountProfile
        {
            accountName = accountName,
            playerId = playerId,
            displayName = displayName,
            createdAtUnixSeconds = createdAtUnixSeconds
        };
    }
}
