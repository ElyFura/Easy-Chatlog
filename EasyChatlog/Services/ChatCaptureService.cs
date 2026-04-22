using System;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
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

    private void OnChatMessage(
        XivChatType type,
        int timestamp,
        ref SeString sender,
        ref SeString message,
        ref bool isHandled)
    {
        try
        {
            var cfg = getConfig();
            if (!cfg.EnabledChannels.TryGetValue(type, out var enabled) || !enabled)
                return;

            if ((type == XivChatType.TellIncoming || type == XivChatType.TellOutgoing) && !cfg.IncludeTells)
                return;

            var entry = new ChatLogEntry
            {
                Timestamp = DateTime.Now,
                Type = type,
                Sender = sender.TextValue,
                Message = message.TextValue,
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
}
