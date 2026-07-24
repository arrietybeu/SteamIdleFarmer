using SteamFarmer.Core.Abstractions;
using SteamFarmer.Core.Scheduling;
using SteamFarmer.Core.Storage;

namespace SteamFarmer.Core.Jobs;

/// <summary>
/// Orchestrates one Steam account's "idle to 100%" jobs: builds drip schedules, ticks them
/// (unlocking due achievements), keeps the account's "playing games" set in sync with running
/// jobs, and persists everything. One instance per account.
/// </summary>
public sealed class JobManager
{
    private readonly ISteamService _steam;
    private readonly IJobStore _store;
    private readonly ulong _steamId;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, FarmJob> _jobs = new();

    public JobManager(ISteamService steam, IJobStore store, ulong steamId, TimeProvider? clock = null)
    {
        _steam = steam;
        _store = store;
        _steamId = steamId;
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>Raised when an achievement is actually unlocked (for live UI/toasts).</summary>
    public event Action<FarmJob, ScheduledUnlock>? AchievementUnlocked;

    /// <summary>Raised whenever any job's state/progress changes.</summary>
    public event Action? JobsChanged;

    /// <summary>Load persisted jobs and resume running ones (downtime does not count toward progress).</summary>
    public async Task InitializeAsync()
    {
        var now = _clock.GetUtcNow();
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            _jobs.Clear();
            foreach (var job in await _store.GetByAccountAsync(_steamId).ConfigureAwait(false))
            {
                if (job.State == JobState.Running)
                    job.LastResumedAtUtc = now; // resume the timing span; accrued seconds already persisted
                _jobs[job.Id] = job;
            }
        }
        finally
        {
            _gate.Release();
        }

        await SyncPlayingAsync().ConfigureAwait(false);
        JobsChanged?.Invoke();
    }

    public async Task<IReadOnlyList<FarmJob>> CreateJobsAsync(
        IReadOnlyList<uint> appIds,
        double hoursPerGame,
        double jitterPct,
        IReadOnlyDictionary<uint, string>? names = null)
    {
        var now = _clock.GetUtcNow();
        var created = new List<FarmJob>();

        foreach (var appId in appIds.Distinct())
        {
            var achievements = await _steam.GetAchievementsAsync(appId).ConfigureAwait(false);
            var achievable = achievements
                .Where(a => a is { Protected: false, Unlocked: false })
                .Select(a => a.ApiName)
                .ToList();

            int seed = Random.Shared.Next();
            var job = new FarmJob
            {
                Id = Guid.NewGuid().ToString("N"),
                SteamId = _steamId,
                AppId = appId,
                GameName = names is not null && names.TryGetValue(appId, out var n) ? n : appId.ToString(),
                HoursTarget = hoursPerGame,
                JitterPct = jitterPct,
                Seed = seed,
                CreatedAtUtc = now,
                State = JobState.Running,
                AccruedRunningSeconds = 0,
                LastResumedAtUtc = now,
                Schedule = Scheduler.BuildSchedule(achievable, hoursPerGame, jitterPct, seed),
            };

            await _store.UpsertAsync(job).ConfigureAwait(false);

            await _gate.WaitAsync().ConfigureAwait(false);
            try { _jobs[job.Id] = job; }
            finally { _gate.Release(); }

            created.Add(job);
        }

        await SyncPlayingAsync().ConfigureAwait(false);
        JobsChanged?.Invoke();
        return created;
    }

