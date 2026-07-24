using SteamFarmer.Core;
using SteamFarmer.Core.Abstractions;
using SteamFarmer.Core.Scheduling;

namespace SteamFarmer.Api;

// ---- Response DTOs (serialized camelCase by ASP.NET web defaults) ----

public record AuthStatusDto(string state, string? persona, string? steamId, string? error);

public record GameDto(uint appId, string name, string? iconUrl, double playtimeHours, bool hasStats);

public record AchievementDto(
    string apiName,
    string displayName,
    string? description,
    bool hidden,
    bool unlocked,
    bool @protected,
    string? iconUrl);

public record JobDto(
    string id,
    uint appId,
    string name,
    string state,
    double hoursTarget,
    double hoursElapsed,
    int totalAchievable,
    int unlockedCount,
    double? nextUnlockInSec);

public record UnlockResultDto(string apiName, bool ok, string? error);

// ---- Request bodies ----

public record UnlockRequest(uint appId, string[] apiNames);
public record CreateJobsRequest(uint[] appIds, double hoursPerGame, double? jitterPct);
public record AccessRequest(string password);

public static class Mapping
{
    public static string AuthStateName(AuthState s) => s switch
    {
        AuthState.Disconnected => "disconnected",
        AuthState.AwaitingQr => "awaiting_qr",
        AuthState.LoggingIn => "logging_in",
        AuthState.LoggedIn => "logged_in",
        AuthState.Error => "error",
        _ => "disconnected",
    };

    public static AuthStatusDto Auth(AuthStatus s)
        => new(AuthStateName(s.State), s.Persona, s.SteamId?.ToString(), s.Error);

    public static AchievementDto Achievement(AchievementInfo a)
        => new(a.ApiName, a.DisplayName, a.Description, a.Hidden, a.Unlocked, a.Protected, a.IconUrl);

    public static GameDto Game(GameInfo g)
        => new(g.AppId, g.Name, g.IconUrl, g.PlaytimeHours, g.HasStats);

    public static JobDto Job(FarmJob j, DateTimeOffset now)
        => new(
            j.Id,
            j.AppId,
            j.GameName,
            j.State.ToString().ToLowerInvariant(),
            j.HoursTarget,
            Math.Round(Scheduler.CurrentAccruedSeconds(j, now) / 3600.0, 4),
            j.Schedule.Count,
            j.Schedule.Count(s => s.Unlocked),
            Scheduler.SecondsUntilNextUnlock(j, now));
}
