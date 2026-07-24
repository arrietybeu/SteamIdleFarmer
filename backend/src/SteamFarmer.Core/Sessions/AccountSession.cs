using SteamFarmer.Core.Abstractions;
using SteamFarmer.Core.Jobs;
using SteamFarmer.Core.Security;
using SteamFarmer.Core.Steam;
using SteamFarmer.Core.Storage;

namespace SteamFarmer.Core.Sessions;

/// <summary>
/// The server-side lifecycle of one logged-in Steam account: it wires the Steam connection to a
/// <see cref="JobManager"/>, runs the periodic tick loop, persists the (encrypted) refresh token so
/// idling survives restarts, and aggregates events for the web layer. Keeps running even when the
/// owner closes their browser — that's the point of a VPS farmer.
/// </summary>
public sealed class AccountSession : IAsyncDisposable
{
    private readonly ITokenProtector _protector;
    private readonly IAccountStore _accounts;
    private readonly IJobStore _jobStore;
    private readonly Func<AccountSession, Task> _onLoggedIn;
    private readonly CancellationTokenSource _cts = new();

    private string? _accountName;
    private string? _refreshToken;
    private bool _initialized;
    private Task? _tickLoop;

    public string BrowserSessionId { get; }
    public DateTimeOffset CreatedAtUtc { get; } = DateTimeOffset.UtcNow;
    public SteamKitSteamService Steam { get; }
    public JobManager? Jobs { get; private set; }

    public ulong? SteamId => Steam.SteamId;
    public AuthStatus Status => Steam.Status;

    /// <summary>Fires on any auth/job change — the web layer pushes a fresh snapshot on this.</summary>
    public event Action? Changed;
    public event Action<string>? ChallengeUrl;
    public event Action<FarmJob, ScheduledUnlock>? AchievementUnlocked;

    public AccountSession(
        string browserSessionId,
        uint loginId,
        ITokenProtector protector,
        IAccountStore accounts,
        IJobStore jobStore,
        Func<AccountSession, Task> onLoggedIn,
        string deviceName)
    {
        BrowserSessionId = browserSessionId;
        _protector = protector;
        _accounts = accounts;
        _jobStore = jobStore;
        _onLoggedIn = onLoggedIn;

        Steam = new SteamKitSteamService(loginId, deviceName);
        Steam.CredentialsObtained += (name, token) => { _accountName = name; _refreshToken = token; };
        Steam.ChallengeUrlChanged += url => ChallengeUrl?.Invoke(url);
        Steam.AuthStatusChanged += OnAuthChanged;
    }

    public Task<QrChallenge> BeginQrLoginAsync(CancellationToken ct = default) => Steam.BeginQrLoginAsync(ct);

    /// <summary>Resume a stored account (from an encrypted token) without QR.</summary>
    public Task ResumeAsync(string accountName, string refreshToken, CancellationToken ct = default)
    {
        _accountName = accountName;
        _refreshToken = refreshToken;
        return Steam.ResumeWithTokenAsync(accountName, refreshToken, ct);
    }

    public async Task LogoutAsync()
    {
        var steamId = SteamId;
        await Steam.LogoutAsync().ConfigureAwait(false);
        if (steamId is { } id)
            await _accounts.DeleteAccountAsync(id).ConfigureAwait(false); // cascades jobs + links
        _initialized = false;
        Jobs = null;
        Changed?.Invoke();
    }

    private async void OnAuthChanged(AuthStatus status)
    {
        try
        {
            Changed?.Invoke();

            if (status.State != AuthState.LoggedIn || _initialized || status.SteamId is not { } steamId)
                return;

            _initialized = true;

            if (_refreshToken is not null && _accountName is not null)
                await PersistAccountAsync(steamId, status.Persona).ConfigureAwait(false);

            var jobs = new JobManager(Steam, _jobStore, steamId);
            jobs.JobsChanged += () => Changed?.Invoke();
            jobs.AchievementUnlocked += (j, u) => AchievementUnlocked?.Invoke(j, u);
            await jobs.InitializeAsync().ConfigureAwait(false);
            Jobs = jobs;

            await _onLoggedIn(this).ConfigureAwait(false);
            StartTickLoop();
            Changed?.Invoke();
        }
        catch
        {
            // Never let an event handler throw into SteamKit's pump.
        }
    }

    private async Task PersistAccountAsync(ulong steamId, string? persona)
    {
        var existing = await _accounts.GetAccountAsync(steamId).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow;
        await _accounts.UpsertAccountAsync(new StoredAccount(
            steamId,
            _accountName!,
            _protector.Protect(_refreshToken!),
            persona,
            existing?.CreatedAtUtc ?? now,
            now)).ConfigureAwait(false);
    }

    private void StartTickLoop()
    {
        _tickLoop = Task.Run(async () =>
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    if (Jobs is not null)
                        await Jobs.TickAsync().ConfigureAwait(false);
                }
                catch
                {
                    // swallow — a single tick failure must not stop the loop
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), _cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        });
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync().ConfigureAwait(false);
        await Steam.DisposeAsync().ConfigureAwait(false);
        _cts.Dispose();
    }
}
