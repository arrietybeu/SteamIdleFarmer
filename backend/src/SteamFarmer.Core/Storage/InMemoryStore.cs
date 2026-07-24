using System.Collections.Concurrent;

namespace SteamFarmer.Core.Storage;

/// <summary>In-memory store for tests and dev runs without persistence.</summary>
public sealed class InMemoryStore : IJobStore, IAccountStore
{
    private readonly ConcurrentDictionary<string, FarmJob> _jobs = new();
    private readonly ConcurrentDictionary<ulong, StoredAccount> _accounts = new();
    private readonly ConcurrentDictionary<string, BrowserLink> _links = new();

    public Task<IReadOnlyList<FarmJob>> GetAllAsync()
        => Task.FromResult<IReadOnlyList<FarmJob>>(_jobs.Values.ToList());

    public Task<IReadOnlyList<FarmJob>> GetByAccountAsync(ulong steamId)
        => Task.FromResult<IReadOnlyList<FarmJob>>(_jobs.Values.Where(j => j.SteamId == steamId).ToList());

    public Task<FarmJob?> GetAsync(string id)
        => Task.FromResult(_jobs.TryGetValue(id, out var j) ? j : null);

    public Task UpsertAsync(FarmJob job)
    {
        _jobs[job.Id] = job;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string id)
    {
        _jobs.TryRemove(id, out _);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<StoredAccount>> GetAllAccountsAsync()
        => Task.FromResult<IReadOnlyList<StoredAccount>>(_accounts.Values.ToList());

    public Task<StoredAccount?> GetAccountAsync(ulong steamId)
        => Task.FromResult(_accounts.TryGetValue(steamId, out var a) ? a : null);

    public Task UpsertAccountAsync(StoredAccount account)
    {
        _accounts[account.SteamId] = account;
        return Task.CompletedTask;
    }

    public Task DeleteAccountAsync(ulong steamId)
    {
        _accounts.TryRemove(steamId, out _);
        foreach (var link in _links.Where(l => l.Value.SteamId == steamId).ToList())
            _links.TryRemove(link.Key, out _);
        foreach (var job in _jobs.Where(j => j.Value.SteamId == steamId).ToList())
            _jobs.TryRemove(job.Key, out _);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<BrowserLink>> GetBrowserLinksAsync()
        => Task.FromResult<IReadOnlyList<BrowserLink>>(_links.Values.ToList());

    public Task UpsertBrowserLinkAsync(BrowserLink link)
    {
        _links[link.BrowserSessionId] = link;
        return Task.CompletedTask;
    }

    public Task DeleteBrowserLinkAsync(string browserSessionId)
    {
        _links.TryRemove(browserSessionId, out _);
        return Task.CompletedTask;
    }
}
