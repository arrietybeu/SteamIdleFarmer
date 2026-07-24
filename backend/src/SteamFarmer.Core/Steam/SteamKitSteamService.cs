using SteamKit2;
using SteamKit2.Authentication;
using SteamKit2.Internal;
using SteamFarmer.Core.Abstractions;

namespace SteamFarmer.Core.Steam;

/// <summary>
/// One Steam account's live connection, built on SteamKit2 3.4. Owns a dedicated callback pump,
/// handles QR login, token-based resume/reconnect, persona + "playing games" (idle), and
/// achievement get/set (delegated to <see cref="AchievementHandler"/>).
///
/// One instance per account. Each instance MUST get a unique <c>loginId</c> so multiple accounts
/// from the same VPS IP do not evict each other's sessions (LogonSessionReplaced).
/// </summary>
public sealed class SteamKitSteamService : ISteamService, IAsyncDisposable
{
    private readonly uint _loginId;
    private readonly string _deviceName;
    private readonly SteamClient _client;
    private readonly CallbackManager _manager;
    private readonly SteamUser _user;
    private readonly SteamFriends _friends;
    private readonly Player _playerService;
    private readonly AchievementHandler _achievements;
    private readonly CancellationTokenSource _cts = new();
    private Thread? _pumpThread;
    private int _pumpStarted;
    private readonly object _gate = new();
    private readonly HashSet<uint> _playingGames = [];

    private string? _accountName;
    private string? _refreshToken;
    private TaskCompletionSource? _connectTcs;
    private int _qrGeneration;
    private int _reconnectAttempt;
    private AuthStatus _status = new(AuthState.Disconnected, null, null, null);

