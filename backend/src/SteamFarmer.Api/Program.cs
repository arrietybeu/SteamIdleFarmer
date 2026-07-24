using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using SteamFarmer.Api;
using SteamFarmer.Core.Security;
using SteamFarmer.Core.Sessions;
using SteamFarmer.Core.Storage;

var builder = WebApplication.CreateBuilder(args);

// ---- Configuration (env) ----
string dataDir = Environment.GetEnvironmentVariable("FARMER_DATA_DIR") ?? "data";
string dbPath = Path.Combine(dataDir, "farmer.db");
string? secret = Environment.GetEnvironmentVariable("FARMER_SECRET");
if (string.IsNullOrWhiteSpace(secret)) secret = null;
string? accessPassword = Environment.GetEnvironmentVariable("FARMER_ACCESS_PASSWORD");
if (string.IsNullOrWhiteSpace(accessPassword)) accessPassword = null; // empty env var (compose ":-") = gate disabled
string deviceName = Environment.GetEnvironmentVariable("FARMER_DEVICE_NAME") ?? "SteamIdleFarmer";
int port = int.TryParse(Environment.GetEnvironmentVariable("PORT"), out var p) ? p : 5080;

builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
builder.Logging.AddSimpleConsole(o => o.SingleLine = true);

// ---- Services ----
var store = new SqliteStore(dbPath);
byte[] salt = LoadOrCreateSalt(Path.Combine(dataDir, "farmer.salt"));
ITokenProtector protector = secret is null
    ? new NullTokenProtector()
    : new AesTokenProtector(secret, salt);

builder.Services.AddSingleton<IJobStore>(store);
builder.Services.AddSingleton<IAccountStore>(store);
builder.Services.AddSingleton<ITokenProtector>(protector);
builder.Services.AddSingleton(sp => new SessionManager(
    sp.GetRequiredService<IAccountStore>(),
    sp.GetRequiredService<IJobStore>(),
    sp.GetRequiredService<ITokenProtector>(),
    deviceName));

// Honor X-Forwarded-Proto from the recommended TLS-terminating reverse proxy so cookies get Secure.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
});

// Per-IP rate limiting: a generous global cap, plus a strict cap on the expensive QR-start endpoint.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 300, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("qr-start", ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 6, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

var app = builder.Build();

if (string.IsNullOrEmpty(secret))
    app.Logger.LogWarning("FARMER_SECRET not set — refresh tokens are stored WITHOUT encryption. Set it in production.");

var sessions = app.Services.GetRequiredService<SessionManager>();
await sessions.ResumeAllAsync();

app.UseForwardedHeaders();
app.UseRateLimiter();
app.UseWebSockets();

// Security response headers (clickjacking + MIME-sniffing defense). CSP kept minimal
// (frame-ancestors only) so it can't break the SPA's Steam-CDN images / bundled assets.
app.Use(async (ctx, next) =>
{
    var h = ctx.Response.Headers;
    h["X-Frame-Options"] = "DENY";
    h["X-Content-Type-Options"] = "nosniff";
    h["Referrer-Policy"] = "no-referrer";
    h["Content-Security-Policy"] = "frame-ancestors 'none'";
    await next();
});

// ---- Per-browser session cookie ----
app.Use(async (ctx, next) =>
{
    if (!ctx.Request.Cookies.TryGetValue("sid", out var sid) || string.IsNullOrWhiteSpace(sid))
    {
        sid = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        ctx.Response.Cookies.Append("sid", sid, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = ctx.Request.IsHttps,
            MaxAge = TimeSpan.FromDays(365),
            Path = "/",
        });
    }
    ctx.Items["sid"] = sid;
    await next();
});

// ---- Optional site access gate (protects the VPS when shared publicly) ----
string? accessToken = accessPassword is null ? null : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(accessPassword)));
if (accessToken is not null)
{
    app.Use(async (ctx, next) =>
    {
        var path = ctx.Request.Path;
        bool guarded = path.StartsWithSegments("/api") || path.StartsWithSegments("/ws");
        bool isAccessEndpoint = path.StartsWithSegments("/api/access");
        if (guarded && !isAccessEndpoint)
        {
            if (!ctx.Request.Cookies.TryGetValue("farmer_access", out var tok) || tok is null ||
                !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(tok), Encoding.UTF8.GetBytes(accessToken)))
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await ctx.Response.WriteAsJsonAsync(new { error = "access_required" });
                return;
            }
        }
        await next();
    });
}

AccountSession Session(HttpContext ctx) => sessions.GetOrCreate((string)ctx.Items["sid"]!);
AccountSession? SessionOrNull(HttpContext ctx) => sessions.Find((string)ctx.Items["sid"]!);

// ---- Access endpoint ----
app.MapPost("/api/access", (AccessRequest req, HttpContext ctx) =>
{
    if (accessToken is null)
        return Results.Ok(); // gate disabled
    if (!CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(req.password)), Convert.FromHexString(accessToken)))
        return Results.Unauthorized();
    ctx.Response.Cookies.Append("farmer_access", accessToken, new CookieOptions
    {
        HttpOnly = true,
        SameSite = SameSiteMode.Lax,
        Secure = ctx.Request.IsHttps,
        MaxAge = TimeSpan.FromDays(30),
        Path = "/",
    });
    return Results.Ok();
});

