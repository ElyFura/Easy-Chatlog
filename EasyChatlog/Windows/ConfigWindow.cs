using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;
using Dalamud.Interface.Windowing;
using EasyChatlog.Services;

namespace EasyChatlog.Windows;

public sealed class ConfigWindow : Window, IDisposable
{
    private readonly Plugin plugin;

    public ConfigWindow(Plugin plugin)
        : base("Easy Chatlog — Settings###EasyChatlogConfig",
               ImGuiWindowFlags.AlwaysAutoResize)
    {
        this.plugin = plugin;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(420, 0),
            MaximumSize = new Vector2(700, float.MaxValue),
        };
    }

    public void Dispose() { }

    public override void Draw()
    {
        var cfg = plugin.Configuration;
        var changed = false;

        if (ImGui.CollapsingHeader("Discord", ImGuiTreeNodeFlags.DefaultOpen))
        {
            var enabled = cfg.DiscordEnabled;
            if (ImGui.Checkbox("Forward chat to Discord", ref enabled))
            {
                cfg.DiscordEnabled = enabled;
                changed = true;
            }

            var modeIdx = (int)cfg.Mode;
            if (ImGui.RadioButton("Webhook", ref modeIdx, (int)DiscordMode.Webhook)) { cfg.Mode = DiscordMode.Webhook; changed = true; plugin.RebuildDiscordSender(); }
            ImGui.SameLine();
            if (ImGui.RadioButton("Bot",     ref modeIdx, (int)DiscordMode.Bot))     { cfg.Mode = DiscordMode.Bot;     changed = true; plugin.RebuildDiscordSender(); }

            ImGui.Spacing();

            if (cfg.Mode == DiscordMode.Webhook)
            {
                var url = cfg.WebhookUrl;
                if (ImGui.InputText("Webhook URL", ref url, 512))
                {
                    cfg.WebhookUrl = url;
                    changed = true;
                }

                var name = cfg.WebhookUsername;
                if (ImGui.InputText("Display name", ref name, 80))
                {
                    cfg.WebhookUsername = name;
                    changed = true;
                }
            }
            else
            {
                var token = cfg.BotToken;
                if (ImGui.InputText("Bot Token", ref token, 200, ImGuiInputTextFlags.Password))
                {
                    cfg.BotToken = token;
                    changed = true;
                }

                var chId = cfg.BotChannelId.ToString();
                if (ImGui.InputText("Channel ID", ref chId, 32, ImGuiInputTextFlags.CharsDecimal))
                {
                    cfg.BotChannelId = ulong.TryParse(chId, out var v) ? v : 0UL;
                    changed = true;
                }
            }

            if (ImGui.Button("Test send"))
            {
                _ = plugin.SendDiscordTestAsync();
            }
        }

        if (ImGui.CollapsingHeader("Buffer / Flush", ImGuiTreeNodeFlags.DefaultOpen))
        {
            var n = cfg.FlushAfterMessages;
            if (ImGui.SliderInt("Flush after N messages", ref n, 1, 100)) { cfg.FlushAfterMessages = n; changed = true; }

            var s = cfg.FlushAfterSeconds;
            if (ImGui.SliderInt("Flush after N seconds", ref s, 1, 60)) { cfg.FlushAfterSeconds = s; changed = true; }

            var hist = cfg.InMemoryHistorySize;
            if (ImGui.SliderInt("In-memory history (entries)", ref hist, 100, 50_000)) { cfg.InMemoryHistorySize = hist; changed = true; }
        }

        if (ImGui.CollapsingHeader("Channels"))
        {
            var tells = cfg.IncludeTells;
            if (ImGui.Checkbox("Include Tells (privacy-sensitive!)", ref tells))
            {
                cfg.IncludeTells = tells;
                changed = true;
            }

            ImGui.Separator();

            foreach (var (group, types) in ChannelFilter.Groups)
            {
                ImGui.TextDisabled(group);
                ImGui.Indent();
                var i = 0;
                foreach (var t in types)
                {
                    var on = cfg.EnabledChannels.GetValueOrDefault(t);
                    if (ImGui.Checkbox($"{t}##chan", ref on))
                    {
                        cfg.EnabledChannels[t] = on;
                        changed = true;
                    }
                    if (++i % 2 != 0) ImGui.SameLine(220);
                }
                ImGui.Unindent();
                ImGui.Spacing();
            }
        }

        if (ImGui.CollapsingHeader("Export"))
        {
            var dir = string.IsNullOrEmpty(cfg.ExportDirectory) ? plugin.DefaultExportDirectory : cfg.ExportDirectory;
            if (ImGui.InputText("Export directory", ref dir, 260))
            {
                cfg.ExportDirectory = dir;
                changed = true;
            }

            var fmtIdx = (int)cfg.DefaultExportFormat;
            string[] fmts = { "Plain Text (.txt)", "JSON (.json)", "HTML (.html)", "Markdown (.md)" };
            if (ImGui.Combo("Default format", ref fmtIdx, fmts, fmts.Length))
            {
                cfg.DefaultExportFormat = (ExportFormat)fmtIdx;
                changed = true;
            }
        }

        if (changed) plugin.SaveConfiguration();
    }
}
