using System;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using EasyChatlog.Services;

namespace EasyChatlog.Windows;

public sealed class ExportWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private string senderFilter = "";

    public ExportWindow(Plugin plugin)
        : base("Easy Chatlog###EasyChatlogMain", ImGuiWindowFlags.None)
    {
        this.plugin = plugin;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(640, 400),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    public void Dispose() { }

    public override void Draw()
    {
        var cfg = plugin.Configuration;

        if (ImGui.Button("Open Settings")) plugin.ToggleConfigUi();
        ImGui.SameLine();
        if (ImGui.Button("Clear buffer")) plugin.Buffer.ClearHistory();
        ImGui.SameLine();
        var enabled = cfg.DiscordEnabled;
        if (ImGui.Checkbox("Discord live-forward", ref enabled))
        {
            cfg.DiscordEnabled = enabled;
            plugin.SaveConfiguration();
        }

        ImGui.Separator();

        ImGui.Text("Filter sender:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(220);
        ImGui.InputText("##senderFilter", ref senderFilter, 64);

        ImGui.SameLine();
        if (ImGui.Button("Export TXT"))      _ = ExportAsync(ExportFormat.Txt);
        ImGui.SameLine();
        if (ImGui.Button("Export JSON"))     _ = ExportAsync(ExportFormat.Json);
        ImGui.SameLine();
        if (ImGui.Button("Export HTML"))     _ = ExportAsync(ExportFormat.Html);
        ImGui.SameLine();
        if (ImGui.Button("Export MD"))       _ = ExportAsync(ExportFormat.Markdown);
        ImGui.SameLine();
        if (ImGui.Button("Send filtered to Discord")) _ = SendFilteredToDiscordAsync();

        ImGui.Separator();

        var snapshot = plugin.Buffer.SnapshotHistory();
        var filtered = string.IsNullOrWhiteSpace(senderFilter)
            ? snapshot
            : snapshot.Where(e => e.Sender.Contains(senderFilter, StringComparison.OrdinalIgnoreCase)).ToList();

        ImGui.Text($"Showing {filtered.Count} / {snapshot.Count} entries");

        if (ImGui.BeginTable("##chatTable", 4,
                ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders | ImGuiTableFlags.ScrollY | ImGuiTableFlags.Resizable))
        {
            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed, 80);
            ImGui.TableSetupColumn("Channel", ImGuiTableColumnFlags.WidthFixed, 110);
            ImGui.TableSetupColumn("Sender", ImGuiTableColumnFlags.WidthFixed, 140);
            ImGui.TableSetupColumn("Message", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableHeadersRow();

            // newest first
            for (var i = filtered.Count - 1; i >= 0; i--)
            {
                var e = filtered[i];
                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.TextUnformatted(e.Timestamp.ToString("HH:mm:ss"));
                ImGui.TableNextColumn(); ImGui.TextUnformatted(e.TypeLabel);
                ImGui.TableNextColumn(); ImGui.TextUnformatted(e.Sender);
                ImGui.TableNextColumn(); ImGui.TextWrapped(e.Message);
            }

            ImGui.EndTable();
        }
    }

    private async Task ExportAsync(ExportFormat fmt)
    {
        var entries = FilteredSnapshot();
        if (entries.Count == 0)
        {
            plugin.Notify("Nothing to export.");
            return;
        }

        try
        {
            var path = await ChatExporter.ExportAsync(entries, plugin.EffectiveExportDirectory, fmt);
            plugin.Notify($"Exported {entries.Count} entries → {path}");
        }
        catch (Exception ex)
        {
            plugin.Notify($"Export failed: {ex.Message}");
        }
    }

    private async Task SendFilteredToDiscordAsync()
    {
        var entries = FilteredSnapshot();
        if (entries.Count == 0)
        {
            plugin.Notify("Nothing to send.");
            return;
        }

        var sender = plugin.DiscordSender;
        if (sender == null)
        {
            plugin.Notify("Discord is not configured.");
            return;
        }

        try
        {
            await sender.SendBatchAsync(entries, CancellationToken.None);
            plugin.Notify($"Sent {entries.Count} entries to Discord.");
        }
        catch (Exception ex)
        {
            plugin.Notify($"Discord send failed: {ex.Message}");
        }
    }

    private System.Collections.Generic.List<Models.ChatLogEntry> FilteredSnapshot()
    {
        var snapshot = plugin.Buffer.SnapshotHistory();
        if (string.IsNullOrWhiteSpace(senderFilter)) return snapshot.ToList();
        return snapshot.Where(e => e.Sender.Contains(senderFilter, StringComparison.OrdinalIgnoreCase)).ToList();
    }
}
