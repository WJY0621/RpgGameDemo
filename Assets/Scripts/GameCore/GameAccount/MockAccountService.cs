using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

public class MockAccountService : IAccountService
{
    private const string ProfileKey = "WorkDemo.MockAccount.Profile";
    private const string AccountListKey = "WorkDemo.MockAccount.Accounts";

    public string LastError { get; private set; }

    public bool HasSavedProfile()
    {
        return PlayerPrefs.HasKey(ProfileKey);
    }

    public AccountProfile LoadSavedProfile()
    {
        string json = PlayerPrefs.GetString(ProfileKey, string.Empty);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        AccountProfile profile = JsonUtility.FromJson<AccountProfile>(json);
        return profile != null && profile.IsValid() ? profile : null;
    }

    public AccountProfile CreateAccount(string displayName)
    {
        LastError = string.Empty;
        AccountProfile profile = new AccountProfile
        {
            playerId = GeneratePlayerId(),
            displayName = ResolveDisplayName(displayName),
            createdAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        Save(profile);
        return profile.Clone();
    }

    public AccountProfile Login(string accountName, string password)
    {
        LastError = string.Empty;
        string normalizedAccount = NormalizeAccount(accountName);
        if (string.IsNullOrWhiteSpace(normalizedAccount) || string.IsNullOrWhiteSpace(password))
        {
            LastError = "请输入账号和密码";
            return null;
        }

        AccountRecordListWrapper wrapper = LoadAccountWrapper();
        string passwordHash = HashPassword(password);
        bool accountExists = false;
        for (int i = 0; i < wrapper.accounts.Count; i++)
        {
            AccountRecord record = wrapper.accounts[i];
            if (record == null)
            {
                continue;
            }

            if (!string.Equals(record.accountName, normalizedAccount, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            accountExists = true;
            if (string.Equals(record.passwordHash, passwordHash, StringComparison.Ordinal))
            {
                AccountProfile profile = new AccountProfile
                {
                    accountName = record.accountName,
                    playerId = record.playerId,
                    displayName = record.displayName,
                    createdAtUnixSeconds = record.createdAtUnixSeconds
                };

                Save(profile);
                return profile.Clone();
            }
        }

        LastError = accountExists ? "密码输入错误" : "账户不存在";
        return null;
    }

    public AccountProfile Register(string accountName, string password)
    {
        LastError = string.Empty;
        string normalizedAccount = NormalizeAccount(accountName);
        if (string.IsNullOrWhiteSpace(normalizedAccount) || string.IsNullOrWhiteSpace(password))
        {
            LastError = "请输入账号和密码";
            return null;
        }

        AccountRecordListWrapper wrapper = LoadAccountWrapper();
        for (int i = 0; i < wrapper.accounts.Count; i++)
        {
            AccountRecord account = wrapper.accounts[i];
            if (account != null &&
                string.Equals(account.accountName, normalizedAccount, StringComparison.OrdinalIgnoreCase))
            {
                LastError = "账户已存在";
                return null;
            }
        }

        AccountRecord record = new AccountRecord
        {
            accountName = normalizedAccount,
            passwordHash = HashPassword(password),
            playerId = GenerateUniquePlayerId(wrapper),
            displayName = string.Empty,
            createdAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        wrapper.accounts.Add(record);
        SaveAccountWrapper(wrapper);

        AccountProfile profile = new AccountProfile
        {
            accountName = record.accountName,
            playerId = record.playerId,
            displayName = record.displayName,
            createdAtUnixSeconds = record.createdAtUnixSeconds
        };

        Save(profile);
        return profile.Clone();
    }

    public AccountProfile LoginOrCreate(string displayName)
    {
        LastError = string.Empty;
        AccountProfile savedProfile = LoadSavedProfile();
        if (savedProfile != null)
        {
            return savedProfile.Clone();
        }

        return CreateAccount(displayName);
    }

    public void Logout()
    {
    }

    public void SaveProfile(AccountProfile profile)
    {
        if (profile == null || !profile.IsValid())
        {
            return;
        }

        Save(profile);
        UpdateAccountRecord(profile);
    }

    public void ClearSavedProfile()
    {
        PlayerPrefs.DeleteKey(ProfileKey);
        PlayerPrefs.Save();
    }

    private void Save(AccountProfile profile)
    {
        PlayerPrefs.SetString(ProfileKey, JsonUtility.ToJson(profile));
        PlayerPrefs.Save();
    }

    private string GeneratePlayerId()
    {
        return UnityEngine.Random.Range(100000, 1000000).ToString();
    }

    private string GenerateUniquePlayerId(AccountRecordListWrapper wrapper)
    {
        for (int i = 0; i < 100; i++)
        {
            string candidate = GeneratePlayerId();
            if (!ContainsPlayerId(wrapper, candidate))
            {
                return candidate;
            }
        }

        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString().Substring(7, 6);
    }

    private bool ContainsPlayerId(AccountRecordListWrapper wrapper, string playerId)
    {
        if (wrapper == null || string.IsNullOrWhiteSpace(playerId))
        {
            return false;
        }

        for (int i = 0; i < wrapper.accounts.Count; i++)
        {
            AccountRecord account = wrapper.accounts[i];
            if (account != null &&
                string.Equals(account.playerId, playerId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private string ResolveDisplayName(string displayName)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            return displayName.Trim();
        }

        return $"Player_{UnityEngine.Random.Range(1000, 9999)}";
    }

    private AccountRecordListWrapper LoadAccountWrapper()
    {
        string json = PlayerPrefs.GetString(AccountListKey, string.Empty);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new AccountRecordListWrapper();
        }

        AccountRecordListWrapper wrapper = JsonUtility.FromJson<AccountRecordListWrapper>(json);
        if (wrapper == null)
        {
            wrapper = new AccountRecordListWrapper();
        }

        if (wrapper.accounts == null)
        {
            wrapper.accounts = new List<AccountRecord>();
        }

        return wrapper;
    }

    private void SaveAccountWrapper(AccountRecordListWrapper wrapper)
    {
        PlayerPrefs.SetString(AccountListKey, JsonUtility.ToJson(wrapper));
        PlayerPrefs.Save();
    }

    private void UpdateAccountRecord(AccountProfile profile)
    {
        if (profile == null || string.IsNullOrWhiteSpace(profile.accountName))
        {
            return;
        }

        AccountRecordListWrapper wrapper = LoadAccountWrapper();
        bool changed = false;
        for (int i = 0; i < wrapper.accounts.Count; i++)
        {
            AccountRecord account = wrapper.accounts[i];
            if (account == null)
            {
                continue;
            }

            if (string.Equals(account.accountName, profile.accountName, StringComparison.OrdinalIgnoreCase))
            {
                account.displayName = profile.displayName;
                changed = true;
                break;
            }
        }

        if (changed)
        {
            SaveAccountWrapper(wrapper);
        }
    }

    private string NormalizeAccount(string accountName)
    {
        return string.IsNullOrWhiteSpace(accountName) ? string.Empty : accountName.Trim().ToLowerInvariant();
    }

    private string HashPassword(string password)
    {
        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] bytes = Encoding.UTF8.GetBytes(password ?? string.Empty);
            byte[] hashBytes = sha256.ComputeHash(bytes);
            StringBuilder builder = new StringBuilder(hashBytes.Length * 2);
            for (int i = 0; i < hashBytes.Length; i++)
            {
                builder.Append(hashBytes[i].ToString("x2"));
            }

            return builder.ToString();
        }
    }

    [Serializable]
    private class AccountRecord
    {
        public string accountName;
        public string passwordHash;
        public string playerId;
        public string displayName;
        public long createdAtUnixSeconds;
    }

    [Serializable]
    private class AccountRecordListWrapper
    {
        public List<AccountRecord> accounts = new List<AccountRecord>();
    }
}
