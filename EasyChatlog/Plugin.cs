using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
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
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string MainCommand = "/easychatlog";
    private const string ShortCommand = "/ecl";

    public Configuration Configuration { get; }
    public ChatBuffer Buffer { get; }
    public IDiscordSender? DiscordSender { get; private set; }
    public string DefaultExportDirectory { get; }
    public string EffectiveExportDirectory =>
        string.IsNullOrWhiteSpace(Configuration.ExportDirectory) ? DefaultExportDirectory : Configuration.ExportDirectory;

    private readonly WindowSystem windows = new("EasyChatlog");
    private readonly ConfigWindow configWindow;
    private readonly ExportWindow exportWindow;
    private readonly ChatCaptureService capture;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        DefaultExportDirectory = Path.Combine(
            PluginInterface.GetPluginConfigDirectory(), "exports");

        DiscordSender = BuildDiscordSender();
        Buffer = new ChatBuffer(Configuration, DiscordSender, Log);
        capture = new ChatCaptureService(ChatGui, Log, Configuration, Buffer);

        configWindow = new ConfigWindow(this);
        exportWindow = new ExportWindow(this);
        windows.AddWindow(configWindow);
        windows.AddWindow(exportWindow);

        PluginInterface.UiBuilder.Draw         += windows.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi   += ToggleMainUi;

        CommandManager.AddHandler(MainCommand, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open Easy Chatlog. Subcommands: config | export <txt|json|html|md> | discord on|off",
        });
        CommandManager.AddHandler(ShortCommand, new CommandInfo(OnCommand)
        {
            HelpMessage = "Alias for /easychatlog",
            ShowInHelp = false,
        });

        Log.Information("Easy Chatlog loaded.");
    }

    public void Dispose()
    {
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

    public void RebuildDiscordSender()
    {
        var old = DiscordSender;
        DiscordSender = BuildDiscordSender();
        old?.Dispose();
    }

    private IDiscordSender? BuildDiscordSender()
    {
        return Configuration.Mode switch
        {
            DiscordMode.Webhook => new DiscordWebhookSender(Configuration, Log),
            DiscordMode.Bot     => new DiscordBotSender(Configuration, Log),
            _ => null,
        };
    }

    public Task SendDiscordTestAsync()
    {
        var sender = DiscordSender;
        if (sender == null)
        {
            Notify("Discord sender not configured.");
            return Task.CompletedTask;
        }
        // LocalPlayer can only be read on the game/main thread — capture before going async.
#pragma warning disable CS0618
        var who = ClientState.LocalPlayer?.Name.TextValue ?? "FFXIV";
#pragma warning restore CS0618

        return Task.Run(async () =>
        {
            try
            {
                await sender.SendRawAsync($"Easy Chatlog test from {who} ({DateTime.Now:HH:mm:ss})", CancellationToken.None);
                Notify("Discord test sent.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Discord test failed");
                Notify($"Discord test failed: {ex.Message}");
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
                Configuration.DiscordEnabled = rest.Equals("on", StringComparison.OrdinalIgnoreCase);
                SaveConfiguration();
                Notify($"Discord live-forward {(Configuration.DiscordEnabled ? "ENABLED" : "DISABLED")}.");
                break;

            default:
                Notify($"Unknown subcommand: {sub}");
                break;
        }
    }

    private ExportFormat ParseFormat(string s) => s.ToLowerInvariant() switch
    {
        "json" => ExportFormat.Json,
        "html" => ExportFormat.Html,
        "md" or "markdown" => ExportFormat.Markdown,
        "txt" or "" => Configuration.DefaultExportFormat,
        _ => Configuration.DefaultExportFormat,
    };

    private async Task QuickExportAsync(ExportFormat fmt)
    {
        var entries = Buffer.SnapshotHistory();
        if (entries.Count == 0)
        {
            Notify("Nothing to export.");
            return;
        }
        try
        {
            var path = await ChatExporter.ExportAsync(entries, EffectiveExportDirectory, fmt);
            Notify($"Exported {entries.Count} entries → {path}");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Quick export failed");
            Notify($"Export failed: {ex.Message}");
        }
    }
}
