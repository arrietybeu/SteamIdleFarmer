using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using SteamFarmer.Core;
using SteamFarmer.Core.Sessions;

namespace SteamFarmer.Api;

/// <summary>Streams live session events (auth, jobs, challenge URL, achievement unlocks) to one WebSocket client.</summary>
public static class WebSocketPump
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task RunAsync(AccountSession session, WebSocket socket, CancellationToken ct)
    {
        // Bounded so a stalled/slow client cannot grow memory without limit; drop the oldest
        // queued update rather than block producers (latest state matters most).
        var channel = Channel.CreateBounded<string>(new BoundedChannelOptions(256)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
        });

        void Enqueue(object message) => channel.Writer.TryWrite(JsonSerializer.Serialize(message, Json));

        void OnChanged()
        {
            Enqueue(new { type = "auth", status = Mapping.Auth(session.Status) });
            Enqueue(JobsMessage(session));
        }
        void OnChallenge(string url) => Enqueue(new { type = "challenge", challengeUrl = url });
        void OnAchievement(FarmJob job, ScheduledUnlock unlock) => Enqueue(new
        {
            type = "achievement",
            appId = job.AppId,
            apiName = unlock.ApiName,
            name = unlock.ApiName,
            game = job.GameName,
        });

        session.Changed += OnChanged;
        session.ChallengeUrl += OnChallenge;
        session.AchievementUnlocked += OnAchievement;

        // Initial snapshot.
        OnChanged();

        try
        {
            var send = SendLoop(socket, channel, ct);
            var receive = ReceiveLoop(socket, ct);
            await Task.WhenAny(send, receive).ConfigureAwait(false);
        }
        finally
        {
            session.Changed -= OnChanged;
            session.ChallengeUrl -= OnChallenge;
            session.AchievementUnlocked -= OnAchievement;
            channel.Writer.TryComplete();
        }
    }

    private static object JobsMessage(AccountSession session)
    {
        var now = DateTimeOffset.UtcNow;
        var jobs = session.Jobs?.Snapshot().Select(j => Mapping.Job(j, now)) ?? [];
        return new { type = "jobs", jobs };
    }

    private static async Task SendLoop(WebSocket socket, Channel<string> channel, CancellationToken ct)
    {
        await foreach (var message in channel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
        {
            if (socket.State != WebSocketState.Open)
                break;
            var bytes = Encoding.UTF8.GetBytes(message);
            await socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, ct).ConfigureAwait(false);
        }
    }

    private static async Task ReceiveLoop(WebSocket socket, CancellationToken ct)
    {
        var buffer = new byte[1024];
        while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            var result = await socket.ReceiveAsync(buffer, ct).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, ct).ConfigureAwait(false);
                break;
            }
        }
    }
}
