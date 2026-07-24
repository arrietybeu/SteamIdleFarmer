namespace SteamFarmer.Core.Abstractions;

public enum AuthState
{
    Disconnected,
    AwaitingQr,
    LoggingIn,
    LoggedIn,
    Error,
}

public sealed record AuthStatus(AuthState State, string? Persona, ulong? SteamId, string? Error);

/// <summary>A QR login challenge. Render <see cref="ChallengeUrl"/> as a QR code for the Steam Mobile app to scan.</summary>
public sealed record QrChallenge(string ChallengeUrl, string SessionId);

public sealed record UnlockResult(string ApiName, bool Ok, string? Error);

/// <summary>
/// Facade over the Steam network layer. The concrete implementation is built on SteamKit2;
/// the rest of the app depends only on this interface so it can be tested with fakes.
/// </summary>
public interface ISteamService
{
    AuthStatus Status { get; }

    /// <summary>Raised whenever the authentication/connection state changes.</summary>
    event Action<AuthStatus>? AuthStatusChanged;

    /// <summary>Raised when the QR challenge URL rotates (Steam refreshes it periodically before scan).</summary>
    event Action<string>? ChallengeUrlChanged;

    /// <summary>Begin (or restart) a QR login. Returns the first challenge URL to display.</summary>
    Task<QrChallenge> BeginQrLoginAsync(CancellationToken ct = default);

    Task LogoutAsync(CancellationToken ct = default);

    Task<IReadOnlyList<GameInfo>> GetOwnedGamesAsync(CancellationToken ct = default);

    Task<IReadOnlyList<AchievementInfo>> GetAchievementsAsync(uint appId, CancellationToken ct = default);

    /// <summary>Unlock the given achievements. Protected/unsettable ones come back with Ok=false.</summary>
    Task<IReadOnlyList<UnlockResult>> SetAchievementsAsync(
        uint appId,
        IReadOnlyList<string> apiNames,
        CancellationToken ct = default);

    /// <summary>Set the complete set of appIds the account is currently "playing" (idle). Empty stops idling.</summary>
    Task SetPlayingGamesAsync(IReadOnlyCollection<uint> appIds, CancellationToken ct = default);
}
