using WorkDemoServer.Contracts;

namespace WorkDemoServer.Services;

public sealed class AdminService
{
    private readonly IAccountRepository accounts;
    private readonly ISocialRepository social;
    private readonly SessionService sessions;

    public AdminService(
        IAccountRepository accounts,
        ISocialRepository social,
        SessionService sessions)
    {
        this.accounts = accounts;
        this.social = social;
        this.sessions = sessions;
    }

    public async Task<IReadOnlyList<AccountAdminResponse>> GetAccountsAsync(
        CancellationToken cancellationToken)
    {
        var accountRecords = await accounts.GetAllAsync(cancellationToken);
        var result = new List<AccountAdminResponse>();

        for (var i = 0; i < accountRecords.Count; i++)
        {
            var account = accountRecords[i];
            if (account == null || string.IsNullOrWhiteSpace(account.PlayerId))
            {
                continue;
            }

            var friendships = await social.GetFriendshipsAsync(account.PlayerId, cancellationToken);
            var incomingRequests = await social.GetIncomingFriendRequestsAsync(account.PlayerId, cancellationToken);

            result.Add(new AccountAdminResponse(
                account.AccountName,
                account.PlayerId,
                account.DisplayName,
                account.CreatedAt,
                sessions.IsOnline(account.PlayerId),
                friendships.Count,
                incomingRequests.Count));
        }

        return result
            .OrderBy(account => account.PlayerId)
            .ToList();
    }
}
