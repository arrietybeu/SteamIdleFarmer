namespace SteamFarmer.Core.Steam;

/// <summary>
/// One achievement (a single bit inside a "stats" bitfield block) parsed from the
/// binary KeyValues schema returned by ClientGetUserStats. Ported from ASF Achievement Manager's
/// StatData, extended with richer display fields for the web UI.
/// </summary>
public sealed class StatData
{
    /// <summary>Stat block id (== CMsgClientGetUserStatsResponse.Stats.stat_id).</summary>
    public uint StatNum { get; set; }

    /// <summary>Bit index within the block.</summary>
    public int BitNum { get; set; }

    /// <summary>Currently unlocked?</summary>
    public bool IsSet { get; set; }

    /// <summary>Protected — can only be set by the game server, never client-side. We refuse these.</summary>
    public bool Restricted { get; set; }

    /// <summary>stat_id of the progress dependency stat, if any.</summary>
    public uint Dependancy { get; set; }

    /// <summary>Value to write to the dependency stat when unlocking.</summary>
    public uint DependancyValue { get; set; }

    public string? DependancyName { get; set; }

    /// <summary>Schema bit "name" — the achievement API name (e.g. ACH_WIN_ONE_GAME).</summary>
    public string? ApiName { get; set; }

    /// <summary>Localized display name.</summary>
    public string? DisplayName { get; set; }

    public string? Description { get; set; }
    public bool Hidden { get; set; }

    /// <summary>Icon file names (combine with the app id to build a CDN URL).</summary>
    public string? Icon { get; set; }
    public string? IconGray { get; set; }

    /// <summary>Current raw bitfield value of the whole block.</summary>
    public uint StatValue { get; set; }
}
