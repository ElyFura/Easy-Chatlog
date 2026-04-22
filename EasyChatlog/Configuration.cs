using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Configuration;
using Dalamud.Game.Text;
using EasyChatlog.Services;

namespace EasyChatlog;

public enum DiscordMode
{
    Webhook = 0,
    Bot = 1,
}

public enum ExportFormat
{
    Txt = 0,
    Json = 1,
    Html = 2,
    Markdown = 3,
}

/// <summary>
/// The actual settings payload. Lives inside a <see cref="ConfigProfile"/>.
/// </summary>
public sealed class CharacterConfig
{
    // Discord
    public bool DiscordEnabled { get; set; } = false;
    public DiscordMode Mode { get; set; } = DiscordMode.Webhook;
    public string WebhookUrl { get; set; } = "";
    public string WebhookUsername { get; set; } = "FFXIV Chat";
    public string BotToken { get; set; } = "";
    public ulong BotChannelId { get; set; } = 0UL;

    // Buffer
    public int FlushAfterMessages { get; set; } = 10;
    public int FlushAfterSeconds { get; set; } = 5;

    // Memory ring buffer size for the export window preview
    public int InMemoryHistorySize { get; set; } = 5000;

    // Export
    public string ExportDirectory { get; set; } = ""; // empty -> default under pluginConfigs
    public ExportFormat DefaultExportFormat { get; set; } = ExportFormat.Txt;

    // Filter
    public bool IncludeTells { get; set; } = false;
    public Dictionary<XivChatType, bool> EnabledChannels { get; set; } = ChannelFilter.DefaultMap();

    public CharacterConfig Clone() => new()
    {
        DiscordEnabled       = DiscordEnabled,
        Mode                 = Mode,
        WebhookUrl           = WebhookUrl,
        WebhookUsername      = WebhookUsername,
        BotToken             = BotToken,
        BotChannelId         = BotChannelId,
        FlushAfterMessages   = FlushAfterMessages,
        FlushAfterSeconds    = FlushAfterSeconds,
        InMemoryHistorySize  = InMemoryHistorySize,
        ExportDirectory      = ExportDirectory,
        DefaultExportFormat  = DefaultExportFormat,
        IncludeTells         = IncludeTells,
        EnabledChannels      = new Dictionary<XivChatType, bool>(EnabledChannels),
    };
}

/// <summary>
/// A named reusable config profile. Multiple characters may share one profile.
/// </summary>
public sealed class ConfigProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Default";
    public CharacterConfig Config { get; set; } = new();
}

