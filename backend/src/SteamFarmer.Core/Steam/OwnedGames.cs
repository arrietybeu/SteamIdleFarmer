using SteamKit2.Internal;

namespace SteamFarmer.Core.Steam;

/// <summary>
/// Fetches the logged-in account's owned games via the SteamKit2 unified <c>Player.GetOwnedGames</c>
/// service (no Steam Web API key needed). Verified against SteamKit2 3.4.0.
/// </summary>
internal static class OwnedGames
{
    public static async Task<IReadOnlyList<GameInfo>> GetOwnedGamesAsync(
        Player playerService,
        ulong? steamId,
        CancellationToken ct)
    {
        if (steamId is not { } id)
            return [];

        var request = new CPlayer_GetOwnedGames_Request
        {
            steamid = id,
            include_appinfo = true,            // required for name + img_icon_url
            include_played_free_games = true,
            include_free_sub = false,
        };

        try
        {
            var response = await playerService.GetOwnedGames(request).ToTask().WaitAsync(ct).ConfigureAwait(false);
            if (response.Result != SteamKit2.EResult.OK)
                return [];

            var games = new List<GameInfo>(response.Body.games.Count);
            foreach (var game in response.Body.games)
            {
                var appId = (uint)game.appid;
                string? iconUrl = string.IsNullOrEmpty(game.img_icon_url)
                    ? null
                    : $"https://media.steampowered.com/steamcommunity/public/images/apps/{appId}/{game.img_icon_url}.jpg";

                games.Add(new GameInfo(
                    appId,
                    game.name ?? appId.ToString(),
                    iconUrl,
                    game.playtime_forever / 60.0,
                    game.has_community_visible_stats));
            }

            return games
                .OrderByDescending(g => g.PlaytimeHours)
                .ThenBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex) when (ex is TaskCanceledException or OperationCanceledException)
        {
            return [];
        }
        catch
        {
            // AsyncJobFailedException or any transient failure — surface as empty; caller can retry.
            return [];
        }
    }
}
