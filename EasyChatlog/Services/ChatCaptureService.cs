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
                Sender = FormatSender(msg.Sender),
                Message = msg.Message.TextValue,
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
}
