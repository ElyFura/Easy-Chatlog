using System.Collections.Generic;
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

public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

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
}
