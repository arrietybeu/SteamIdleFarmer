namespace SteamFarmer.Core.Storage;

/// <summary>A Steam account whose refresh token we hold so idling resumes across restarts.</summary>
public sealed record StoredAccount(
    ulong SteamId,
    string AccountName,
    string RefreshTokenEnc,
    string? Persona,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastSeenUtc);

/// <summary>Maps a browser session cookie to the Steam account it authenticated.</summary>
public sealed record BrowserLink(string BrowserSessionId, ulong SteamId, DateTimeOffset CreatedAtUtc);

public interface IJobStore
{
    Task<IReadOnlyList<FarmJob>> GetAllAsync();
    Task<IReadOnlyList<FarmJob>> GetByAccountAsync(ulong steamId);
    Task<FarmJob?> GetAsync(string id);
    Task UpsertAsync(FarmJob job);
    Task DeleteAsync(string id);
}

public interface IAccountStore
{
    Task<IReadOnlyList<StoredAccount>> GetAllAccountsAsync();
    Task<StoredAccount?> GetAccountAsync(ulong steamId);
    Task UpsertAccountAsync(StoredAccount account);
    Task DeleteAccountAsync(ulong steamId);

    Task<IReadOnlyList<BrowserLink>> GetBrowserLinksAsync();
    Task UpsertBrowserLinkAsync(BrowserLink link);
    Task DeleteBrowserLinkAsync(string browserSessionId);
}
