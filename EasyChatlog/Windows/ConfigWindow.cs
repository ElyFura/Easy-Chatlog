using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;
using Dalamud.Interface.Windowing;
using EasyChatlog.Localization;
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
        : base(Loc.S.ConfigTitle + "###EasyChatlogConfig",
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
        WindowName = (!string.IsNullOrEmpty(charName)
            ? string.Format(Loc.S.ConfigTitleFmt, charName)
            : Loc.S.ConfigTitle) + "###EasyChatlogConfig";

        var config = plugin.Configuration;

        // Keep editingProfileId valid; default to the active character's profile when entering.
        if (!config.Profiles.ContainsKey(editingProfileId))
            editingProfileId = plugin.ActiveProfile.Id;

        var profile = config.Profiles[editingProfileId];
        var cfg = profile.Config;
        var changed = false;

        DrawLanguageSelector();
        ImGui.Separator();

        DrawProfileBar(ref changed);
        ImGui.Separator();

        if (ImGui.CollapsingHeader(Loc.S.HeaderDiscord + "###ecDiscord", ImGuiTreeNodeFlags.DefaultOpen))
        {
            var enabled = cfg.DiscordEnabled;
            if (ImGui.Checkbox(Loc.S.ForwardToDiscord, ref enabled))
            {
                cfg.DiscordEnabled = enabled;
                changed = true;
            }

            var modeIdx = (int)cfg.Mode;
            if (ImGui.RadioButton(Loc.S.ModeWebhook, ref modeIdx, (int)DiscordMode.Webhook)) { cfg.Mode = DiscordMode.Webhook; changed = true; plugin.RebuildDiscordSender(); }
            ImGui.SameLine();
            if (ImGui.RadioButton(Loc.S.ModeBot,     ref modeIdx, (int)DiscordMode.Bot))     { cfg.Mode = DiscordMode.Bot;     changed = true; plugin.RebuildDiscordSender(); }

            ImGui.Spacing();

            if (cfg.Mode == DiscordMode.Webhook)
            {
                var url = cfg.WebhookUrl;
                if (ImGui.InputText(Loc.S.WebhookUrl, ref url, 512))
                {
                    cfg.WebhookUrl = url;
                    changed = true;
                }

                var name = cfg.WebhookUsername;
                if (ImGui.InputText(Loc.S.DisplayName, ref name, 80))
                {
                    cfg.WebhookUsername = name;
                    changed = true;
                }
            }
            else
            {
                var token = cfg.BotToken;
                if (ImGui.InputText(Loc.S.BotToken, ref token, 200, ImGuiInputTextFlags.Password))
                {
                    cfg.BotToken = token;
                    changed = true;
                }

                var chId = cfg.BotChannelId.ToString();
                if (ImGui.InputText(Loc.S.ChannelId, ref chId, 32, ImGuiInputTextFlags.CharsDecimal))
                {
                    cfg.BotChannelId = ulong.TryParse(chId, out var v) ? v : 0UL;
                    changed = true;
                }
            }

            if (ImGui.Button(Loc.S.TestSend))
            {
                _ = plugin.SendDiscordTestAsync();
            }
        }

        if (ImGui.CollapsingHeader(Loc.S.HeaderRendering + "###ecRendering", ImGuiTreeNodeFlags.DefaultOpen))
        {
            var perSender = cfg.PerSenderIdentity;
            if (ImGui.Checkbox(Loc.S.PerSenderIdentity, ref perSender))
            {
                cfg.PerSenderIdentity = perSender;
                changed = true;
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(Loc.S.PerSenderTooltip);

            ImGui.BeginDisabled(!cfg.PerSenderIdentity);
            var ident = cfg.UseIdenticonAvatar;
            if (ImGui.Checkbox(Loc.S.UseIdenticon, ref ident))
            {
                cfg.UseIdenticonAvatar = ident;
                changed = true;
            }
            ImGui.EndDisabled();
        }

        if (ImGui.CollapsingHeader(Loc.S.HeaderThreads + "###ecThreads"))
            DrawThreadingSection(cfg, ref changed);

        if (ImGui.CollapsingHeader(Loc.S.HeaderBuffer + "###ecBuffer", ImGuiTreeNodeFlags.DefaultOpen))
        {
            var n = cfg.FlushAfterMessages;
            if (ImGui.SliderInt(Loc.S.FlushAfterMessages, ref n, 1, 100)) { cfg.FlushAfterMessages = n; changed = true; }

            var s = cfg.FlushAfterSeconds;
            if (ImGui.SliderInt(Loc.S.FlushAfterSeconds, ref s, 1, 60)) { cfg.FlushAfterSeconds = s; changed = true; }

            var hist = cfg.InMemoryHistorySize;
            if (ImGui.SliderInt(Loc.S.InMemoryHistory, ref hist, 100, 50_000)) { cfg.InMemoryHistorySize = hist; changed = true; }
        }

        if (ImGui.CollapsingHeader(Loc.S.HeaderChannels + "###ecChannels"))
        {
            var tells = cfg.IncludeTells;
            if (ImGui.Checkbox(Loc.S.IncludeTells, ref tells))
            {
                cfg.IncludeTells = tells;
                changed = true;
            }

            ImGui.Separator();

            foreach (var (group, types) in ChannelFilter.Groups)
            {
                ImGui.TextDisabled(Loc.S.ChannelGroup(group));
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

        if (ImGui.CollapsingHeader(Loc.S.HeaderExport + "###ecExport"))
        {
            var dir = string.IsNullOrEmpty(cfg.ExportDirectory) ? plugin.DefaultExportDirectory : cfg.ExportDirectory;
            if (ImGui.InputText(Loc.S.ExportDirectory, ref dir, 260))
            {
                cfg.ExportDirectory = dir;
                changed = true;
            }

            var fmtIdx = (int)cfg.DefaultExportFormat;
            string[] fmts = { Loc.S.FormatTxt, Loc.S.FormatJson, Loc.S.FormatHtml, Loc.S.FormatMarkdown };
            if (ImGui.Combo(Loc.S.DefaultFormat, ref fmtIdx, fmts, fmts.Length))
            {
                cfg.DefaultExportFormat = (ExportFormat)fmtIdx;
                changed = true;
            }
        }

        if (ImGui.CollapsingHeader(Loc.S.HeaderAssignments + "###ecAssign"))
            DrawCharacterAssignments(ref changed);

        if (changed) plugin.SaveConfiguration();

        DrawNewPopup();
        DrawRenamePopup();
        DrawDeletePopup();
    }

    // --- Language ------------------------------------------------------------------------

    private static readonly Language[] LanguageValues =
        [Language.Auto, Language.English, Language.German];

    /// <summary>
    /// Label is bilingual so the setting stays findable no matter which language is active.
    /// </summary>
    private void DrawLanguageSelector()
    {
        string[] labels = [Loc.S.LanguageAuto, Loc.S.LanguageEnglish, Loc.S.LanguageGerman];

        var idx = Array.IndexOf(LanguageValues, plugin.Configuration.Language);
        if (idx < 0) idx = 0;

        ImGui.Text(Loc.S.LanguageLabel);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(220);
        if (ImGui.Combo("##ecLanguage", ref idx, labels, labels.Length))
            plugin.SetLanguage(LanguageValues[idx]);
    }

    // --- Threading -----------------------------------------------------------------------

    // Rebuilt per frame so a language switch takes effect immediately.
    private static string[] ThreadingLabels =>
        [Loc.S.ThreadingOff, Loc.S.ThreadingPerTell, Loc.S.ThreadingPerChannel, Loc.S.ThreadingPerSender];
    private static string[] ArchiveLabels =>
        [Loc.S.ArchiveOneHour, Loc.S.ArchiveOneDay, Loc.S.ArchiveThreeDays, Loc.S.ArchiveOneWeek];
    private static readonly ThreadAutoArchive[] ArchiveValues =
        { ThreadAutoArchive.OneHour, ThreadAutoArchive.OneDay, ThreadAutoArchive.ThreeDays, ThreadAutoArchive.OneWeek };

    private string newThreadKey = "";
    private string newThreadId  = "";

    private void DrawThreadingSection(CharacterConfig cfg, ref bool changed)
    {
        if (ImGui.SmallButton(Loc.S.HelpButton + "##threadHelp"))
            ImGui.OpenPopup("##threadHelp");
        ImGui.SameLine();
        ImGui.TextDisabled(Loc.S.ThreadHelpHint);

        DrawThreadHelpPopup(cfg);

        var threadingLabels = ThreadingLabels;
        var modeIdx = (int)cfg.Threading;
        if (ImGui.Combo(Loc.S.ThreadingMode, ref modeIdx, threadingLabels, threadingLabels.Length))
        {
            cfg.Threading = (ThreadingMode)modeIdx;
            changed = true;
        }

        var archiveLabels = ArchiveLabels;
        var archiveIdx = Array.IndexOf(ArchiveValues, cfg.ThreadArchive);
        if (archiveIdx < 0) archiveIdx = 1;
        if (ImGui.Combo(Loc.S.AutoArchive, ref archiveIdx, archiveLabels, archiveLabels.Length))
        {
            cfg.ThreadArchive = ArchiveValues[archiveIdx];
            changed = true;
        }

        var tmpl = cfg.ThreadNameTemplate;
        if (ImGui.InputText(Loc.S.ThreadNameTemplate, ref tmpl, 80))
        {
            cfg.ThreadNameTemplate = tmpl;
            changed = true;
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(Loc.S.ThreadTemplateTip);

        ImGui.Spacing();

        if (cfg.Mode == DiscordMode.Bot)
        {
            ImGui.TextDisabled(Loc.S.BotAutoCreates);
            ImGui.TextDisabled(Loc.S.BotRequiredPerms);
            ImGui.Spacing();
            DrawKnownThreads(cfg, ref changed);
        }
        else
        {
            ImGui.TextDisabled(Loc.S.WebhookNoThreads1);
            ImGui.TextDisabled(Loc.S.WebhookNoThreads2);
            ImGui.Spacing();
            DrawWebhookOverrides(cfg, ref changed);
        }
    }

    private void DrawKnownThreads(CharacterConfig cfg, ref bool changed)
    {
        if (cfg.ThreadMap.Count == 0)
        {
            ImGui.TextDisabled(Loc.S.NoThreadsYet);
            return;
        }

        if (ImGui.BeginTable("##knownThreads", 3, ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.Borders))
        {
            ImGui.TableSetupColumn(Loc.S.ColumnKey);
            ImGui.TableSetupColumn(Loc.S.ColumnThreadId);
            ImGui.TableSetupColumn("");
            ImGui.TableHeadersRow();

            foreach (var (key, id) in cfg.ThreadMap.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase).ToList())
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(key);
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(id.ToString());
                ImGui.TableNextColumn();
                if (ImGui.SmallButton($"{Loc.S.Forget}##fk_{key}"))
                {
                    cfg.ThreadMap.Remove(key);
                    changed = true;
                }
            }
            ImGui.EndTable();
        }
    }

    private void DrawThreadHelpPopup(CharacterConfig cfg)
    {
        ImGui.SetNextWindowSize(new Vector2(620, 460), ImGuiCond.Appearing);
        if (!ImGui.BeginPopupModal("##threadHelp", ImGuiWindowFlags.NoCollapse)) return;

        ImGui.TextUnformatted(Loc.S.HelpRoutingKeys);
        ImGui.Separator();
        ImGui.TextWrapped(Loc.S.HelpRoutingIntro);
        ImGui.Spacing();

        ImGui.BulletText(Loc.S.HelpBulletOff);
        ImGui.BulletText(Loc.S.HelpBulletTell);
        ImGui.BulletText(Loc.S.HelpBulletChannel);
        ImGui.BulletText(Loc.S.HelpBulletSender);

        ImGui.Spacing();
        ImGui.TextUnformatted(Loc.S.HelpExamples);
        ImGui.Separator();
        ImGui.BulletText(Loc.S.HelpExSay);
        ImGui.BulletText(Loc.S.HelpExParty);
        ImGui.BulletText(Loc.S.HelpExLs1);
        ImGui.BulletText("channel:FreeCompany");
        ImGui.BulletText(Loc.S.HelpExTell);
        ImGui.BulletText("sender:Foo Bar@Phoenix");

        ImGui.Spacing();
        ImGui.TextUnformatted(Loc.S.HelpPlaceholders);
        ImGui.Separator();
        ImGui.BulletText(Loc.S.HelpPhKey);
        ImGui.BulletText(Loc.S.HelpPhType);
        ImGui.BulletText(Loc.S.HelpPhSender);

        ImGui.Spacing();
        ImGui.TextUnformatted(Loc.S.HelpWebhookSetup);
        ImGui.Separator();
        ImGui.TextWrapped(Loc.S.HelpWebhookBody);

        ImGui.Spacing();
        if (cfg.Threading == ThreadingMode.PerChannelType)
        {
            ImGui.TextUnformatted(Loc.S.HelpQuickFill);
            DrawChannelKeyPicker();
        }

        ImGui.Spacing();
        if (ImGui.Button(Loc.S.Close, new Vector2(120, 0))) ImGui.CloseCurrentPopup();
        ImGui.EndPopup();
    }

    private void DrawChannelKeyPicker()
    {
        if (!ImGui.BeginChild("##chanPicker", new Vector2(0, 160), true)) { ImGui.EndChild(); return; }
        foreach (var (group, types) in ChannelFilter.Groups)
        {
            ImGui.TextDisabled(group);
            foreach (var t in types)
            {
                var key = $"channel:{t}";
                if (ImGui.SmallButton($"{key}##pick_{t}"))
                {
                    newThreadKey = key;
                    ImGui.CloseCurrentPopup();
                }
                ImGui.SameLine();
            }
            ImGui.NewLine();
        }
        ImGui.EndChild();
    }

    private void DrawWebhookOverrides(CharacterConfig cfg, ref bool changed)
    {
        if (ImGui.BeginTable("##overrides", 3, ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.Borders))
        {
            ImGui.TableSetupColumn(Loc.S.ColumnKey);
            ImGui.TableSetupColumn(Loc.S.ColumnThreadId);
            ImGui.TableSetupColumn("");
            ImGui.TableHeadersRow();

            foreach (var (key, id) in cfg.WebhookThreadOverrides.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase).ToList())
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(key);
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(id.ToString());
                ImGui.TableNextColumn();
                if (ImGui.SmallButton($"{Loc.S.Remove}##rm_{key}"))
                {
                    cfg.WebhookThreadOverrides.Remove(key);
                    changed = true;
                }
            }
            ImGui.EndTable();
        }

        ImGui.SetNextItemWidth(180);
        ImGui.InputTextWithHint("##nkey", Loc.S.HintKey, ref newThreadKey, 120);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(180);
        ImGui.InputTextWithHint("##nid", Loc.S.HintThreadId, ref newThreadId, 32, ImGuiInputTextFlags.CharsDecimal);
        ImGui.SameLine();
        if (ImGui.Button(Loc.S.Add) && !string.IsNullOrWhiteSpace(newThreadKey) && ulong.TryParse(newThreadId, out var tid) && tid != 0)
        {
            cfg.WebhookThreadOverrides[newThreadKey.Trim()] = tid;
            newThreadKey = "";
            newThreadId = "";
            changed = true;
        }
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

        ImGui.Text(Loc.S.EditingProfile);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(220);
        if (ImGui.Combo("##editProfile", ref editIdx, names, names.Length))
            editingProfileId = ids[editIdx];

        if (editingProfileId == config.DefaultProfileId)
        {
            ImGui.SameLine();
            ImGui.TextDisabled(Loc.S.IsDefaultMarker);
        }

        ImGui.SameLine();
        if (ImGui.Button(Loc.S.New))
        {
            newNameBuffer = "";
            ImGui.OpenPopup("##newProfile");
        }
        ImGui.SameLine();
        if (ImGui.Button(Loc.S.Rename))
        {
            renameBuffer = config.Profiles[editingProfileId].Name;
            ImGui.OpenPopup("##renameProfile");
        }
        ImGui.SameLine();
        if (ImGui.Button(Loc.S.Duplicate))
        {
            var src = config.Profiles[editingProfileId];
            var dup = config.CreateProfile(src.Name + Loc.S.CopySuffix, src.Config);
            editingProfileId = dup.Id;
            changed = true;
        }
        ImGui.SameLine();
        ImGui.BeginDisabled(config.Profiles.Count <= 1);
        if (ImGui.Button(Loc.S.Delete))
        {
            pendingDeleteId = editingProfileId;
            ImGui.OpenPopup("##deleteProfile");
        }
        ImGui.EndDisabled();

        // Set-as-default toggle — makes new / unassigned characters use this profile.
        if (editingProfileId != config.DefaultProfileId)
        {
            if (ImGui.SmallButton(Loc.S.SetAsDefault))
            {
                config.DefaultProfileId = editingProfileId;
                changed = true;
            }
            ImGui.SameLine();
            ImGui.TextDisabled(Loc.S.DefaultFallbackHint);
        }

        // Assignment for the logged-in character.
        var cid = Plugin.PlayerState.ContentId;
        if (cid != 0)
        {
            var charName = Plugin.PlayerState.CharacterName;
            if (string.IsNullOrEmpty(charName)) charName = string.Format(Loc.S.CharacterFallbackFmt, cid);

            var assignedId = config.CharacterProfileMap.GetValueOrDefault(cid, config.DefaultProfileId);
            var assignedIdx = Array.IndexOf(ids, assignedId);
            if (assignedIdx < 0) assignedIdx = 0;

            ImGui.Spacing();
            ImGui.Text(string.Format(Loc.S.ActiveProfileForFmt, charName));
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
                if (ImGui.SmallButton(Loc.S.EditThisOne))
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
            ImGui.TextDisabled(Loc.S.NoCharactersYet);
            return;
        }

        var profileIds = config.Profiles.Values
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Select(p => p.Id)
            .ToArray();
        var profileNames = profileIds.Select(id => config.Profiles[id].Name).ToArray();

        foreach (var cid in knownIds)
        {
            var label = config.CharacterNames.TryGetValue(cid, out var n) ? n : string.Format(Loc.S.CharacterFallbackFmt, cid);
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
            if (ImGui.SmallButton($"{Loc.S.Clear}##clr_{cid}"))
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

        ImGui.Text(Loc.S.NewProfilePrompt);
        ImGui.SetNextItemWidth(260);
        var submit = ImGui.InputText("##newName", ref newNameBuffer, 64, ImGuiInputTextFlags.EnterReturnsTrue);

        if (ImGui.Button(Loc.S.Create) || submit)
        {
            var p = plugin.Configuration.CreateProfile(newNameBuffer);
            editingProfileId = p.Id;
            plugin.SaveConfiguration();
            ImGui.CloseCurrentPopup();
        }
        ImGui.SameLine();
        if (ImGui.Button(Loc.S.Cancel)) ImGui.CloseCurrentPopup();

        ImGui.EndPopup();
    }

    private void DrawRenamePopup()
    {
        if (!ImGui.BeginPopup("##renameProfile")) return;

        ImGui.Text(Loc.S.RenameProfilePrompt);
        ImGui.SetNextItemWidth(260);
        var submit = ImGui.InputText("##rename", ref renameBuffer, 64, ImGuiInputTextFlags.EnterReturnsTrue);

        if ((ImGui.Button(Loc.S.Save) || submit)
            && plugin.Configuration.Profiles.TryGetValue(editingProfileId, out var p)
            && !string.IsNullOrWhiteSpace(renameBuffer))
        {
            p.Name = renameBuffer.Trim();
            plugin.SaveConfiguration();
            ImGui.CloseCurrentPopup();
        }
        ImGui.SameLine();
        if (ImGui.Button(Loc.S.Cancel)) ImGui.CloseCurrentPopup();

        ImGui.EndPopup();
    }

    private void DrawDeletePopup()
    {
        if (!ImGui.BeginPopup("##deleteProfile")) return;

        var target = plugin.Configuration.Profiles.GetValueOrDefault(pendingDeleteId);
        ImGui.TextWrapped(target != null
            ? string.Format(Loc.S.DeleteProfileFmt, target.Name)
            : Loc.S.ProfileNotFound);

        if (ImGui.Button(Loc.S.Delete) && target != null)
        {
            plugin.Configuration.DeleteProfile(pendingDeleteId);
            if (editingProfileId == pendingDeleteId)
                editingProfileId = plugin.Configuration.DefaultProfileId;
            plugin.SaveConfiguration();
            plugin.RebuildDiscordSender();
            ImGui.CloseCurrentPopup();
        }
        ImGui.SameLine();
        if (ImGui.Button(Loc.S.Cancel)) ImGui.CloseCurrentPopup();

        ImGui.EndPopup();
    }
}