    public SteamKitSteamService(uint loginId, string? deviceName = null)
    {
        _loginId = loginId;
        _deviceName = deviceName ?? "SteamIdleFarmer";

        _client = new SteamClient();
        _manager = new CallbackManager(_client);
        _user = _client.GetHandler<SteamUser>()!;
        _friends = _client.GetHandler<SteamFriends>()!;
        _playerService = _client.GetHandler<SteamUnifiedMessages>()!.CreateService<Player>();
        _achievements = new AchievementHandler();
        _client.AddHandler(_achievements);

        _manager.Subscribe<SteamClient.ConnectedCallback>(OnConnected);
        _manager.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnected);
        _manager.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);
        _manager.Subscribe<SteamUser.LoggedOffCallback>(OnLoggedOff);
        _manager.Subscribe<SteamFriends.PersonaStateCallback>(OnPersonaState);
        // The callback pump thread starts lazily on first connect (see EnsurePumpRunning) so an
        // idle, never-logged-in session costs no OS thread — mitigates session-flood DoS.
    }

    public AuthStatus Status => _status;
    public ulong? SteamId => _status.SteamId;

    public event Action<AuthStatus>? AuthStatusChanged;
    public event Action<string>? ChallengeUrlChanged;

    /// <summary>Raised once QR login yields a refresh token, so it can be persisted for resume/reconnect.</summary>
    public event Action<string, string>? CredentialsObtained;

    private void PumpLoop()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                _manager.RunWaitCallbacks(TimeSpan.FromSeconds(1));
            }
            catch
            {
                // Never let a single callback exception kill the pump.
            }
        }
    }

    /// <summary>Start the callback pump exactly once, on first real use (connect). Idle sessions cost no thread.</summary>
    private void EnsurePumpRunning()
    {
        if (Interlocked.CompareExchange(ref _pumpStarted, 1, 0) != 0)
            return;
        _pumpThread = new Thread(PumpLoop) { IsBackground = true, Name = $"steam-pump-{_loginId}" };
        _pumpThread.Start();
    }

    // ---- Auth --------------------------------------------------------------

    public async Task<QrChallenge> BeginQrLoginAsync(CancellationToken ct = default)
    {
        await EnsureConnectedAsync(ct).ConfigureAwait(false);
        SetStatus(_status with { State = AuthState.AwaitingQr, Error = null });

        // Supersede any previous QR loop so only the newest code is active.
        int generation = Interlocked.Increment(ref _qrGeneration);
        var qr = await NewQrSessionAsync().ConfigureAwait(false);
        WireChallenge(qr, generation);
        _ = Task.Run(() => PollQrLoopAsync(qr, generation), _cts.Token);

        return new QrChallenge(qr.ChallengeURL, Guid.NewGuid().ToString("N"));
    }

    private Task<QrAuthSession> NewQrSessionAsync()
        => _client.Authentication.BeginAuthSessionViaQRAsync(new AuthSessionDetails
        {
            DeviceFriendlyName = _deviceName,
            IsPersistentSession = true,
        });

    private void WireChallenge(QrAuthSession qr, int generation)
        => qr.ChallengeURLChanged = () =>
        {
            if (generation == _qrGeneration)
                ChallengeUrlChanged?.Invoke(qr.ChallengeURL);
        };

    /// <summary>
    /// Wait for the user to scan the QR. A Steam QR session expires after a few minutes, which surfaces
    /// as <c>AsyncJobFailedException</c>; instead of erroring, we transparently mint a fresh code and push
    /// it to the UI, up to a bounded number of consecutive failures.
    /// </summary>
    private async Task PollQrLoopAsync(QrAuthSession qr, int generation)
    {
        const int maxConsecutiveFailures = 10;
        int failures = 0;

        while (!_cts.IsCancellationRequested && generation == _qrGeneration)
        {
            try
            {
                var result = await qr.PollingWaitForResultAsync(_cts.Token).ConfigureAwait(false);
                if (generation != _qrGeneration)
                    return; // superseded by a newer QR request

                _accountName = result.AccountName;
                _refreshToken = result.RefreshToken;
                CredentialsObtained?.Invoke(result.AccountName, result.RefreshToken);
                LogOnWithToken();
                return;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception)
            {
                // QR expired (AsyncJobFailedException) or a transient CM hiccup — renew the code.
                if (generation != _qrGeneration || _cts.IsCancellationRequested)
                    return;

                if (++failures > maxConsecutiveFailures)
                {
                    SetStatus(new AuthStatus(AuthState.Error, null, null,
                        "Không tạo được mã QR (mất kết nối Steam). Bấm \"Tạo mã mới\" để thử lại."));
                    return;
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), _cts.Token).ConfigureAwait(false);
                    if (!_client.IsConnected)
                        await EnsureConnectedAsync(_cts.Token).ConfigureAwait(false);
                    if (generation != _qrGeneration)
                        return;

                    qr = await NewQrSessionAsync().ConfigureAwait(false);
                    WireChallenge(qr, generation);
                    ChallengeUrlChanged?.Invoke(qr.ChallengeURL); // push the fresh code to the UI
                    SetStatus(_status with { State = AuthState.AwaitingQr, Error = null });
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch
                {
                    SetStatus(new AuthStatus(AuthState.Error, null, null,
                        "Mất kết nối Steam khi tạo mã QR. Thử lại sau giây lát."));
                    return;
                }
            }
        }
    }

    /// <summary>Resume a previously-authenticated account from a stored refresh token (no QR).</summary>
    public async Task ResumeWithTokenAsync(string accountName, string refreshToken, CancellationToken ct = default)
    {
        _accountName = accountName;
        _refreshToken = refreshToken;
        await EnsureConnectedAsync(ct).ConfigureAwait(false);
        // OnConnected auto-logs-on when a token is present.
    }

    public Task LogoutAsync(CancellationToken ct = default)
    {
        _refreshToken = null;
        _accountName = null;
        lock (_gate)
            _playingGames.Clear();

        if (_client.IsConnected)
        {
            _user.LogOff();
            _client.Disconnect();
        }
        SetStatus(new AuthStatus(AuthState.Disconnected, null, null, null));
        return Task.CompletedTask;
    }

    private Task EnsureConnectedAsync(CancellationToken ct)
    {
        EnsurePumpRunning();
        if (_client.IsConnected)
            return Task.CompletedTask;

        lock (_gate)
        {
            if (_connectTcs is null || _connectTcs.Task.IsCompleted)
            {
                _connectTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _client.Connect();
            }
            return _connectTcs.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
        }
    }

    private void LogOnWithToken()
    {
        if (_accountName is null || _refreshToken is null)
            return;

        SetStatus(_status with { State = AuthState.LoggingIn, Error = null });
        _user.LogOn(new SteamUser.LogOnDetails
        {
            Username = _accountName,
            AccessToken = _refreshToken,
            ShouldRememberPassword = true,
            LoginID = _loginId,
        });
    }

    private void OnConnected(SteamClient.ConnectedCallback cb)
    {
        Interlocked.Exchange(ref _reconnectAttempt, 0); // healthy again — reset the backoff
        _connectTcs?.TrySetResult();
        // Reconnect / resume path: log straight back on if we already hold a token.
        if (_refreshToken is not null && _accountName is not null && _status.State != AuthState.AwaitingQr)
            LogOnWithToken();
    }

    private void OnDisconnected(SteamClient.DisconnectedCallback cb)
    {
        _connectTcs?.TrySetCanceled();

        if (cb.UserInitiated || _cts.IsCancellationRequested)
        {
            SetStatus(_status with { State = AuthState.Disconnected });
            return;
        }

        // Without a token there is nothing to log back on with (logged out, or the token died):
        // stay disconnected instead of reconnecting forever. The QR flow reconnects on demand.
        if (_refreshToken is null || _accountName is null)
        {
            SetStatus(_status with { State = AuthState.Disconnected });
            return;
        }

        // Steam does not auto-reconnect. Exponential backoff + jitter so a long Steam outage
        // can't turn into us hammering the CM servers from one IP (which risks a rate limit).
        SetStatus(_status with { State = AuthState.LoggingIn });
        int attempt = Interlocked.Increment(ref _reconnectAttempt);
        double seconds = Math.Min(5 * Math.Pow(2, Math.Min(attempt - 1, 6)), 300)
                         + Random.Shared.NextDouble() * 3;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(seconds), _cts.Token).ConfigureAwait(false);
                if (!_cts.IsCancellationRequested)
                    _client.Connect();
            }
            catch (OperationCanceledException)
            {
            }
        });
    }

    private void OnLoggedOn(SteamUser.LoggedOnCallback cb)
    {
        if (cb.Result != EResult.OK)
        {
            if (cb.Result is EResult.InvalidPassword or EResult.AccessDenied or EResult.Expired or EResult.Revoked)
            {
                // Stored token is dead — force a fresh QR next time.
                _refreshToken = null;
                _accountName = null;
            }
            SetStatus(new AuthStatus(AuthState.Error, null, null, cb.Result.ToString()));
            return;
        }

        ulong? steamId = cb.ClientSteamID?.ConvertToUInt64() ?? _user.SteamID?.ConvertToUInt64();
        SetStatus(new AuthStatus(AuthState.LoggedIn, _friends.GetPersonaName(), steamId, null));
        _friends.SetPersonaState(EPersonaState.Online);
        ResendPlayingGames();
    }

    private void OnLoggedOff(SteamUser.LoggedOffCallback cb)
    {
        if (_status.State == AuthState.LoggedIn)
            SetStatus(_status with { State = AuthState.LoggingIn });
    }

    private void OnPersonaState(SteamFriends.PersonaStateCallback cb)
    {
        if (_status.State == AuthState.LoggedIn &&
            _status.SteamId is { } id &&
            cb.FriendID.ConvertToUInt64() == id &&
            !string.IsNullOrEmpty(cb.Name) &&
            cb.Name != _status.Persona)
        {
            SetStatus(_status with { Persona = cb.Name });
        }
    }

    // ---- Idle --------------------------------------------------------------

    public Task SetPlayingGamesAsync(IReadOnlyCollection<uint> appIds, CancellationToken ct = default)
    {
        var desired = appIds.Take(32).ToHashSet(); // Steam network cap

        bool changed;
        lock (_gate)
        {
            changed = !_playingGames.SetEquals(desired);
            if (changed)
            {
                _playingGames.Clear();
                foreach (var appId in desired)
                    _playingGames.Add(appId);
            }
        }

        // Only resend when the SET actually changes. Re-sending an identical set makes Steam end the
        // current play session and start a new one — doing that on every achievement unlock chops the
        // session into fragments and playtime never accumulates.
        if (changed && _client.IsConnected && _status.State == AuthState.LoggedIn)
            ResendPlayingGames();
        return Task.CompletedTask;
    }

    private void ResendPlayingGames()
    {
        var msg = new ClientMsgProtobuf<CMsgClientGamesPlayed>(EMsg.ClientGamesPlayedWithDataBlob);
        lock (_gate)
        {
            foreach (var appId in _playingGames)
                msg.Body.games_played.Add(new CMsgClientGamesPlayed.GamePlayed { game_id = new GameID(appId) });
        }
        _client.Send(msg);
    }

    // ---- Achievements ------------------------------------------------------

    public async Task<IReadOnlyList<AchievementInfo>> GetAchievementsAsync(uint appId, CancellationToken ct = default)
    {
        var stats = await _achievements.GetAchievementsAsync(appId, ct).ConfigureAwait(false);
        if (stats is null)
            return [];

        return stats
            .Where(s => s.ApiName is not null)
            .Select(s => new AchievementInfo(
                s.ApiName!,
                s.DisplayName ?? s.ApiName!,
                s.Description,
                s.Hidden,
                s.IsSet,
                s.Restricted,
                BuildIconUrl(appId, s.IsSet ? s.Icon : s.IconGray ?? s.Icon)))
            .ToList();
    }

    public Task<IReadOnlyList<UnlockResult>> SetAchievementsAsync(
        uint appId,
        IReadOnlyList<string> apiNames,
        CancellationToken ct = default)
        => _achievements.SetAchievementsAsync(appId, apiNames, set: true, ct);

    // ---- Owned games -------------------------------------------------------

    public Task<IReadOnlyList<GameInfo>> GetOwnedGamesAsync(CancellationToken ct = default)
        => OwnedGames.GetOwnedGamesAsync(_playerService, SteamId, ct);

    // ---- Helpers -----------------------------------------------------------

    private static string? BuildIconUrl(uint appId, string? icon)
        => string.IsNullOrEmpty(icon)
            ? null
            : $"https://cdn.steamstatic.com/steamcommunity/public/images/apps/{appId}/{icon}";

    private void SetStatus(AuthStatus status)
    {
        _status = status;
        AuthStatusChanged?.Invoke(status);
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync().ConfigureAwait(false);
        try
        {
            if (_client.IsConnected)
                _client.Disconnect();
        }
        catch
        {
            // ignore
        }
        _cts.Dispose();
    }
}
