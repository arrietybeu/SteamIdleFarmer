using SteamFarmer.Core;
using SteamFarmer.Core.Abstractions;
using SteamFarmer.Core.Jobs;
using SteamFarmer.Core.Scheduling;
using SteamFarmer.Core.Storage;

namespace SteamFarmer.Core.Tests;

public class JobManagerTests
{
    private sealed class TestClock(DateTimeOffset start) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = start;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class FakeSteam : ISteamService
    {
        public AuthStatus Status { get; } = new(AuthState.LoggedIn, "tester", 123, null);
        public event Action<AuthStatus>? AuthStatusChanged;
        public event Action<string>? ChallengeUrlChanged;

        public readonly Dictionary<uint, List<AchievementInfo>> Catalog = new();
        public readonly List<(uint appId, IReadOnlyList<string> names)> SetCalls = new();
        public IReadOnlyCollection<uint> LastPlaying = [];

        public Task<QrChallenge> BeginQrLoginAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task LogoutAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<GameInfo>> GetOwnedGamesAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<GameInfo>>([]);

        public Task<IReadOnlyList<AchievementInfo>> GetAchievementsAsync(uint appId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<AchievementInfo>>(Catalog.TryGetValue(appId, out var l) ? l : []);

        public Task<IReadOnlyList<UnlockResult>> SetAchievementsAsync(uint appId, IReadOnlyList<string> apiNames, CancellationToken ct = default)
        {
            SetCalls.Add((appId, apiNames));
            _ = AuthStatusChanged; _ = ChallengeUrlChanged; // silence unused-event warnings
            return Task.FromResult<IReadOnlyList<UnlockResult>>(apiNames.Select(n => new UnlockResult(n, true, null)).ToList());
        }

        public Task SetPlayingGamesAsync(IReadOnlyCollection<uint> appIds, CancellationToken ct = default)
        {
            LastPlaying = appIds;
            return Task.CompletedTask;
        }
    }

    private static AchievementInfo Ach(string name, bool unlocked = false, bool prot = false)
        => new(name, name, null, false, unlocked, prot, null);

    [Fact]
    public async Task CreateJob_ExcludesProtectedAndUnlocked_FromSchedule()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var steam = new FakeSteam();
        steam.Catalog[100] =
        [
            Ach("A"), Ach("B"), Ach("C"), Ach("D"),
            Ach("LOCKED_PROT", prot: true),
            Ach("ALREADY", unlocked: true),
        ];
        var store = new InMemoryStore();
        var jm = new JobManager(steam, store, steamId: 123, clock);

        var jobs = await jm.CreateJobsAsync([100], hoursPerGame: 10, jitterPct: 0);

        Assert.Single(jobs);
        Assert.Equal(4, jobs[0].Schedule.Count); // only A,B,C,D are achievable
        Assert.Contains(100u, steam.LastPlaying);  // running job -> idling that game
    }

    [Fact]
    public async Task Tick_UnlocksDueAchievements_AndCompletesAtTarget()
    {
        var t0 = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var clock = new TestClock(t0);
        var steam = new FakeSteam();
        steam.Catalog[100] = [Ach("A"), Ach("B"), Ach("C"), Ach("D")];
        var store = new InMemoryStore();
        var jm = new JobManager(steam, store, 123, clock);

        var job = (await jm.CreateJobsAsync([100], hoursPerGame: 10, jitterPct: 0))[0];
        // base gap = 10h/4 = 2.5h -> due at 2.5, 5, 7.5, 10h.

        // Nothing due yet at 1h.
        clock.Now = t0.AddHours(1);
        await jm.TickAsync();
        Assert.Equal(0, job.Schedule.Count(s => s.Unlocked));

        // At 2.6h the first unlock (2.5h) is due.
        clock.Now = t0.AddHours(2.6);
        await jm.TickAsync();
        Assert.Equal(1, job.Schedule.Count(s => s.Unlocked));
        Assert.Single(steam.SetCalls);

        // At 7.6h three unlocks (2.5, 5, 7.5h) should be done.
        clock.Now = t0.AddHours(7.6);
        await jm.TickAsync();
        Assert.Equal(3, job.Schedule.Count(s => s.Unlocked));

        // At 10.1h everything is unlocked and the job completes; idling stops.
        clock.Now = t0.AddHours(10.1);
        await jm.TickAsync();
        Assert.Equal(4, job.Schedule.Count(s => s.Unlocked));
        Assert.Equal(JobState.Completed, job.State);
        Assert.DoesNotContain(100u, steam.LastPlaying);
    }

    [Fact]
    public async Task Pause_FreezesProgress_AndPersists()
    {
        var t0 = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var clock = new TestClock(t0);
        var steam = new FakeSteam();
        steam.Catalog[100] = [Ach("A"), Ach("B")];
        var store = new InMemoryStore();
        var jm = new JobManager(steam, store, 123, clock);

        var job = (await jm.CreateJobsAsync([100], hoursPerGame: 10, jitterPct: 0))[0];

        clock.Now = t0.AddHours(2);
        await jm.PauseAsync(job.Id);
        Assert.Equal(JobState.Paused, job.State);
        double accruedAtPause = Scheduler.CurrentAccruedSeconds(job, clock.Now);

        // 100h of wall-clock passes while paused — progress must not move.
        clock.Now = t0.AddHours(102);
        Assert.Equal(accruedAtPause, Scheduler.CurrentAccruedSeconds(job, clock.Now), 3);
        Assert.DoesNotContain(100u, steam.LastPlaying); // paused -> not idling

        // Persisted state reflects the pause.
        var reloaded = await store.GetAsync(job.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(JobState.Paused, reloaded!.State);
    }

    [Fact]
    public async Task Initialize_ResumesRunningJobs_WithoutCountingDowntime()
    {
        var t0 = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var store = new InMemoryStore();

        // A job that had accrued 1h of running time, persisted while "Running".
        var seedJob = new FarmJob
        {
            Id = "job1",
            SteamId = 123,
            AppId = 100,
            GameName = "G",
            HoursTarget = 10,
            JitterPct = 0,
            Seed = 1,
            CreatedAtUtc = t0,
            State = JobState.Running,
            AccruedRunningSeconds = 3600,
            LastResumedAtUtc = t0, // stale; Initialize should re-stamp to "now"
            Schedule = Scheduler.BuildSchedule(["A", "B"], 10, 0, 1),
        };
        await store.UpsertAsync(seedJob);

        var clock = new TestClock(t0.AddDays(5)); // 5 days of downtime
        var steam = new FakeSteam();
        steam.Catalog[100] = [Ach("A"), Ach("B")];
        var jm = new JobManager(steam, store, 123, clock);

        await jm.InitializeAsync();

        var job = jm.Snapshot().Single();
        // Downtime is NOT counted: accrued stays ~1h, not 1h+5days.
        Assert.Equal(3600, Scheduler.CurrentAccruedSeconds(job, clock.Now), 1);
        Assert.Contains(100u, steam.LastPlaying); // running job resumes idling
    }
}
