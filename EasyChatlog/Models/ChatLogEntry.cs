using System;
using Dalamud.Game.Text;

namespace EasyChatlog.Models;

public sealed class ChatLogEntry
{
    public DateTime Timestamp { get; init; }
    public XivChatType Type { get; init; }
    public string Sender { get; init; } = "";
    public string Message { get; init; } = "";

    public string TypeLabel => Type.ToString();
}
