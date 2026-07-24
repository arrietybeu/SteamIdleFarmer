using SteamFarmer.Core;
using SteamFarmer.Core.Scheduling;

namespace SteamFarmer.Core.Tests;

public class SchedulerTests
{
    private static List<string> Names(int n) => Enumerable.Range(0, n).Select(i => $"ACH_{i}").ToList();

    [Fact]
    public void BuildSchedule_ReturnsOnePerAchievement()
    {
        var s = Scheduler.BuildSchedule(Names(10), hoursTarget: 200, jitterPct: 10, seed: 1);
        Assert.Equal(10, s.Count);
        Assert.Equal(10, s.Select(x => x.ApiName).Distinct().Count());
    }

    [Fact]
    public void BuildSchedule_Empty_ReturnsEmpty()
    {
        var s = Scheduler.BuildSchedule([], hoursTarget: 200, jitterPct: 10, seed: 1);
        Assert.Empty(s);
    }

    [Fact]
    public void BuildSchedule_InvalidHours_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Scheduler.BuildSchedule(Names(5), hoursTarget: 0, jitterPct: 0, seed: 1));
    }

    [Fact]
    public void BuildSchedule_AllDueWithinTarget_AndLastEqualsTarget()
    {
        double target = 200;
        double totalSeconds = target * 3600;
        var s = Scheduler.BuildSchedule(Names(37), hoursTarget: target, jitterPct: 25, seed: 42);

        Assert.All(s, u =>
        {
            Assert.True(u.DueAtRunningSeconds > 0);
            Assert.True(u.DueAtRunningSeconds <= totalSeconds + 1e-6);
        });
        // Schedule is sorted ascending; the final unlock lands exactly at the target.
        Assert.Equal(totalSeconds, s[^1].DueAtRunningSeconds, 3);
        // Monotonic non-decreasing.
        for (int i = 1; i < s.Count; i++)
            Assert.True(s[i].DueAtRunningSeconds >= s[i - 1].DueAtRunningSeconds);
    }

    [Fact]
    public void BuildSchedule_ZeroJitter_IsEvenlySpaced()
    {
        int n = 20;
        double target = 200;
        double baseGap = target * 3600 / n;
        var s = Scheduler.BuildSchedule(Names(n), hoursTarget: target, jitterPct: 0, seed: 7);

        for (int i = 0; i < n; i++)
            Assert.Equal(baseGap * (i + 1), s[i].DueAtRunningSeconds, 3);
    }

    [Fact]
    public void BuildSchedule_IsDeterministicForSameSeed()
    {
        var a = Scheduler.BuildSchedule(Names(30), 200, 15, seed: 123);
        var b = Scheduler.BuildSchedule(Names(30), 200, 15, seed: 123);

        Assert.Equal(a.Count, b.Count);
        for (int i = 0; i < a.Count; i++)
        {
            Assert.Equal(a[i].ApiName, b[i].ApiName);
            Assert.Equal(a[i].DueAtRunningSeconds, b[i].DueAtRunningSeconds, 6);
        }
    }

    [Fact]
    public void BuildSchedule_JitterStaysWithinBounds()
    {
        int n = 50;
        double target = 100;
        double baseGap = target * 3600 / n;
        double jitterPct = 20;
        double maxJitter = jitterPct / 100.0 * baseGap;
        var s = Scheduler.BuildSchedule(Names(n), target, jitterPct, seed: 99);

        // Reconstruct: after sorting we can't tie a unlock to a slot, but every due must be
        // within [1, total] and no due may exceed (slotMax = n*baseGap + maxJitter).
        double totalSeconds = target * 3600;
        Assert.All(s, u =>
        {
            Assert.InRange(u.DueAtRunningSeconds, 1.0, totalSeconds + maxJitter + 1e-6);
        });
    }

    [Fact]
    public void CurrentAccrued_PausedJob_ReturnsStoredAccrual()
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var job = MakeJob();
        job.State = JobState.Paused;
        job.AccruedRunningSeconds = 3600;
        job.LastResumedAtUtc = null;

        Assert.Equal(3600, Scheduler.CurrentAccruedSeconds(job, now.AddHours(5)));
    }

    [Fact]
    public void CurrentAccrued_RunningJob_AddsActiveSpan()
    {
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var job = MakeJob();
        job.AccruedRunningSeconds = 100;
        Scheduler.Resume(job, start);

        Assert.Equal(100 + 50, Scheduler.CurrentAccruedSeconds(job, start.AddSeconds(50)));
    }

    [Fact]
    public void PauseResume_DoesNotCountDowntime()
    {
        var t0 = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var job = MakeJob();

        Scheduler.Resume(job, t0);
        Scheduler.Pause(job, t0.AddSeconds(10));          // ran 10s
        Assert.Equal(10, job.AccruedRunningSeconds, 6);
        Assert.Equal(JobState.Paused, job.State);

        // 90s of downtime while paused, then resume and run 5 more seconds.
        Scheduler.Resume(job, t0.AddSeconds(100));
        Assert.Equal(15, Scheduler.CurrentAccruedSeconds(job, t0.AddSeconds(105)), 6);
    }

    [Fact]
    public void FoldActiveSpan_PreservesAccrualWithoutChangingState()
    {
        var t0 = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var job = MakeJob();
        Scheduler.Resume(job, t0);

        Scheduler.FoldActiveSpan(job, t0.AddSeconds(30));
        Assert.Equal(30, job.AccruedRunningSeconds, 6);
        Assert.Equal(JobState.Running, job.State);
        // No double count: folding again immediately adds ~0.
        Scheduler.FoldActiveSpan(job, t0.AddSeconds(30));
        Assert.Equal(30, job.AccruedRunningSeconds, 6);
    }

    [Fact]
    public void DueUnlocks_ReturnsPastDuePending()
    {
        var t0 = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var job = MakeJob(hoursTarget: 10, n: 10, jitter: 0); // gaps of 3600s
        Scheduler.Resume(job, t0);

        // After 2h05m of running time, the first two unlocks (at 1h and 2h) are due.
        var due = Scheduler.DueUnlocks(job, t0.AddMinutes(125));
        Assert.Equal(2, due.Count);
    }

    [Fact]
    public void DueUnlocks_ExcludesAlreadyUnlocked()
    {
        var t0 = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var job = MakeJob(hoursTarget: 10, n: 10, jitter: 0);
        Scheduler.Resume(job, t0);
        job.Schedule[0].Unlocked = true;

        var due = Scheduler.DueUnlocks(job, t0.AddMinutes(125));
        Assert.Single(due);
    }

    [Fact]
    public void NextPending_And_SecondsUntil()
    {
        var t0 = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var job = MakeJob(hoursTarget: 10, n: 10, jitter: 0);
        Scheduler.Resume(job, t0);

        var next = Scheduler.NextPending(job);
        Assert.NotNull(next);
        Assert.Equal(3600, next!.DueAtRunningSeconds, 3);

        // 600s in, 3000s remain until first unlock.
        Assert.Equal(3000, Scheduler.SecondsUntilNextUnlock(job, t0.AddSeconds(600))!.Value, 3);
    }

    [Fact]
    public void IsComplete_TrueWhenAllUnlocked()
    {
        var job = MakeJob(hoursTarget: 10, n: 3, jitter: 0);
        Assert.False(Scheduler.IsComplete(job));
        foreach (var u in job.Schedule) u.Unlocked = true;
        Assert.True(Scheduler.IsComplete(job));
    }

    private static FarmJob MakeJob(double hoursTarget = 200, int n = 10, double jitter = 10, int seed = 1)
    {
        var job = new FarmJob
        {
            Id = Guid.NewGuid().ToString("N"),
            AppId = 1623730,
            GameName = "Test Game",
            HoursTarget = hoursTarget,
            JitterPct = jitter,
            Seed = seed,
            CreatedAtUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        };
        job.Schedule.AddRange(Scheduler.BuildSchedule(Names(n), hoursTarget, jitter, seed));
        return job;
    }
}
