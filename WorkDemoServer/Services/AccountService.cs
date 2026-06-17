using WorkDemoServer.Contracts;
using WorkDemoServer.Models;

namespace WorkDemoServer.Services;

public sealed class AccountService
{
    private const int FirstSequentialPlayerId = 100001;

    private readonly IAccountRepository repository;
    private readonly IPasswordHasher passwordHasher;

    public AccountService(IAccountRepository repository, IPasswordHasher passwordHasher)
    {
        this.repository = repository;
        this.passwordHasher = passwordHasher;
    }

    public async Task<AccountResult> RegisterAsync(
        RegisterAccountRequest request,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateAccountAndPassword(request.AccountName, request.Password);
        if (validationError != null)
        {
            return AccountResult.Fail(AccountErrorCode.Validation, validationError);
        }

        var accountName = request.AccountName.Trim();
        var normalizedName = NormalizeAccountName(accountName);
        var existing = await repository.FindByAccountNameAsync(normalizedName, cancellationToken);
        if (existing != null)
        {
            return AccountResult.Fail(AccountErrorCode.AccountAlreadyExists, "Account already exists.");
        }

        var playerId = await GenerateUniquePlayerIdAsync(cancellationToken);
        var displayName = string.IsNullOrWhiteSpace(request.DisplayName)
            ? accountName
            : request.DisplayName.Trim();

        var account = new AccountRecord
        {
            AccountName = accountName,
            NormalizedAccountName = normalizedName,
            PasswordHash = passwordHasher.Hash(request.Password),
            PlayerId = playerId,
            DisplayName = displayName,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await repository.AddAsync(account, cancellationToken);
        return AccountResult.Success(ToProfile(account));
    }

    public async Task<AccountResult> LoginAsync(
        LoginAccountRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.AccountName))
        {
            return AccountResult.Fail(AccountErrorCode.Validation, "Account name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return AccountResult.Fail(AccountErrorCode.Validation, "Password is required.");
        }

        var normalizedName = NormalizeAccountName(request.AccountName);
        var account = await repository.FindByAccountNameAsync(normalizedName, cancellationToken);
        if (account == null)
        {
            return AccountResult.Fail(AccountErrorCode.AccountNotFound, "Account does not exist.");
        }

        if (!passwordHasher.Verify(request.Password, account.PasswordHash))
        {
            return AccountResult.Fail(AccountErrorCode.WrongPassword, "Wrong password.");
        }

        return AccountResult.Success(ToProfile(account));
    }

    public async Task<AccountProfileResponse?> GetProfileAsync(
        string playerId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return null;
        }

        var account = await repository.FindByPlayerIdAsync(playerId.Trim(), cancellationToken);
        return account == null ? null : ToProfile(account);
    }

    public async Task<AccountResult> UpdateDisplayNameAsync(
        string playerId,
        string displayName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return AccountResult.Fail(AccountErrorCode.Validation, "Display name is required.");
        }

        var account = await repository.FindByPlayerIdAsync(playerId, cancellationToken);
        if (account == null)
        {
            return AccountResult.Fail(AccountErrorCode.AccountNotFound, "Account does not exist.");
        }

        account.DisplayName = displayName.Trim();
        await repository.UpdateAsync(account, cancellationToken);

        return AccountResult.Success(ToProfile(account));
    }

    private async Task<string> GenerateUniquePlayerIdAsync(CancellationToken cancellationToken)
    {
        var accounts = await repository.GetAllAsync(cancellationToken);
        var nextId = FirstSequentialPlayerId;

        for (var i = 0; i < accounts.Count; i++)
        {
            var account = accounts[i];
            if (account == null || !int.TryParse(account.PlayerId, out var playerId))
            {
                continue;
            }

            nextId = Math.Max(nextId, playerId + 1);
        }

        if (nextId > 999999)
        {
            throw new InvalidOperationException("Player ID range is exhausted.");
        }

        return nextId.ToString();
    }

    private static string? ValidateAccountAndPassword(string accountName, string password)
    {
        if (string.IsNullOrWhiteSpace(accountName))
        {
            return "Account name is required.";
        }

        if (accountName.Trim().Length < 3)
        {
            return "Account name must be at least 3 characters.";
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            return "Password is required.";
        }

        if (password.Length < 6)
        {
            return "Password must be at least 6 characters.";
        }

        return null;
    }

    private static string NormalizeAccountName(string accountName)
    {
        return accountName.Trim().ToUpperInvariant();
    }

    private static AccountProfileResponse ToProfile(AccountRecord account)
    {
        return new AccountProfileResponse(
            account.AccountName,
            account.PlayerId,
            account.DisplayName,
            account.CreatedAt);
    }
}