    /// <summary>Advance all running jobs: unlock what's due, complete finished ones, resync idling.</summary>
    public async Task TickAsync()
    {
        var now = _clock.GetUtcNow();
        List<FarmJob> running;

        await _gate.WaitAsync().ConfigureAwait(false);
        try { running = _jobs.Values.Where(j => j.State == JobState.Running).ToList(); }
        finally { _gate.Release(); }

        bool anyChanged = false;

        foreach (var job in running)
        {
            bool jobChanged = false;
            var due = Scheduler.DueUnlocks(job, now);
            if (due.Count > 0)
            {
                var names = due.Select(d => d.ApiName).ToList();
                var results = await _steam.SetAchievementsAsync(job.AppId, names).ConfigureAwait(false);
                var byName = results.ToDictionary(r => r.ApiName, StringComparer.Ordinal);

                foreach (var unlock in due)
                {
                    if (!byName.TryGetValue(unlock.ApiName, out var res))
                        continue;

                    if (res.Ok)
                    {
                        unlock.Unlocked = true;
                        unlock.UnlockedAtUtc = now;
                        jobChanged = true;
                        AchievementUnlocked?.Invoke(job, unlock);
                    }
                    else if (res.Error is not null &&
                             (res.Error.Contains("protected", StringComparison.OrdinalIgnoreCase) ||
                              res.Error.Contains("unknown", StringComparison.OrdinalIgnoreCase)))
                    {
                        // Permanent failure — skip so we don't retry forever.
                        unlock.Unlocked = true;
                        unlock.UnlockedAtUtc = now;
                        job.Error = "Một số thành tựu không mở được (protected/không tồn tại) đã bị bỏ qua.";
                        jobChanged = true;
                    }
                    // else: transient — leave pending for the next tick.
                }
            }

            // Fold the elapsed span into AccruedRunningSeconds on EVERY tick. Without this the
            // running span only ever lives in memory, and a restart (which re-stamps
            // LastResumedAtUtc) throws it away — leaving a job with unlocked achievements but a
            // playtime counter reset toward zero, and pushing the remaining unlocks out.
            Scheduler.FoldActiveSpan(job, now);

            if (Scheduler.CurrentAccruedSeconds(job, now) >= job.HoursTarget * 3600.0 && Scheduler.IsComplete(job))
            {
                Scheduler.Pause(job, now);
                job.State = JobState.Completed;
                jobChanged = true;
            }

            // Always persist: the folded accrual has to survive a restart even when nothing unlocked.
            await _store.UpsertAsync(job).ConfigureAwait(false);
            anyChanged |= jobChanged;
        }

        if (anyChanged)
            await SyncPlayingAsync().ConfigureAwait(false);

        if (running.Count > 0)
            JobsChanged?.Invoke(); // keeps live progress flowing to the UI each tick
    }

    public Task PauseAsync(string jobId) => MutateAsync(jobId, job => Scheduler.Pause(job, _clock.GetUtcNow()));

    public Task ResumeAsync(string jobId) => MutateAsync(jobId, job =>
    {
        if (job.State is JobState.Paused)
            Scheduler.Resume(job, _clock.GetUtcNow());
    });

    public async Task DeleteAsync(string jobId)
    {
        bool removed;
        await _gate.WaitAsync().ConfigureAwait(false);
        try { removed = _jobs.Remove(jobId); }
        finally { _gate.Release(); }

        // Only this account's jobs live in _jobs; if the id wasn't ours, do NOT touch the shared
        // store — otherwise a caller could delete another account's job row (IDOR).
        if (!removed)
            return;

        await _store.DeleteAsync(jobId).ConfigureAwait(false);
        await SyncPlayingAsync().ConfigureAwait(false);
        JobsChanged?.Invoke();
    }

    public IReadOnlyList<FarmJob> Snapshot()
    {
        _gate.Wait();
        try { return _jobs.Values.ToList(); }
        finally { _gate.Release(); }
    }

    private async Task MutateAsync(string jobId, Action<FarmJob> mutate)
    {
        FarmJob? job;
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!_jobs.TryGetValue(jobId, out job))
                return;
            mutate(job);
        }
        finally
        {
            _gate.Release();
        }

        await _store.UpsertAsync(job).ConfigureAwait(false);
        await SyncPlayingAsync().ConfigureAwait(false);
        JobsChanged?.Invoke();
    }

    /// <summary>Tell Steam to "play" exactly the set of games with running jobs (max 32).</summary>
    private async Task SyncPlayingAsync()
    {
        List<uint> playing;
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            playing = _jobs.Values
                .Where(j => j.State == JobState.Running)
                .Select(j => j.AppId)
                .Distinct()
                .Take(32)
                .ToList();
        }
        finally
        {
            _gate.Release();
        }

        await _steam.SetPlayingGamesAsync(playing).ConfigureAwait(false);
    }
}
