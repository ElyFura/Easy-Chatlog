using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using EasyChatlog.Localization;
using EasyChatlog.Models;
using EasyChatlog.Services;
using EasyChatlog.Windows;

namespace EasyChatlog;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string MainCommand = "/easychatlog";
    private const string ShortCommand = "/ecl";

    public Configuration Configuration { get; }
    public ChatBuffer Buffer { get; }
    public IDiscordSender? DiscordSender { get; private set; }
    public string DefaultExportDirectory { get; }
    public string EffectiveExportDirectory =>
        string.IsNullOrWhiteSpace(ActiveCharConfig.ExportDirectory) ? DefaultExportDirectory : ActiveCharConfig.ExportDirectory;

    /// <summary>
    /// Profile assigned to the logged-in character, or the default profile when no one is logged in.
    /// </summary>
    public ConfigProfile ActiveProfile =>
        Configuration.GetProfileForCharacter(PlayerState.ContentId);

    /// <summary>Settings payload of the <see cref="ActiveProfile"/>.</summary>
    public CharacterConfig ActiveCharConfig => ActiveProfile.Config;

    private readonly WindowSystem windows = new("EasyChatlog");
    private readonly ConfigWindow configWindow;
    private readonly ExportWindow exportWindow;
    private readonly ChatCaptureService capture;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Migrate();

        Loc.Apply(Configuration.Language, PluginInterface.UiLanguage);

        DefaultExportDirectory = Path.Combine(
            PluginInterface.GetPluginConfigDirectory(), "exports");

        DiscordSender = BuildDiscordSender();
        Buffer = new ChatBuffer(() => ActiveCharConfig, () => DiscordSender, Log);
        capture = new ChatCaptureService(ChatGui, Log, () => ActiveCharConfig, Buffer);

        configWindow = new ConfigWindow(this);
        exportWindow = new ExportWindow(this);
        windows.AddWindow(configWindow);
        windows.AddWindow(exportWindow);

        PluginInterface.UiBuilder.Draw         += windows.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi   += ToggleMainUi;

        RegisterCommands();

        ClientState.Login  += OnLogin;
        ClientState.Logout += OnLogout;

        // Keep the localized command help in sync when the language changes.
        Loc.Changed += OnLanguageChanged;
        PluginInterface.LanguageChanged += OnDalamudLanguageChanged;

        // If a character is already logged in when the plugin loads, persist any newly created config.
        if (PlayerState.ContentId != 0)
            SaveConfiguration();

        Log.Information("Easy Chatlog loaded.");
    }

    public void Dispose()
    {
        PluginInterface.LanguageChanged -= OnDalamudLanguageChanged;
        Loc.Changed -= OnLanguageChanged;

        ClientState.Login  -= OnLogin;
        ClientState.Logout -= OnLogout;

        CommandManager.RemoveHandler(MainCommand);
        CommandManager.RemoveHandler(ShortCommand);

        PluginInterface.UiBuilder.Draw         -= windows.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi   -= ToggleMainUi;

        windows.RemoveAllWindows();
        configWindow.Dispose();
        exportWindow.Dispose();

        capture.Dispose();
        Buffer.Dispose();
        DiscordSender?.Dispose();
    }

    public void SaveConfiguration() => PluginInterface.SavePluginConfig(Configuration);

    private void RegisterCommands()
    {
        CommandManager.AddHandler(MainCommand, new CommandInfo(OnCommand)
        {
            HelpMessage = Loc.S.CommandHelp,
        });
        CommandManager.AddHandler(ShortCommand, new CommandInfo(OnCommand)
        {
            HelpMessage = Loc.S.CommandHelpAlias,
            ShowInHelp = false,
        });
    }

    /// <summary>Command help text is captured at registration time, so re-register it on a language switch.</summary>
    private void OnLanguageChanged()
    {
        CommandManager.RemoveHandler(MainCommand);
        CommandManager.RemoveHandler(ShortCommand);
        RegisterCommands();
    }

    private void OnDalamudLanguageChanged(string langCode) => Loc.OnDalamudLanguageChanged(langCode);

    /// <summary>Switch the UI language and persist the choice.</summary>
    public void SetLanguage(Language language)
    {
        Configuration.Language = language;
        Loc.Apply(language, PluginInterface.UiLanguage);
        SaveConfiguration();
    }

    public void RebuildDiscordSender()
    {
        var old = DiscordSender;
        DiscordSender = BuildDiscordSender();
        old?.Dispose();
    }

    private IDiscordSender? BuildDiscordSender()
    {
        var cfg = ActiveCharConfig;
        return cfg.Mode switch
        {
            DiscordMode.Webhook => new DiscordWebhookSender(cfg, Log),
            DiscordMode.Bot     => new DiscordBotSender(cfg, Log, SaveConfiguration),
            _ => null,
        };
    }

    private void OnLogin()
    {
        var cid = PlayerState.ContentId;
        if (cid != 0)
        {
            var name = PlayerState.CharacterName;
            if (!string.IsNullOrEmpty(name))
                Configuration.CharacterNames[cid] = name;
        }
        RebuildDiscordSender();
        SaveConfiguration();
        Log.Information("Easy Chatlog: loaded profile '{0}' for character {1}.", ActiveProfile.Name, cid);
    }

    private void OnLogout(int type, int code)
    {
        Log.Information("Easy Chatlog: character logged out.");
    }

    public Task SendDiscordTestAsync()
    {
        var sender = DiscordSender;
        if (sender == null)
        {
            Notify(Loc.S.SenderNotConfigured);
            return Task.CompletedTask;
        }
        var who = PlayerState.CharacterName ?? "FFXIV";
        var body = string.Format(Loc.S.DiscordTestBodyFmt, who, DateTime.Now.ToString("HH:mm:ss"));

        return Task.Run(async () =>
        {
            try
            {
                await sender.SendRawAsync(body, CancellationToken.None);
                Notify(Loc.S.DiscordTestSent);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Discord test failed");
                Notify(string.Format(Loc.S.DiscordTestFailedFmt, ex.Message));
            }
        });
    }

    public void Notify(string message)
    {
        ChatGui.Print($"[Easy Chatlog] {message}");
        Log.Information(message);
    }

    public void ToggleConfigUi() => configWindow.Toggle();
    public void ToggleMainUi()   => exportWindow.Toggle();

    private void OnCommand(string command, string args)
    {
        var trimmed = (args ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            ToggleMainUi();
            return;
        }

        var parts = trimmed.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var sub = parts[0].ToLowerInvariant();
        var rest = parts.Length > 1 ? parts[1] : "";

        switch (sub)
        {
            case "config":
            case "settings":
                ToggleConfigUi();
                break;

            case "export":
                var fmt = ParseFormat(rest);
                _ = QuickExportAsync(fmt);
                break;

            case "discord":
                ActiveCharConfig.DiscordEnabled = rest.Equals("on", StringComparison.OrdinalIgnoreCase);
                SaveConfiguration();
                Notify(string.Format(Loc.S.LiveForwardOnOffFmt,
                    ActiveCharConfig.DiscordEnabled ? Loc.S.Enabled : Loc.S.Disabled));
                break;

            default:
                Notify(string.Format(Loc.S.UnknownSubcommandFmt, sub));
                break;
        }
    }

    private ExportFormat ParseFormat(string s) => s.ToLowerInvariant() switch
    {
        "json" => ExportFormat.Json,
        "html" => ExportFormat.Html,
        "md" or "markdown" => ExportFormat.Markdown,
        "txt" or "" => ActiveCharConfig.DefaultExportFormat,
        _ => ActiveCharConfig.DefaultExportFormat,
    };

    private async Task QuickExportAsync(ExportFormat fmt)
    {
        var entries = Buffer.SnapshotHistory();
        if (entries.Count == 0)
        {
            Notify(Loc.S.NothingToExport);
            return;
        }
        try
        {
            var path = await ChatExporter.ExportAsync(entries, EffectiveExportDirectory, fmt);
            Notify(string.Format(Loc.S.ExportedFmt, entries.Count, path));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Quick export failed");
            Notify(string.Format(Loc.S.ExportFailedFmt, ex.Message));
        }
    }
}
