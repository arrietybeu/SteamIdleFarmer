using System.Collections.Concurrent;
using System.Security.Cryptography;
using SteamFarmer.Core.Abstractions;
using SteamFarmer.Core.Security;
using SteamFarmer.Core.Storage;

namespace SteamFarmer.Core.Sessions;

/// <summary>
/// Multi-tenant registry. Each browser cookie maps to an <see cref="AccountSession"/> (its own Steam
/// connection). Sessions persist server-side and keep idling even with no browser attached. On startup
/// it resumes every stored account so farming continues across restarts.
/// </summary>
public sealed class SessionManager : IAsyncDisposable
{
    private readonly IAccountStore _accounts;
    private readonly IJobStore _jobStore;
    private readonly ITokenProtector _protector;
    private readonly string _deviceName;

    private readonly ConcurrentDictionary<string, AccountSession> _byBrowser = new();
    private readonly ConcurrentDictionary<ulong, AccountSession> _bySteamId = new();
    private readonly Timer _sweepTimer;
    private int _loginCounter;

    public SessionManager(IAccountStore accounts, IJobStore jobStore, ITokenProtector protector, string? deviceName = null)
    {
        _accounts = accounts;
        _jobStore = jobStore;
        _protector = protector;
        _deviceName = deviceName ?? "SteamIdleFarmer";
        _sweepTimer = new Timer(_ => Sweep(), null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    /// <summary>Evict never-authenticated sessions so a flood of anonymous cookies can't leak memory.</summary>
    private void Sweep()
    {
        var cutoff = DateTimeOffset.UtcNow - TimeSpan.FromMinutes(15);
        foreach (var (sid, session) in _byBrowser)
        {
            if (session.SteamId is null &&
                session.Status.State != AuthState.LoggedIn &&
                session.CreatedAtUtc < cutoff &&
                _byBrowser.TryRemove(sid, out var removed))
            {
                _ = removed.DisposeAsync();
            }
        }
    }

    /// <summary>Get the session for a browser cookie, creating a fresh (not-logged-in) one if needed.</summary>
    public AccountSession GetOrCreate(string browserSessionId)
        => _byBrowser.GetOrAdd(browserSessionId, CreateSession);

    /// <summary>Look up an existing session for a browser cookie without creating one.</summary>
    public AccountSession? Find(string browserSessionId)
        => _byBrowser.TryGetValue(browserSessionId, out var s) ? s : null;

    private AccountSession CreateSession(string browserSessionId)
        => new(browserSessionId, NextLoginId(), _protector, _accounts, _jobStore, OnLoggedInAsync, _deviceName);

    private uint NextLoginId()
        => 0x5AF00000u + (uint)Interlocked.Increment(ref _loginCounter);

    private async Task OnLoggedInAsync(AccountSession session)
    {
        if (session.SteamId is not { } steamId)
            return;

        _bySteamId[steamId] = session;
        await _accounts.UpsertBrowserLinkAsync(
            new BrowserLink(session.BrowserSessionId, steamId, DateTimeOffset.UtcNow)).ConfigureAwait(false);
    }

    /// <summary>On startup, bring every stored account back online and reconnect their browser cookies.</summary>
    public async Task ResumeAllAsync()
    {
        var accounts = await _accounts.GetAllAccountsAsync().ConfigureAwait(false);
        var links = (await _accounts.GetBrowserLinksAsync().ConfigureAwait(false))
            .GroupBy(l => l.SteamId)
            .ToDictionary(g => g.Key, g => g.Select(l => l.BrowserSessionId).ToList());

        foreach (var account in accounts)
        {
            // Only real, random browser-link ids may address a session from a cookie. If an account
            // has no link, its primary id is a fresh random token (NEVER "resumed-{steamId}", which
            // would be guessable from the public SteamID) and it is reachable only internally by SteamId.
            var sids = links.TryGetValue(account.SteamId, out var list) ? list : [];
            string primary = sids.Count > 0
                ? sids[0]
                : $"resumed-{Convert.ToHexString(RandomNumberGenerator.GetBytes(16))}";

            var session = CreateSession(primary);
            _bySteamId[account.SteamId] = session;
            foreach (var sid in sids)
                _byBrowser[sid] = session;

            try
            {
                var token = _protector.Unprotect(account.RefreshTokenEnc);
                await session.ResumeAsync(account.AccountName, token).ConfigureAwait(false);
            }
            catch
            {
                // Bad/expired token — the account will need a fresh QR login.
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _sweepTimer.DisposeAsync().ConfigureAwait(false);
        foreach (var session in _byBrowser.Values.Distinct())
            await session.DisposeAsync().ConfigureAwait(false);
    }
}
