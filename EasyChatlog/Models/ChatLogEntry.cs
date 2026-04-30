using System;
using Dalamud.Game.Text;

namespace EasyChatlog.Models;

public sealed class ChatLogEntry
{
    public DateTime Timestamp { get; init; }
    public XivChatType Type { get; init; }
    public string Sender { get; init; } = "";
    public string Message { get; init; } = "";

    /// <summary>
    /// Local character name at the time the entry was captured. Only populated for
    /// <see cref="XivChatType.TellOutgoing"/>, where <see cref="Sender"/> holds the recipient.
    /// </summary>
    public string LocalPlayerName { get; init; } = "";

    public string TypeLabel => Type.ToString();
}
