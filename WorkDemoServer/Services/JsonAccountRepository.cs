using System.Text.Json;
using Microsoft.Extensions.Options;
using WorkDemoServer.Models;

namespace WorkDemoServer.Services;

public sealed class JsonAccountRepository : IAccountRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string storePath;
    private readonly SemaphoreSlim gate = new(1, 1);

    public JsonAccountRepository(IOptions<AccountStoreOptions> options, IWebHostEnvironment environment)
    {
        var configuredPath = options.Value.FilePath;
        storePath = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(environment.ContentRootPath, configuredPath);
    }

    public async Task<IReadOnlyList<AccountRecord>> GetAllAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            return await LoadAccountsAsync(cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<AccountRecord?> FindByAccountNameAsync(
        string normalizedAccountName,
        CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var accounts = await LoadAccountsAsync(cancellationToken);
            return accounts.FirstOrDefault(account =>
                string.Equals(account.NormalizedAccountName, normalizedAccountName, StringComparison.Ordinal));
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<AccountRecord?> FindByPlayerIdAsync(
        string playerId,
        CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var accounts = await LoadAccountsAsync(cancellationToken);
            return accounts.FirstOrDefault(account =>
                string.Equals(account.PlayerId, playerId, StringComparison.Ordinal));
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task AddAsync(AccountRecord account, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var accounts = await LoadAccountsAsync(cancellationToken);
            accounts.Add(account);
            await SaveAccountsAsync(accounts, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task UpdateAsync(AccountRecord account, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var accounts = await LoadAccountsAsync(cancellationToken);
            var index = accounts.FindIndex(item =>
                string.Equals(item.PlayerId, account.PlayerId, StringComparison.Ordinal));

            if (index < 0)
            {
                accounts.Add(account);
            }
            else
            {
                accounts[index] = account;
            }

            await SaveAccountsAsync(accounts, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<List<AccountRecord>> LoadAccountsAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(storePath))
        {
            return new List<AccountRecord>();
        }

        await using var stream = File.OpenRead(storePath);
        var accounts = await JsonSerializer.DeserializeAsync<List<AccountRecord>>(
            stream,
            JsonOptions,
            cancellationToken);

        return accounts ?? new List<AccountRecord>();
    }

    private async Task SaveAccountsAsync(
        List<AccountRecord> accounts,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(storePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = storePath + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, accounts, JsonOptions, cancellationToken);
        }

        if (File.Exists(storePath))
        {
            File.Delete(storePath);
        }

        File.Move(tempPath, storePath);
    }
}
