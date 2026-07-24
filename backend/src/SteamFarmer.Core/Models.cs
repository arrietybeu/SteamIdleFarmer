namespace SteamFarmer.Core;

/// <summary>A game the account owns.</summary>
public sealed record GameInfo(
    uint AppId,
    string Name,
    string? IconUrl,
    double PlaytimeHours,
    bool HasStats);

/// <summary>A single achievement definition + current state for the logged-in user.</summary>
public sealed record AchievementInfo(
    string ApiName,
    string DisplayName,
    string? Description,
    bool Hidden,
    bool Unlocked,
    bool Protected,
    string? IconUrl);

public enum JobState
{
    Pending,
    Running,
    Paused,
    Completed,
    Failed,
}

/// <summary>One scheduled achievement unlock, timed in accrued running-seconds since the job started.</summary>
public sealed class ScheduledUnlock
{
    public required string ApiName { get; init; }

    /// <summary>When this unlock is due, measured in accumulated running-time (seconds), NOT wall-clock.</summary>
    public required double DueAtRunningSeconds { get; init; }

    public bool Unlocked { get; set; }
    public DateTimeOffset? UnlockedAtUtc { get; set; }
}

/// <summary>
/// An "idle to 100%" job for a single game: idle the game while dripping its unlockable
/// achievements evenly across <see cref="HoursTarget"/> hours of running time.
/// </summary>
public sealed class FarmJob
{
    public required string Id { get; init; }

    /// <summary>The Steam account this job belongs to (multi-tenant scoping).</summary>
    public ulong SteamId { get; init; }

    public required uint AppId { get; init; }
    public required string GameName { get; set; }

    /// <summary>Target running-hours over which achievements are spread. Default 200.</summary>
    public required double HoursTarget { get; init; }

    /// <summary>Random spacing jitter as a percentage of the base gap (0..100).</summary>
    public required double JitterPct { get; init; }

    /// <summary>Seed used to build the schedule deterministically (kept for reference/resume).</summary>
    public required int Seed { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; }
    public JobState State { get; set; } = JobState.Pending;

    /// <summary>Running-time already accrued while the job was in the Running state, excluding the current active span.</summary>
    public double AccruedRunningSeconds { get; set; }

    /// <summary>UTC instant the job last entered the Running state; null when not running.</summary>
    public DateTimeOffset? LastResumedAtUtc { get; set; }

    public List<ScheduledUnlock> Schedule { get; init; } = [];

    public string? Error { get; set; }
}
