using System.Collections.Concurrent;
using SteamKit2;
using SteamKit2.Internal;
using SteamFarmer.Core.Abstractions;

namespace SteamFarmer.Core.Steam;

/// <summary>
/// SteamKit2 handler that reads and writes achievements over the Steam client protocol,
/// without the game running. Ported from ASF Achievement Manager's AchievementHandler, but
/// self-contained: request/response are correlated with our own JobID→TaskCompletionSource map
/// (no dependency on SteamKit internals like PostCallback/AsyncJob) and keyed by achievement API name.
/// </summary>
public sealed class AchievementHandler : ClientMsgHandler
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

    private readonly ConcurrentDictionary<ulong, TaskCompletionSource<CMsgClientGetUserStatsResponse>> _getWaiters = new();
    private readonly ConcurrentDictionary<ulong, TaskCompletionSource<CMsgClientStoreUserStatsResponse>> _setWaiters = new();

    public override void HandleMsg(IPacketMsg packetMsg)
    {
        ArgumentNullException.ThrowIfNull(packetMsg);

        switch (packetMsg.MsgType)
        {
            case EMsg.ClientGetUserStatsResponse:
            {
                var body = new ClientMsgProtobuf<CMsgClientGetUserStatsResponse>(packetMsg).Body;
                if (_getWaiters.TryRemove(packetMsg.TargetJobID, out var tcs))
                    tcs.TrySetResult(body);
                break;
            }

            case EMsg.ClientStoreUserStatsResponse:
            {
                var body = new ClientMsgProtobuf<CMsgClientStoreUserStatsResponse>(packetMsg).Body;
                if (_setWaiters.TryRemove(packetMsg.TargetJobID, out var tcs))
                    tcs.TrySetResult(body);
                break;
            }
        }
    }

    /// <summary>Fetch + parse the full achievement list for an app (null on failure/timeout).</summary>
    public async Task<IReadOnlyList<StatData>?> GetAchievementsAsync(uint appId, CancellationToken ct = default)
    {
        var response = await GetRawAsync(appId, ct).ConfigureAwait(false);
        if (response is null || (EResult)response.eresult != EResult.OK)
            return null;
        return ParseResponse(response);
    }

    /// <summary>
    /// Unlock (or, with set=false, relock) the given achievements by API name. Protected achievements
    /// and unknown names come back as failures; already-in-state ones succeed as no-ops.
    /// </summary>
    public async Task<IReadOnlyList<UnlockResult>> SetAchievementsAsync(
        uint appId,
        IReadOnlyList<string> apiNames,
        bool set = true,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(apiNames);

        if (Client.SteamID is null)
            return apiNames.Select(n => new UnlockResult(n, false, "not logged in")).ToList();

        // ALWAYS fetch fresh: we need live bitfields + the current crc_stats to echo back.
        var response = await GetRawAsync(appId, ct).ConfigureAwait(false);
        if (response is null || (EResult)response.eresult != EResult.OK)
            return apiNames.Select(n => new UnlockResult(n, false, "could not read stats")).ToList();

        var stats = ParseResponse(response);
        var byName = stats
            .Where(s => s.ApiName is not null)
            .ToDictionary(s => s.ApiName!, StringComparer.Ordinal);

        var results = new List<UnlockResult>(apiNames.Count);
        var toStore = new List<CMsgClientStoreUserStats2.Stats>();
        var willStore = new List<string>();

        foreach (var name in apiNames)
        {
            if (!byName.TryGetValue(name, out var stat))
            {
                results.Add(new UnlockResult(name, false, "unknown achievement"));
                continue;
            }
            if (stat.Restricted)
            {
                results.Add(new UnlockResult(name, false, "protected — cannot be set by client"));
                continue;
            }
            if (stat.IsSet == set)
            {
                results.Add(new UnlockResult(name, true, null)); // already in desired state
                continue;
            }

            AccumulateBit(toStore, stat, set);
            willStore.Add(name);
        }

        if (toStore.Count == 0)
            return results;

        var storeOk = await StoreAsync(appId, response.crc_stats, toStore, ct).ConfigureAwait(false);
        foreach (var name in willStore)
            results.Add(new UnlockResult(name, storeOk, storeOk ? null : "store rejected by Steam"));

        // Preserve original request order.
        return apiNames
            .Select(n => results.First(r => r.ApiName == n))
            .ToList();
    }

    private async Task<CMsgClientGetUserStatsResponse?> GetRawAsync(uint appId, CancellationToken ct)
    {
        if (!Client.IsConnected || Client.SteamID is null)
            return null;

        var request = new ClientMsgProtobuf<CMsgClientGetUserStats>(EMsg.ClientGetUserStats)
        {
            SourceJobID = Client.GetNextJobID(),
        };
        request.Body.game_id = appId;
        request.Body.steam_id_for_user = Client.SteamID.ConvertToUInt64();
        // crc_stats / schema_local_version left 0 so the server returns the full schema.

        var tcs = new TaskCompletionSource<CMsgClientGetUserStatsResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        _getWaiters[request.SourceJobID.Value] = tcs;
        Client.Send(request);

        return await AwaitOrNull(tcs, request.SourceJobID.Value, _getWaiters, ct).ConfigureAwait(false);
    }

    private async Task<bool> StoreAsync(uint appId, uint crcStats, List<CMsgClientStoreUserStats2.Stats> blocks, CancellationToken ct)
    {
        var request = new ClientMsgProtobuf<CMsgClientStoreUserStats2>(EMsg.ClientStoreUserStats2)
        {
            SourceJobID = Client.GetNextJobID(),
        };
        request.Body.game_id = appId;
        request.Body.settor_steam_id = Client.SteamID!.ConvertToUInt64();
        request.Body.settee_steam_id = Client.SteamID.ConvertToUInt64();
        request.Body.explicit_reset = false;
        request.Body.crc_stats = crcStats; // echo, never compute
        request.Body.stats.AddRange(blocks);

        var tcs = new TaskCompletionSource<CMsgClientStoreUserStatsResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        _setWaiters[request.SourceJobID.Value] = tcs;
        Client.Send(request);

        var response = await AwaitOrNull(tcs, request.SourceJobID.Value, _setWaiters, ct).ConfigureAwait(false);
        if (response is null)
            return false;
        // stats_out_of_date => our crc/values were stale; treat as failure (caller may retry with a fresh GET).
        return (EResult)response.eresult == EResult.OK && !response.stats_out_of_date;
    }

    private static async Task<T?> AwaitOrNull<T>(
        TaskCompletionSource<T> tcs,
        ulong key,
        ConcurrentDictionary<ulong, TaskCompletionSource<T>> waiters,
        CancellationToken ct)
        where T : class
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(Timeout);
        try
        {
            await using (timeoutCts.Token.Register(() => tcs.TrySetCanceled()).ConfigureAwait(false))
                return await tcs.Task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        finally
        {
            waiters.TryRemove(key, out _);
        }
    }

    /// <summary>OR/AND a single achievement bit into the store block for its stat_id, reusing a shared block.</summary>
    private static void AccumulateBit(List<CMsgClientStoreUserStats2.Stats> statsToSet, StatData stat, bool set)
    {
        var current = statsToSet.Find(s => s.stat_id == stat.StatNum);
        if (current is null)
        {
            current = new CMsgClientStoreUserStats2.Stats
            {
                stat_id = stat.StatNum,
                stat_value = stat.StatValue, // start from the current bitfield so sibling bits survive
            };
            statsToSet.Add(current);
        }

        uint mask = 1u << stat.BitNum;
        if (set)
            current.stat_value |= mask;
        else
            current.stat_value &= ~mask;

        // Progress-based achievements need their backing int stat written too.
        if (!string.IsNullOrEmpty(stat.DependancyName) && statsToSet.All(s => s.stat_id != stat.Dependancy))
        {
            statsToSet.Add(new CMsgClientStoreUserStats2.Stats
            {
                stat_id = stat.Dependancy,
                stat_value = set ? stat.DependancyValue : 0,
            });
        }
    }

    /// <summary>Parse the binary KeyValues schema into a flat list of achievements. Faithful to ASF's walk, extended with display fields.</summary>
    internal static IReadOnlyList<StatData> ParseResponse(CMsgClientGetUserStatsResponse response)
    {
        var result = new List<StatData>();
        if (response.schema is null)
            return result;

        var kv = new KeyValue();
        using (var ms = new MemoryStream(response.schema))
        {
            if (!kv.TryReadAsBinary(ms))
                return result;
        }

        var statsRoot = kv.Children.Find(c => c.Name == "stats");
        if (statsRoot is null)
            return result;

        // Pass 1: every achievement bit (type 4 / ACHIEVEMENTS).
        foreach (var stat in statsRoot.Children)
        {
            string? type = stat.Children.Find(c => c.Name == "type")?.Value?.ToUpperInvariant();
            if (type != "4" && type != "ACHIEVEMENTS")
                continue;
            if (!uint.TryParse(stat.Name, out uint statNum))
                continue;

            var bits = stat.Children.Find(c => c.Name == "bits");
            if (bits is null)
                continue;

            foreach (var ach in bits.Children)
            {
                if (!int.TryParse(ach.Name, out int bitNum))
                    continue;

                uint? blockValue = response.stats?.Find(s => s.stat_id == statNum)?.stat_value;
                bool isSet = blockValue is not null && (blockValue.Value & (1u << bitNum)) != 0;

                // Protected: presence of a non-null "permission" child (bit 2 = server-only).
                bool restricted = ach.Children.Find(c => c.Name == "permission" && c.Value != null) != null;

                var progress = ach.Children.Find(c => c.Name == "progress");
                string dependencyName = progress?.Children.Find(c => c.Name == "value")?
                    .Children.Find(c => c.Name == "operand1")?.Value ?? "";
                uint.TryParse(progress?.Children.Find(c => c.Name == "max_val")?.Value ?? "0", out uint dependencyValue);

                var display = ach.Children.Find(c => c.Name == "display");
                string? displayName = LocalizedString(display?.Children.Find(c => c.Name == "name"));
                string? description = LocalizedString(display?.Children.Find(c => c.Name == "desc"));
                bool hidden = display?.Children.Find(c => c.Name == "hidden")?.Value == "1";
                string? icon = display?.Children.Find(c => c.Name == "icon")?.Value;
                string? iconGray = display?.Children.Find(c => c.Name == "icon_gray")?.Value;

                result.Add(new StatData
                {
                    StatNum = statNum,
                    BitNum = bitNum,
                    IsSet = isSet,
                    Restricted = restricted,
                    Dependancy = 0,
                    DependancyValue = dependencyValue,
                    DependancyName = dependencyName,
                    ApiName = ach.Children.Find(c => c.Name == "name")?.Value,
                    DisplayName = displayName,
                    Description = description,
                    Hidden = hidden,
                    Icon = icon,
                    IconGray = iconGray,
                    StatValue = blockValue ?? 0,
                });
            }
        }

        // Pass 2: INT stats (type 1) used as progress dependencies; propagate restricted upward.
        foreach (var stat in statsRoot.Children)
        {
            string? type = stat.Children.Find(c => c.Name == "type")?.Value?.ToUpperInvariant();
            if (type != "1" && type != "INT")
                continue;
            if (!uint.TryParse(stat.Name, out uint statNum))
                continue;

            bool restricted = int.TryParse(stat.Children.Find(c => c.Name == "permission")?.Value, out int p) && p > 1;
            string? name = stat.Children.Find(c => c.Name == "name")?.Value;
            if (name is null)
                continue;

            foreach (var parent in result.Where(x => x.DependancyName == name))
            {
                parent.Dependancy = statNum;
                if (restricted)
                    parent.Restricted = true;
            }
        }

        return result;
    }

    /// <summary>Read a localized KeyValues node, preferring English, else the first available value.</summary>
    private static string? LocalizedString(KeyValue? node)
    {
        if (node is null)
            return null;
        // A localized node has language children; a plain node has a direct value.
        if (node.Value is not null)
            return node.Value;
        var english = node.Children.Find(c => c.Name == "english");
        if (english?.Value is not null)
            return english.Value;
        return node.Children.FirstOrDefault(c => c.Value is not null)?.Value;
    }
}
