using System;

public class AccountMgr
{
    private readonly IAccountService accountService;

    public AccountProfile CurrentProfile { get; private set; }
    public bool IsLoggedIn => CurrentProfile != null && CurrentProfile.IsValid();
    public string LastError { get; private set; }

    public event Action<AccountProfile> OnProfileChanged;

    public AccountMgr(IAccountService accountService = null)
    {
        this.accountService = accountService ?? new MockAccountService();
    }

    public void Init()
    {
        CurrentProfile = accountService.LoadSavedProfile();
        LastError = CurrentProfile != null ? string.Empty : accountService.LastError;
        if (CurrentProfile != null)
        {
            OnProfileChanged?.Invoke(CurrentProfile.Clone());
        }
    }

    public AccountProfile LoginOrCreate(string displayName)
    {
        CurrentProfile = accountService.LoginOrCreate(displayName);
        LastError = CurrentProfile != null ? string.Empty : accountService.LastError;
        OnProfileChanged?.Invoke(CurrentProfile?.Clone());
        return CurrentProfile?.Clone();
    }

    public AccountProfile CreateNewAccount(string displayName)
    {
        CurrentProfile = accountService.CreateAccount(displayName);
        LastError = CurrentProfile != null ? string.Empty : accountService.LastError;
        OnProfileChanged?.Invoke(CurrentProfile?.Clone());
        return CurrentProfile?.Clone();
    }

    public AccountProfile Login(string accountName, string password)
    {
        CurrentProfile = accountService.Login(accountName, password);
        LastError = CurrentProfile != null ? string.Empty : accountService.LastError;
        OnProfileChanged?.Invoke(CurrentProfile?.Clone());
        return CurrentProfile?.Clone();
    }

    public AccountProfile Register(string accountName, string password)
    {
        CurrentProfile = accountService.Register(accountName, password);
        LastError = CurrentProfile != null ? string.Empty : accountService.LastError;
        OnProfileChanged?.Invoke(CurrentProfile?.Clone());
        return CurrentProfile?.Clone();
    }

    public AccountProfile SetDisplayName(string displayName)
    {
        if (CurrentProfile == null || !CurrentProfile.IsValid())
        {
            LastError = "No logged in account.";
            return null;
        }

        CurrentProfile.displayName = string.IsNullOrWhiteSpace(displayName)
            ? string.Empty
            : displayName.Trim();
        accountService.SaveProfile(CurrentProfile);
        LastError = string.Empty;
        OnProfileChanged?.Invoke(CurrentProfile.Clone());
        return CurrentProfile.Clone();
    }

    public void Logout()
    {
        accountService.Logout();
        CurrentProfile = null;
        LastError = string.Empty;
        OnProfileChanged?.Invoke(null);
    }

    public void ClearLocalAccount()
    {
        accountService.ClearSavedProfile();
        CurrentProfile = null;
        LastError = string.Empty;
        OnProfileChanged?.Invoke(null);
    }
}
