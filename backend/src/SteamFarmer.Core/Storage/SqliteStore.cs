using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace SteamFarmer.Core.Storage;

/// <summary>SQLite-backed persistence for accounts, browser links, and farm jobs. All access is serialized.</summary>
public sealed class SqliteStore : IJobStore, IAccountStore, IDisposable
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly SqliteConnection _conn;
    private readonly object _sync = new();

    public SqliteStore(string dbPath)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(dbPath));
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        _conn = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
        }.ToString());
        _conn.Open();

        // Restrict the DB (which holds encrypted per-tenant tokens) to the owner on Linux.
        if (!OperatingSystem.IsWindows())
        {
            try
            {
                if (!string.IsNullOrEmpty(dir))
                    File.SetUnixFileMode(dir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                if (File.Exists(dbPath))
                    File.SetUnixFileMode(dbPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            catch
            {
                // best-effort hardening
            }
        }

        Exec("PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;");
        Exec("""
            CREATE TABLE IF NOT EXISTS accounts (
                steam_id INTEGER PRIMARY KEY,
                account_name TEXT NOT NULL,
                refresh_token_enc TEXT NOT NULL,
                persona TEXT,
                created_utc TEXT NOT NULL,
                last_seen_utc TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS browser_links (
                browser_session_id TEXT PRIMARY KEY,
                steam_id INTEGER NOT NULL,
                created_utc TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS jobs (
                id TEXT PRIMARY KEY,
                steam_id INTEGER NOT NULL,
                app_id INTEGER NOT NULL,
                game_name TEXT NOT NULL,
                hours_target REAL NOT NULL,
                jitter_pct REAL NOT NULL,
                seed INTEGER NOT NULL,
                created_utc TEXT NOT NULL,
                state INTEGER NOT NULL,
                accrued_seconds REAL NOT NULL,
                last_resumed_utc TEXT,
                schedule_json TEXT NOT NULL,
                error TEXT
            );
            CREATE INDEX IF NOT EXISTS ix_jobs_steam ON jobs(steam_id);
            """);
    }

    // ---- Jobs --------------------------------------------------------------

    public Task<IReadOnlyList<FarmJob>> GetAllAsync() => Task.FromResult(QueryJobs(null));

    public Task<IReadOnlyList<FarmJob>> GetByAccountAsync(ulong steamId) => Task.FromResult(QueryJobs(steamId));

    public Task<FarmJob?> GetAsync(string id)
    {
        lock (_sync)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM jobs WHERE id = $id";
            cmd.Parameters.AddWithValue("$id", id);
            using var r = cmd.ExecuteReader();
            return Task.FromResult(r.Read() ? ReadJob(r) : null);
        }
    }

    public Task UpsertAsync(FarmJob job)
    {
        lock (_sync)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO jobs (id, steam_id, app_id, game_name, hours_target, jitter_pct, seed, created_utc, state, accrued_seconds, last_resumed_utc, schedule_json, error)
                VALUES ($id,$steam,$app,$name,$hours,$jitter,$seed,$created,$state,$accrued,$resumed,$schedule,$error)
                ON CONFLICT(id) DO UPDATE SET
                    game_name=$name, hours_target=$hours, jitter_pct=$jitter, seed=$seed, state=$state,
                    accrued_seconds=$accrued, last_resumed_utc=$resumed, schedule_json=$schedule, error=$error;
                """;
            cmd.Parameters.AddWithValue("$id", job.Id);
            cmd.Parameters.AddWithValue("$steam", (long)job.SteamId);
            cmd.Parameters.AddWithValue("$app", job.AppId);
            cmd.Parameters.AddWithValue("$name", job.GameName);
            cmd.Parameters.AddWithValue("$hours", job.HoursTarget);
            cmd.Parameters.AddWithValue("$jitter", job.JitterPct);
            cmd.Parameters.AddWithValue("$seed", job.Seed);
            cmd.Parameters.AddWithValue("$created", Iso(job.CreatedAtUtc));
            cmd.Parameters.AddWithValue("$state", (int)job.State);
            cmd.Parameters.AddWithValue("$accrued", job.AccruedRunningSeconds);
            cmd.Parameters.AddWithValue("$resumed", (object?)(job.LastResumedAtUtc is { } r ? Iso(r) : null) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$schedule", JsonSerializer.Serialize(job.Schedule, Json));
            cmd.Parameters.AddWithValue("$error", (object?)job.Error ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string id)
    {
        lock (_sync)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM jobs WHERE id = $id";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }
        return Task.CompletedTask;
    }

    private IReadOnlyList<FarmJob> QueryJobs(ulong? steamId)
    {
        lock (_sync)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = steamId is null ? "SELECT * FROM jobs" : "SELECT * FROM jobs WHERE steam_id = $steam";
            if (steamId is { } s)
                cmd.Parameters.AddWithValue("$steam", (long)s);
            using var r = cmd.ExecuteReader();
            var list = new List<FarmJob>();
            while (r.Read())
                list.Add(ReadJob(r));
            return list;
        }
    }

    private static FarmJob ReadJob(SqliteDataReader r)
    {
        var schedule = JsonSerializer.Deserialize<List<ScheduledUnlock>>(r.GetString(r.GetOrdinal("schedule_json")), Json) ?? [];
        return new FarmJob
        {
            Id = r.GetString(r.GetOrdinal("id")),
            SteamId = (ulong)r.GetInt64(r.GetOrdinal("steam_id")),
            AppId = (uint)r.GetInt64(r.GetOrdinal("app_id")),
            GameName = r.GetString(r.GetOrdinal("game_name")),
            HoursTarget = r.GetDouble(r.GetOrdinal("hours_target")),
            JitterPct = r.GetDouble(r.GetOrdinal("jitter_pct")),
            Seed = r.GetInt32(r.GetOrdinal("seed")),
            CreatedAtUtc = ParseIso(r.GetString(r.GetOrdinal("created_utc"))),
            State = (JobState)r.GetInt32(r.GetOrdinal("state")),
            AccruedRunningSeconds = r.GetDouble(r.GetOrdinal("accrued_seconds")),
            LastResumedAtUtc = r.IsDBNull(r.GetOrdinal("last_resumed_utc")) ? null : ParseIso(r.GetString(r.GetOrdinal("last_resumed_utc"))),
            Error = r.IsDBNull(r.GetOrdinal("error")) ? null : r.GetString(r.GetOrdinal("error")),
            Schedule = schedule,
        };
    }

    // ---- Accounts ----------------------------------------------------------

    public Task<IReadOnlyList<StoredAccount>> GetAllAccountsAsync()
    {
        lock (_sync)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM accounts";
            using var r = cmd.ExecuteReader();
            var list = new List<StoredAccount>();
            while (r.Read())
                list.Add(ReadAccount(r));
            return Task.FromResult<IReadOnlyList<StoredAccount>>(list);
        }
    }

    public Task<StoredAccount?> GetAccountAsync(ulong steamId)
    {
        lock (_sync)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM accounts WHERE steam_id = $steam";
            cmd.Parameters.AddWithValue("$steam", (long)steamId);
            using var r = cmd.ExecuteReader();
            return Task.FromResult(r.Read() ? ReadAccount(r) : null);
        }
    }

    public Task UpsertAccountAsync(StoredAccount a)
    {
        lock (_sync)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO accounts (steam_id, account_name, refresh_token_enc, persona, created_utc, last_seen_utc)
                VALUES ($steam,$name,$token,$persona,$created,$seen)
                ON CONFLICT(steam_id) DO UPDATE SET
                    account_name=$name, refresh_token_enc=$token, persona=$persona, last_seen_utc=$seen;
                """;
            cmd.Parameters.AddWithValue("$steam", (long)a.SteamId);
            cmd.Parameters.AddWithValue("$name", a.AccountName);
            cmd.Parameters.AddWithValue("$token", a.RefreshTokenEnc);
            cmd.Parameters.AddWithValue("$persona", (object?)a.Persona ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$created", Iso(a.CreatedAtUtc));
            cmd.Parameters.AddWithValue("$seen", Iso(a.LastSeenUtc));
            cmd.ExecuteNonQuery();
        }
        return Task.CompletedTask;
    }

    public Task DeleteAccountAsync(ulong steamId)
    {
        lock (_sync)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM accounts WHERE steam_id=$steam; DELETE FROM jobs WHERE steam_id=$steam; DELETE FROM browser_links WHERE steam_id=$steam;";
            cmd.Parameters.AddWithValue("$steam", (long)steamId);
            cmd.ExecuteNonQuery();
        }
        return Task.CompletedTask;
    }

    private static StoredAccount ReadAccount(SqliteDataReader r) => new(
        (ulong)r.GetInt64(r.GetOrdinal("steam_id")),
        r.GetString(r.GetOrdinal("account_name")),
        r.GetString(r.GetOrdinal("refresh_token_enc")),
        r.IsDBNull(r.GetOrdinal("persona")) ? null : r.GetString(r.GetOrdinal("persona")),
        ParseIso(r.GetString(r.GetOrdinal("created_utc"))),
        ParseIso(r.GetString(r.GetOrdinal("last_seen_utc"))));

    // ---- Browser links -----------------------------------------------------

    public Task<IReadOnlyList<BrowserLink>> GetBrowserLinksAsync()
    {
        lock (_sync)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM browser_links";
            using var r = cmd.ExecuteReader();
            var list = new List<BrowserLink>();
            while (r.Read())
                list.Add(new BrowserLink(
                    r.GetString(r.GetOrdinal("browser_session_id")),
                    (ulong)r.GetInt64(r.GetOrdinal("steam_id")),
                    ParseIso(r.GetString(r.GetOrdinal("created_utc")))));
            return Task.FromResult<IReadOnlyList<BrowserLink>>(list);
        }
    }

    public Task UpsertBrowserLinkAsync(BrowserLink link)
    {
        lock (_sync)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO browser_links (browser_session_id, steam_id, created_utc)
                VALUES ($sid,$steam,$created)
                ON CONFLICT(browser_session_id) DO UPDATE SET steam_id=$steam;
                """;
            cmd.Parameters.AddWithValue("$sid", link.BrowserSessionId);
            cmd.Parameters.AddWithValue("$steam", (long)link.SteamId);
            cmd.Parameters.AddWithValue("$created", Iso(link.CreatedAtUtc));
            cmd.ExecuteNonQuery();
        }
        return Task.CompletedTask;
    }

    public Task DeleteBrowserLinkAsync(string browserSessionId)
    {
        lock (_sync)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM browser_links WHERE browser_session_id = $sid";
            cmd.Parameters.AddWithValue("$sid", browserSessionId);
            cmd.ExecuteNonQuery();
        }
        return Task.CompletedTask;
    }

    // ---- helpers -----------------------------------------------------------

    private void Exec(string sql)
    {
        lock (_sync)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }
    }

    private static string Iso(DateTimeOffset dt) => dt.ToUniversalTime().ToString("O");
    private static DateTimeOffset ParseIso(string s) => DateTimeOffset.Parse(s, null, System.Globalization.DateTimeStyles.RoundtripKind);

    public void Dispose() => _conn.Dispose();
}
