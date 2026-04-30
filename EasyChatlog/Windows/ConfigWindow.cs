using System;
using System.Collections.Generic;
using System.Linq;
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

    // Which profile the editor is currently acting on. May differ from the active character's profile.
    private Guid editingProfileId = Guid.Empty;

    // Popup buffers
    private string newNameBuffer = "";
    private string renameBuffer = "";
    private Guid pendingDeleteId = Guid.Empty;

    public ConfigWindow(Plugin plugin)
        : base("Easy Chatlog — Settings###EasyChatlogConfig",
               ImGuiWindowFlags.AlwaysAutoResize)
    {
        this.plugin = plugin;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(460, 0),
            MaximumSize = new Vector2(760, float.MaxValue),
        };
    }

    public void Dispose() { }

    public override void Draw()
    {
        var charName = Plugin.PlayerState.CharacterName;
        WindowName = !string.IsNullOrEmpty(charName)
            ? $"Easy Chatlog — Settings ({charName})###EasyChatlogConfig"
            : "Easy Chatlog — Settings###EasyChatlogConfig";

        var config = plugin.Configuration;

        // Keep editingProfileId valid; default to the active character's profile when entering.
        if (!config.Profiles.ContainsKey(editingProfileId))
            editingProfileId = plugin.ActiveProfile.Id;

        var profile = config.Profiles[editingProfileId];
        var cfg = profile.Config;
        var changed = false;

        DrawProfileBar(ref changed);
        ImGui.Separator();

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

        if (ImGui.CollapsingHeader("Character assignments"))
            DrawCharacterAssignments(ref changed);

        if (changed) plugin.SaveConfiguration();

        DrawNewPopup();
        DrawRenamePopup();
        DrawDeletePopup();
    }

    // --- Profile bar ---------------------------------------------------------------------

    private void DrawProfileBar(ref bool changed)
    {
        var config = plugin.Configuration;

        // Sorted profile list for the combos.
        var ids = config.Profiles.Values
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Select(p => p.Id)
            .ToArray();
        var names = ids.Select(id => config.Profiles[id].Name).ToArray();

        var editIdx = Array.IndexOf(ids, editingProfileId);
        if (editIdx < 0) editIdx = 0;

        ImGui.Text("Editing profile:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(220);
        if (ImGui.Combo("##editProfile", ref editIdx, names, names.Length))
            editingProfileId = ids[editIdx];

        if (editingProfileId == config.DefaultProfileId)
        {
            ImGui.SameLine();
            ImGui.TextDisabled("(default)");
        }

        ImGui.SameLine();
        if (ImGui.Button("New"))
        {
            newNameBuffer = "";
            ImGui.OpenPopup("##newProfile");
        }
        ImGui.SameLine();
        if (ImGui.Button("Rename"))
        {
            renameBuffer = config.Profiles[editingProfileId].Name;
            ImGui.OpenPopup("##renameProfile");
        }
        ImGui.SameLine();
        if (ImGui.Button("Duplicate"))
        {
            var src = config.Profiles[editingProfileId];
            var dup = config.CreateProfile(src.Name + " (copy)", src.Config);
            editingProfileId = dup.Id;
            changed = true;
        }
        ImGui.SameLine();
        ImGui.BeginDisabled(config.Profiles.Count <= 1);
        if (ImGui.Button("Delete"))
        {
            pendingDeleteId = editingProfileId;
            ImGui.OpenPopup("##deleteProfile");
        }
        ImGui.EndDisabled();

        // Set-as-default toggle — makes new / unassigned characters use this profile.
        if (editingProfileId != config.DefaultProfileId)
        {
            if (ImGui.SmallButton("Set as default"))
            {
                config.DefaultProfileId = editingProfileId;
                changed = true;
            }
            ImGui.SameLine();
            ImGui.TextDisabled("(current default fallback for unassigned characters)");
        }

        // Assignment for the logged-in character.
        var cid = Plugin.PlayerState.ContentId;
        if (cid != 0)
        {
            var charName = Plugin.PlayerState.CharacterName;
            if (string.IsNullOrEmpty(charName)) charName = $"Character {cid}";

            var assignedId = config.CharacterProfileMap.GetValueOrDefault(cid, config.DefaultProfileId);
            var assignedIdx = Array.IndexOf(ids, assignedId);
            if (assignedIdx < 0) assignedIdx = 0;

            ImGui.Spacing();
            ImGui.Text($"Profile active for {charName}:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(220);
            if (ImGui.Combo("##assignProfile", ref assignedIdx, names, names.Length))
            {
                config.AssignProfile(cid, ids[assignedIdx]);
                plugin.RebuildDiscordSender();
                changed = true;
            }

            if (ids[assignedIdx] != editingProfileId)
            {
                ImGui.SameLine();
                if (ImGui.SmallButton("Edit this one"))
                    editingProfileId = ids[assignedIdx];
            }
        }
    }

    // --- Known-character assignment list -------------------------------------------------

    private void DrawCharacterAssignments(ref bool changed)
    {
        var config = plugin.Configuration;

        var knownIds = config.CharacterNames.Keys
            .Concat(config.CharacterProfileMap.Keys)
            .Distinct()
            .OrderBy(id => config.CharacterNames.GetValueOrDefault(id, id.ToString()), StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (knownIds.Count == 0)
        {
            ImGui.TextDisabled("No characters recorded yet. Log in on a character to populate this list.");
            return;
        }

        var profileIds = config.Profiles.Values
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Select(p => p.Id)
            .ToArray();
        var profileNames = profileIds.Select(id => config.Profiles[id].Name).ToArray();

        foreach (var cid in knownIds)
        {
            var label = config.CharacterNames.TryGetValue(cid, out var n) ? n : $"Character {cid}";
            var assignedId = config.CharacterProfileMap.GetValueOrDefault(cid, config.DefaultProfileId);
            var idx = Array.IndexOf(profileIds, assignedId);
            if (idx < 0) idx = 0;

            ImGui.SetNextItemWidth(220);
            if (ImGui.Combo($"{label}##assign_{cid}", ref idx, profileNames, profileNames.Length))
            {
                config.AssignProfile(cid, profileIds[idx]);
                if (cid == Plugin.PlayerState.ContentId)
                    plugin.RebuildDiscordSender();
                changed = true;
            }

            ImGui.SameLine();
            if (ImGui.SmallButton($"Clear##clr_{cid}"))
            {
                config.CharacterProfileMap.Remove(cid);
                if (cid == Plugin.PlayerState.ContentId)
                    plugin.RebuildDiscordSender();
                changed = true;
            }
        }
    }

    // --- Popups --------------------------------------------------------------------------

    private void DrawNewPopup()
    {
        if (!ImGui.BeginPopup("##newProfile")) return;

        ImGui.Text("New profile name:");
        ImGui.SetNextItemWidth(260);
        var submit = ImGui.InputText("##newName", ref newNameBuffer, 64, ImGuiInputTextFlags.EnterReturnsTrue);

        if (ImGui.Button("Create") || submit)
        {
            var p = plugin.Configuration.CreateProfile(newNameBuffer);
            editingProfileId = p.Id;
            plugin.SaveConfiguration();
            ImGui.CloseCurrentPopup();
        }
        ImGui.SameLine();
        if (ImGui.Button("Cancel")) ImGui.CloseCurrentPopup();

        ImGui.EndPopup();
    }

    private void DrawRenamePopup()
    {
        if (!ImGui.BeginPopup("##renameProfile")) return;

        ImGui.Text("Rename profile:");
        ImGui.SetNextItemWidth(260);
        var submit = ImGui.InputText("##rename", ref renameBuffer, 64, ImGuiInputTextFlags.EnterReturnsTrue);

        if ((ImGui.Button("Save") || submit)
            && plugin.Configuration.Profiles.TryGetValue(editingProfileId, out var p)
            && !string.IsNullOrWhiteSpace(renameBuffer))
        {
            p.Name = renameBuffer.Trim();
            plugin.SaveConfiguration();
            ImGui.CloseCurrentPopup();
        }
        ImGui.SameLine();
        if (ImGui.Button("Cancel")) ImGui.CloseCurrentPopup();

        ImGui.EndPopup();
    }

    private void DrawDeletePopup()
    {
        if (!ImGui.BeginPopup("##deleteProfile")) return;

        var target = plugin.Configuration.Profiles.GetValueOrDefault(pendingDeleteId);
        ImGui.TextWrapped(target != null
            ? $"Delete profile \"{target.Name}\"?\nCharacters using it will fall back to the default profile."
            : "Profile not found.");

        if (ImGui.Button("Delete") && target != null)
        {
            plugin.Configuration.DeleteProfile(pendingDeleteId);
            if (editingProfileId == pendingDeleteId)
                editingProfileId = plugin.Configuration.DefaultProfileId;
            plugin.SaveConfiguration();
            plugin.RebuildDiscordSender();
            ImGui.CloseCurrentPopup();
        }
        ImGui.SameLine();
        if (ImGui.Button("Cancel")) ImGui.CloseCurrentPopup();

        ImGui.EndPopup();
    }
}