// ---- Auth ----
app.MapGet("/api/auth/status", (HttpContext ctx) =>
{
    // Find (never create) so unauthenticated status polling can't spawn sessions.
    var status = SessionOrNull(ctx)?.Status
        ?? new SteamFarmer.Core.Abstractions.AuthStatus(SteamFarmer.Core.Abstractions.AuthState.Disconnected, null, null, null);
    return Results.Ok(Mapping.Auth(status));
});

app.MapPost("/api/auth/qr/start", async (HttpContext ctx) =>
{
    var challenge = await Session(ctx).BeginQrLoginAsync(ctx.RequestAborted);
    return Results.Ok(new { challengeUrl = challenge.ChallengeUrl, sessionId = challenge.SessionId });
}).RequireRateLimiting("qr-start");

app.MapPost("/api/auth/logout", async (HttpContext ctx) =>
{
    await Session(ctx).LogoutAsync();
    return Results.Ok();
});

// ---- Games & achievements ----
app.MapGet("/api/games", async (HttpContext ctx) =>
{
    var session = Session(ctx);
    if (!IsLoggedIn(session))
        return Results.Unauthorized();
    var games = await session.Steam.GetOwnedGamesAsync(ctx.RequestAborted);
    return Results.Ok(games.Select(Mapping.Game));
});

app.MapGet("/api/games/{appId:long}/achievements", async (long appId, HttpContext ctx) =>
{
    var session = Session(ctx);
    if (!IsLoggedIn(session))
        return Results.Unauthorized();
    var list = await session.Steam.GetAchievementsAsync((uint)appId, ctx.RequestAborted);
    return Results.Ok(list.Select(Mapping.Achievement));
});

app.MapPost("/api/achievements/unlock", async (UnlockRequest req, HttpContext ctx) =>
{
    var session = Session(ctx);
    if (!IsLoggedIn(session))
        return Results.Unauthorized();
    var results = await session.Steam.SetAchievementsAsync(req.appId, req.apiNames, ctx.RequestAborted);
    return Results.Ok(new { results = results.Select(r => new UnlockResultDto(r.ApiName, r.Ok, r.Error)) });
});

// ---- Jobs ----
app.MapGet("/api/jobs", (HttpContext ctx) =>
{
    var now = DateTimeOffset.UtcNow;
    var jobs = SessionOrNull(ctx)?.Jobs?.Snapshot().Select(j => Mapping.Job(j, now)) ?? [];
    return Results.Ok(jobs);
});

app.MapPost("/api/jobs", async (CreateJobsRequest req, HttpContext ctx) =>
{
    var session = Session(ctx);
    if (!IsLoggedIn(session) || session.Jobs is null)
        return Results.Unauthorized();
    if (req.appIds.Length == 0 || req.hoursPerGame <= 0)
        return Results.BadRequest(new { error = "appIds and a positive hoursPerGame are required" });

    var games = await session.Steam.GetOwnedGamesAsync(ctx.RequestAborted);
    var names = games.ToDictionary(g => g.AppId, g => g.Name);

    var created = await session.Jobs.CreateJobsAsync(req.appIds, req.hoursPerGame, req.jitterPct ?? 10, names);
    var now = DateTimeOffset.UtcNow;
    return Results.Ok(new { jobs = created.Select(j => Mapping.Job(j, now)) });
});

app.MapPost("/api/jobs/{id}/pause", async (string id, HttpContext ctx) =>
{
    if (Session(ctx).Jobs is { } jobs) await jobs.PauseAsync(id);
    return Results.Ok();
});

app.MapPost("/api/jobs/{id}/resume", async (string id, HttpContext ctx) =>
{
    if (Session(ctx).Jobs is { } jobs) await jobs.ResumeAsync(id);
    return Results.Ok();
});

app.MapDelete("/api/jobs/{id}", async (string id, HttpContext ctx) =>
{
    if (Session(ctx).Jobs is { } jobs) await jobs.DeleteAsync(id);
    return Results.Ok();
});

// ---- WebSocket ----
app.Map("/ws", async (HttpContext ctx) =>
{
    if (!ctx.WebSockets.IsWebSocketRequest)
    {
        ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }
    // Reject cross-origin WebSocket handshakes (defense-in-depth against WS hijacking).
    var origin = ctx.Request.Headers.Origin.ToString();
    if (!string.IsNullOrEmpty(origin) &&
        (!Uri.TryCreate(origin, UriKind.Absolute, out var o) || o.Host != ctx.Request.Host.Host))
    {
        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
        return;
    }
    var session = Session(ctx);
    using var socket = await ctx.WebSockets.AcceptWebSocketAsync();
    await WebSocketPump.RunAsync(session, socket, ctx.RequestAborted);
});

// ---- Static frontend (SPA fallback) ----
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

app.Logger.LogInformation("Steam Idle Farmer listening on http://0.0.0.0:{Port}", port);
app.Run();

static bool IsLoggedIn(AccountSession s)
    => s.Status.State == SteamFarmer.Core.Abstractions.AuthState.LoggedIn;

// Random per-install salt for token key derivation, persisted next to the DB.
static byte[] LoadOrCreateSalt(string path)
{
    var dir = Path.GetDirectoryName(Path.GetFullPath(path));
    if (!string.IsNullOrEmpty(dir))
        Directory.CreateDirectory(dir);
    if (File.Exists(path))
        return File.ReadAllBytes(path);
    var salt = RandomNumberGenerator.GetBytes(32);
    File.WriteAllBytes(path, salt);
    if (!OperatingSystem.IsWindows())
    {
        try { File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite); } catch { }
    }
    return salt;
}
