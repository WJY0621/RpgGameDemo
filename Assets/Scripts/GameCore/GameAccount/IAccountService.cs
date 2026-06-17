public interface IAccountService
{
    string LastError { get; }
    bool HasSavedProfile();
    AccountProfile LoadSavedProfile();
    AccountProfile Login(string accountName, string password);
    AccountProfile Register(string accountName, string password);
    AccountProfile CreateAccount(string displayName);
    AccountProfile LoginOrCreate(string displayName);
    void SaveProfile(AccountProfile profile);
    void Logout();
    void ClearSavedProfile();
}
