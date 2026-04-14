using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using EasyChatlog.Models;
using EasyChatlog.Services;

namespace EasyChatlog.Windows;

public sealed class ExportWindow : Window, IDisposable
{
    private readonly Plugin plugin;

    // Sender filter lives on ChatBuffer so both UI and live-forward share it.
    private HashSet<string> SelectedSenders => plugin.Buffer.SelectedSenders;
    private string[] knownSenders = [];
    private string senderSearch = "";

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
        var snapshot = plugin.Buffer.SnapshotHistory();

        // Refresh known senders list from buffer.
        knownSenders = snapshot
            .Select(e => e.Sender)
            .Where(s => !string.IsNullOrEmpty(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        // --- toolbar ---
        if (ImGui.Button("Open Settings")) plugin.ToggleConfigUi();
        ImGui.SameLine();
        if (ImGui.Button("Clear buffer"))
        {
            plugin.Buffer.ClearHistory();
            SelectedSenders.Clear();
        }
        ImGui.SameLine();
        var enabled = cfg.DiscordEnabled;
        if (ImGui.Checkbox("Discord live-forward", ref enabled))
        {
            cfg.DiscordEnabled = enabled;
            plugin.SaveConfiguration();
        }

        ImGui.Separator();

        // --- sender multi-select ---
        DrawSenderFilter();

        ImGui.Separator();

        // --- export / send buttons ---
        if (ImGui.Button("Export TXT"))  _ = ExportAsync(ExportFormat.Txt);
        ImGui.SameLine();
        if (ImGui.Button("Export JSON")) _ = ExportAsync(ExportFormat.Json);
        ImGui.SameLine();
        if (ImGui.Button("Export HTML")) _ = ExportAsync(ExportFormat.Html);
        ImGui.SameLine();
        if (ImGui.Button("Export MD"))   _ = ExportAsync(ExportFormat.Markdown);
        ImGui.SameLine();
        if (ImGui.Button("Send filtered to Discord")) _ = SendFilteredToDiscordAsync();

        ImGui.Separator();

        // --- table ---
        var filtered = ApplyFilter(snapshot);
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

    private void DrawSenderFilter()
    {
        ImGui.Text("Filter by sender(s):");
        ImGui.SameLine();

        int count;
        lock (SelectedSenders) { count = SelectedSenders.Count; }

        if (count == 0)
            ImGui.TextDisabled("(all — live-forward sends everything)");
        else
            ImGui.TextDisabled($"({count} selected — live-forward + export use this filter)");

        ImGui.SameLine();
        if (ImGui.SmallButton("Clear filter"))
        {
            lock (SelectedSenders) { SelectedSenders.Clear(); }
        }

        // Search box to narrow the sender list.
        ImGui.SetNextItemWidth(200);
        ImGui.InputTextWithHint("##senderSearch", "Search sender...", ref senderSearch, 64);

        // Compact checkbox list — show senders matching the search.
        var visibleSenders = string.IsNullOrWhiteSpace(senderSearch)
            ? knownSenders
            : knownSenders.Where(s => s.Contains(senderSearch, StringComparison.OrdinalIgnoreCase)).ToArray();

        if (visibleSenders.Length > 0 && ImGui.BeginChild("##senderList", new Vector2(0, Math.Min(visibleSenders.Length * 24 + 8, 200)), true))
        {
            foreach (var name in visibleSenders)
            {
                bool selected;
                lock (SelectedSenders) { selected = SelectedSenders.Contains(name); }
                if (ImGui.Checkbox(name, ref selected))
                {
                    lock (SelectedSenders)
                    {
                        if (selected)
                            SelectedSenders.Add(name);
                        else
                            SelectedSenders.Remove(name);
                    }
                }
            }
            ImGui.EndChild();
        }
    }

    private List<ChatLogEntry> ApplyFilter(IReadOnlyList<ChatLogEntry> snapshot)
    {
        lock (SelectedSenders)
        {
            if (SelectedSenders.Count == 0)
                return snapshot.ToList();

            return snapshot.Where(e => SelectedSenders.Contains(e.Sender)).ToList();
        }
    }

    private async Task ExportAsync(ExportFormat fmt)
    {
        var entries = ApplyFilter(plugin.Buffer.SnapshotHistory());
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
        var entries = ApplyFilter(plugin.Buffer.SnapshotHistory());
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
}
