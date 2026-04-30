using System;
using System.Text;
using Dalamud.Game.Chat;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using EasyChatlog.Models;

namespace EasyChatlog.Services;

public sealed class ChatCaptureService : IDisposable
{
    private readonly IChatGui chatGui;
    private readonly IPluginLog log;
    private readonly Func<CharacterConfig> getConfig;
    private readonly ChatBuffer buffer;

    public ChatCaptureService(IChatGui chatGui, IPluginLog log, Func<CharacterConfig> getConfig, ChatBuffer buffer)
    {
        this.chatGui = chatGui;
        this.log = log;
        this.getConfig = getConfig;
        this.buffer = buffer;

        this.chatGui.ChatMessage += OnChatMessage;
    }

    private void OnChatMessage(IHandleableChatMessage msg)
    {
        try
        {
            var type = msg.LogKind;

            var cfg = getConfig();
            if (!cfg.EnabledChannels.TryGetValue(type, out var enabled) || !enabled)
                return;

            if ((type == XivChatType.TellIncoming || type == XivChatType.TellOutgoing) && !cfg.IncludeTells)
                return;

            var localName = type == XivChatType.TellOutgoing
                ? Plugin.PlayerState.CharacterName ?? ""
                : "";

            var entry = new ChatLogEntry
            {
                Timestamp = DateTime.Now,
                Type = type,
                Sender = ReplaceGamefontGlyphs(FormatSender(msg.Sender)),
                Message = ReplaceGamefontGlyphs(FormatMessage(msg.Message)),
                LocalPlayerName = localName,
            };

            buffer.TryEnqueue(entry);
        }
        catch (Exception ex)
        {
            // Never throw on the game thread.
            log.Error(ex, "ChatCapture failed");
        }
    }

    public void Dispose()
    {
        chatGui.ChatMessage -= OnChatMessage;
    }

    /// <summary>
    /// Cross-world senders carry a world-icon glyph between name and server. The glyph has
    /// no plain-text form, so <c>SeString.TextValue</c> drops it and the two run together
    /// (e.g. "Fabson ValtherisTwintania"). Walk the payloads and substitute '@' for the icon.
    /// </summary>
    private static string FormatSender(SeString sender)
    {
        if (sender.Payloads.Count == 0)
            return sender.TextValue;

        var sb = new StringBuilder();
        foreach (var payload in sender.Payloads)
        {
            switch (payload)
            {
                case TextPayload tp when !string.IsNullOrEmpty(tp.Text):
                    sb.Append(tp.Text);
                    break;
                case IconPayload:
                    if (sb.Length > 0 && sb[^1] != '@' && sb[^1] != ' ')
                        sb.Append('@');
                    break;
                // PlayerPayload is skipped: API 15 wraps the name in a PlayerPayload
                // *and* renders the displayed name as adjacent TextPayloads, so
                // emitting both would duplicate the name (e.g. "Aurora CalcisAurora Calcis").
            }
        }

        var s = sb.ToString().Trim();
        return string.IsNullOrEmpty(s) ? sender.TextValue : s;
    }

    /// <summary>
    /// Concatenate the text-bearing payloads of a chat message. Mirrors the
    /// behaviour of <see cref="SeString.TextValue"/> for plain text but also
    /// resolves auto-translate phrases and pulls printable ASCII from custom
    /// <c>RawPayload</c> chunks (third-party chat plugins sometimes inject
    /// tag text that way). Styling/structural payloads are dropped — their
    /// visible glyphs are emitted as adjacent <see cref="TextPayload"/>s.
    /// </summary>
    private static string FormatMessage(SeString message)
    {
        if (message.Payloads.Count == 0)
            return message.TextValue;

        var sb = new StringBuilder();
        foreach (var payload in message.Payloads)
        {
            switch (payload)
            {
                case TextPayload tp:
                    if (!string.IsNullOrEmpty(tp.Text))
                        sb.Append(tp.Text);
                    break;

                case AutoTranslatePayload at:
                    var atText = at.Text;
                    if (!string.IsNullOrEmpty(atText))
                        sb.Append(atText);
                    break;

                case RawPayload raw:
                    var rawText = ExtractPrintableAscii(raw.Data);
                    if (!string.IsNullOrEmpty(rawText))
                        sb.Append(rawText);
                    break;
            }
        }

        var s = sb.ToString();
        return string.IsNullOrEmpty(s) ? message.TextValue : s;
    }

    private static string ExtractPrintableAscii(byte[] data)
    {
        if (data.Length == 0) return string.Empty;
        var sb = new StringBuilder(data.Length);
        foreach (var b in data)
            if (b >= 0x20 && b <= 0x7E) sb.Append((char)b);
        return sb.ToString();
    }

    /// <summary>
    /// FFXIV renders many in-chat tags (PF datacenter labels like "TBU"/"EU",
    /// the "ST" ordinal badge, the cross-world icon, role symbols, …) as
    /// glyphs from the game font's Private Use Area (U+E000–U+F8FF). They
    /// survive <c>SeString.TextValue</c> as Unicode chars but are invisible
    /// in Discord/exports because no other font has them. Replace known
    /// glyphs with readable ASCII and strip the rest.
    /// </summary>
    internal static string ReplaceGamefontGlyphs(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;

        var hasPua = false;
        foreach (var c in s)
            if (c >= 0xE000 && c <= 0xF8FF) { hasPua = true; break; }
        if (!hasPua) return s;

        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
        {
            if (c >= 0xE071 && c <= 0xE08A)
            {
                sb.Append((char)('A' + (c - 0xE071)));
                continue;
            }
            if (c >= 0xE060 && c <= 0xE069)
            {
                sb.Append((char)('0' + (c - 0xE060)));
                continue;
            }

            switch ((int)c)
            {
                case 0xE0BB: sb.Append('@'); break;     // cross-world icon
                case 0xE040: break;                     // auto-translate open
                case 0xE041: break;                     // auto-translate close
                case 0xE0D1: sb.Append("[ST]"); break;  // ordinal "ST" badge
                default:
                    if (c < 0xE000 || c > 0xF8FF)
                        sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }
}