public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    /// <summary>All named config profiles, keyed by <see cref="ConfigProfile.Id"/>.</summary>
    public Dictionary<Guid, ConfigProfile> Profiles { get; set; } = new();

    /// <summary>Maps a character's ContentId to the assigned profile id.</summary>
    public Dictionary<ulong, Guid> CharacterProfileMap { get; set; } = new();

    /// <summary>Display names for known characters (populated on login, used only for UI).</summary>
    public Dictionary<ulong, string> CharacterNames { get; set; } = new();

    /// <summary>Profile used when a character has no explicit assignment.</summary>
    public Guid DefaultProfileId { get; set; } = Guid.Empty;

    // ---- V2 legacy field ----------------------------------------------------------------
    // Old per-character dictionary. Migrated into Profiles + CharacterProfileMap.
    public Dictionary<ulong, CharacterConfig> Characters { get; set; } = new();

    // ---- V1 legacy fields (flat / global) -----------------------------------------------
    public bool DiscordEnabled { get; set; }
    public DiscordMode Mode { get; set; }
    public string WebhookUrl { get; set; } = "";
    public string WebhookUsername { get; set; } = "FFXIV Chat";
    public string BotToken { get; set; } = "";
    public ulong BotChannelId { get; set; }
    public int FlushAfterMessages { get; set; } = 10;
    public int FlushAfterSeconds { get; set; } = 5;
    public int InMemoryHistorySize { get; set; } = 5000;
    public string ExportDirectory { get; set; } = "";
    public ExportFormat DefaultExportFormat { get; set; }
    public bool IncludeTells { get; set; }
    public Dictionary<XivChatType, bool> EnabledChannels { get; set; } = ChannelFilter.DefaultMap();

    /// <summary>
    /// Bring the loaded config up to the current schema. Must be called once at startup.
    /// </summary>
    public void Migrate()
    {
        // V1 → V2: flat fields become a single CharacterConfig parked under contentId 0.
        if (Version < 2)
        {
            if (Characters.Count == 0)
            {
                Characters[0] = new CharacterConfig
                {
                    DiscordEnabled       = DiscordEnabled,
                    Mode                 = Mode,
                    WebhookUrl           = WebhookUrl,
                    WebhookUsername      = WebhookUsername,
                    BotToken             = BotToken,
                    BotChannelId         = BotChannelId,
                    FlushAfterMessages   = FlushAfterMessages,
                    FlushAfterSeconds    = FlushAfterSeconds,
                    InMemoryHistorySize  = InMemoryHistorySize,
                    ExportDirectory      = ExportDirectory,
                    DefaultExportFormat  = DefaultExportFormat,
                    IncludeTells         = IncludeTells,
                    EnabledChannels      = EnabledChannels ?? ChannelFilter.DefaultMap(),
                };
            }
            Version = 2;
        }

        // V2 → V3: Characters dict becomes named Profiles + a ContentId→Guid mapping.
        if (Version < 3)
        {
            foreach (var (cid, charCfg) in Characters)
            {
                var profile = new ConfigProfile
                {
                    Name   = cid == 0 ? "Default" : (CharacterNames.TryGetValue(cid, out var n) ? n : $"Character {cid}"),
                    Config = charCfg,
                };
                Profiles[profile.Id] = profile;
                if (cid != 0)
                    CharacterProfileMap[cid] = profile.Id;
                if (DefaultProfileId == Guid.Empty)
                    DefaultProfileId = profile.Id;
            }
            Characters.Clear();
            Version = 3;
        }

        // Safety net: always have at least one profile and a valid DefaultProfileId.
        if (Profiles.Count == 0)
        {
            var p = new ConfigProfile { Name = "Default" };
            Profiles[p.Id] = p;
            DefaultProfileId = p.Id;
        }
        else if (!Profiles.ContainsKey(DefaultProfileId))
        {
            DefaultProfileId = Profiles.Keys.First();
        }

        // Drop dangling character assignments (profile might have been deleted in a prior session).
        foreach (var cid in CharacterProfileMap.Where(kv => !Profiles.ContainsKey(kv.Value)).Select(kv => kv.Key).ToList())
            CharacterProfileMap.Remove(cid);
    }

    /// <summary>Profile assigned to <paramref name="contentId"/>, or the default profile.</summary>
    public ConfigProfile GetProfileForCharacter(ulong contentId)
    {
        if (contentId != 0
            && CharacterProfileMap.TryGetValue(contentId, out var pid)
            && Profiles.TryGetValue(pid, out var p))
            return p;

        return Profiles[DefaultProfileId];
    }

    public ConfigProfile CreateProfile(string name, CharacterConfig? seed = null)
    {
        var profile = new ConfigProfile
        {
            Name   = string.IsNullOrWhiteSpace(name) ? "New profile" : name,
            Config = seed?.Clone() ?? new CharacterConfig(),
        };
        Profiles[profile.Id] = profile;
        return profile;
    }

    /// <summary>Deletes a profile. Characters pointing at it fall back to the default. No-op if only one profile remains.</summary>
    public void DeleteProfile(Guid id)
    {
        if (Profiles.Count <= 1) return;
        if (!Profiles.Remove(id)) return;

        if (DefaultProfileId == id)
            DefaultProfileId = Profiles.Keys.First();

        foreach (var cid in CharacterProfileMap.Where(kv => kv.Value == id).Select(kv => kv.Key).ToList())
            CharacterProfileMap.Remove(cid);
    }

    public void AssignProfile(ulong contentId, Guid profileId)
    {
        if (contentId == 0) return;
        if (!Profiles.ContainsKey(profileId)) return;
        CharacterProfileMap[contentId] = profileId;
    }
}
