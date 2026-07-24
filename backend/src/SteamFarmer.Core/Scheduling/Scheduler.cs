namespace SteamFarmer.Core.Scheduling;

/// <summary>
/// Pure scheduling logic for the "idle to 100%" drip. All timing is expressed in
/// <b>accrued running-seconds</b> (time the job actually spent Running), never wall-clock —
/// so downtime and pauses freeze progress, matching the fact that Steam playtime only
/// accrues while the account is online and idling.
/// </summary>
public static class Scheduler
{
    /// <summary>
    /// Build the drip schedule for a set of unlockable achievements (protected and
    /// already-unlocked ones must be filtered out by the caller before calling this).
    /// Each achievement is assigned a due offset in running-seconds, evenly spaced across
    /// <paramref name="hoursTarget"/> with ±<paramref name="jitterPct"/>% jitter. The final
    /// unlock lands exactly at the target so the game reaches 100% right on time.
    /// Deterministic for a given <paramref name="seed"/>.
    /// </summary>
    public static List<ScheduledUnlock> BuildSchedule(
        IReadOnlyList<string> achievableApiNames,
        double hoursTarget,
        double jitterPct,
        int seed)
    {
        ArgumentNullException.ThrowIfNull(achievableApiNames);
        if (hoursTarget <= 0)
            throw new ArgumentOutOfRangeException(nameof(hoursTarget), "hoursTarget must be > 0.");

        var result = new List<ScheduledUnlock>(achievableApiNames.Count);
        if (achievableApiNames.Count == 0)
            return result;

        double totalSeconds = hoursTarget * 3600.0;
        int n = achievableApiNames.Count;
        double baseGap = totalSeconds / n;
        double jitterFrac = Math.Clamp(jitterPct, 0, 100) / 100.0;

        var rng = new Random(seed);

        // Deterministic Fisher–Yates shuffle so which achievement fires in which slot is randomized.
        var order = achievableApiNames.ToArray();
        for (int i = order.Length - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (order[i], order[j]) = (order[j], order[i]);
        }

        for (int i = 0; i < n; i++)
        {
            double center = baseGap * (i + 1);
            double jitter = (rng.NextDouble() * 2.0 - 1.0) * jitterFrac * baseGap;
            double due = i == n - 1
                ? totalSeconds                      // guarantee 100% exactly at target
                : Math.Clamp(center + jitter, 1.0, totalSeconds);

            result.Add(new ScheduledUnlock
            {
                ApiName = order[i],
                DueAtRunningSeconds = due,
            });
        }

        // Order by due time so "next pending" and progress reads are monotonic even if jitter reordered neighbours.
        result.Sort(static (a, b) => a.DueAtRunningSeconds.CompareTo(b.DueAtRunningSeconds));
        return result;
    }

    /// <summary>Total running-seconds accrued as of <paramref name="nowUtc"/>, including the current active span if Running.</summary>
    public static double CurrentAccruedSeconds(FarmJob job, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(job);
        double active = 0;
        if (job.State == JobState.Running && job.LastResumedAtUtc is { } resumed)
            active = Math.Max(0, (nowUtc - resumed).TotalSeconds);
        return job.AccruedRunningSeconds + active;
    }

    /// <summary>Achievements whose due time has passed and that are not yet unlocked.</summary>
    public static IReadOnlyList<ScheduledUnlock> DueUnlocks(FarmJob job, DateTimeOffset nowUtc)
    {
        double accrued = CurrentAccruedSeconds(job, nowUtc);
        return job.Schedule
            .Where(s => !s.Unlocked && s.DueAtRunningSeconds <= accrued)
            .OrderBy(s => s.DueAtRunningSeconds)
            .ToList();
    }

    /// <summary>The next achievement waiting to unlock, or null if all are done.</summary>
    public static ScheduledUnlock? NextPending(FarmJob job)
        => job.Schedule
            .Where(s => !s.Unlocked)
            .OrderBy(s => s.DueAtRunningSeconds)
            .FirstOrDefault();

    /// <summary>Seconds of running-time until the next unlock is due (0 if already due, null if none pending).</summary>
    public static double? SecondsUntilNextUnlock(FarmJob job, DateTimeOffset nowUtc)
    {
        var next = NextPending(job);
        if (next is null)
            return null;
        double accrued = CurrentAccruedSeconds(job, nowUtc);
        return Math.Max(0, next.DueAtRunningSeconds - accrued);
    }

    public static bool IsComplete(FarmJob job) => job.Schedule.All(s => s.Unlocked);

    /// <summary>Transition the job into the Running state, starting a new active timing span.</summary>
    public static void Resume(FarmJob job, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(job);
        if (job.State == JobState.Running)
            return;
        job.State = JobState.Running;
        job.LastResumedAtUtc = nowUtc;
    }

    /// <summary>Fold the current active span into accrued time and stop counting (Paused).</summary>
    public static void Pause(FarmJob job, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(job);
        FoldActiveSpan(job, nowUtc);
        if (job.State == JobState.Running)
            job.State = JobState.Paused;
    }

    /// <summary>
    /// Fold the current active running span into <see cref="FarmJob.AccruedRunningSeconds"/> without
    /// changing state. Used when persisting a running job so a crash/restart does not lose or
    /// double-count the active span (the caller re-stamps LastResumedAtUtc on resume).
    /// </summary>
    public static void FoldActiveSpan(FarmJob job, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(job);
        if (job.State == JobState.Running && job.LastResumedAtUtc is { } resumed)
        {
            job.AccruedRunningSeconds += Math.Max(0, (nowUtc - resumed).TotalSeconds);
            job.LastResumedAtUtc = nowUtc;
        }
    }
}
