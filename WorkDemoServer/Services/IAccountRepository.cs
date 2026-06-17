using WorkDemoServer.Models;

namespace WorkDemoServer.Services;

public interface IAccountRepository
{
    Task<IReadOnlyList<AccountRecord>> GetAllAsync(CancellationToken cancellationToken);

    Task<AccountRecord?> FindByAccountNameAsync(
        string normalizedAccountName,
        CancellationToken cancellationToken);

    Task<AccountRecord?> FindByPlayerIdAsync(
        string playerId,
        CancellationToken cancellationToken);

    Task AddAsync(AccountRecord account, CancellationToken cancellationToken);

    Task UpdateAsync(AccountRecord account, CancellationToken cancellationToken);
}
