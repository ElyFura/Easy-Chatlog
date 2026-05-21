using System;
using Dalamud.Game.Text;
using EasyChatlog.Models;

namespace EasyChatlog.Services;

/// <summary>
/// Decides which Discord thread a given chat entry belongs to.
/// Pure routing logic — no Discord calls, the senders use the returned key/name.
/// </summary>
public static class ThreadRouter
{
    /// <summary>
    /// Logical key for a chat entry under the chosen threading mode.
    /// Tell partners are normalized so outgoing and incoming with the same person share one thread.
    /// Returns null when the entry should go to the parent channel (no thread).
    /// </summary>
    public static string? GetKey(ChatLogEntry e, ThreadingMode mode)
    {
        switch (mode)
        {
            case ThreadingMode.Off:
                return null;

            case ThreadingMode.PerTellPartner:
                if (e.Type == XivChatType.TellIncoming)
                    return string.IsNullOrEmpty(e.Sender) ? null : $"tell:{e.Sender}";
                if (e.Type == XivChatType.TellOutgoing)
                    // Sender holds the recipient for outgoing tells (see ChatLogEntry).
                    return string.IsNullOrEmpty(e.Sender) ? null : $"tell:{e.Sender}";
                return null;

            case ThreadingMode.PerChannelType:
                return $"channel:{e.Type}";

            case ThreadingMode.PerSender:
                // For outgoing tells, group under the recipient so both sides land in one thread.
                var who = e.Type == XivChatType.TellOutgoing ? e.Sender : e.Sender;
                return string.IsNullOrEmpty(who) ? $"channel:{e.Type}" : $"sender:{who}";

            default:
                return null;
        }
    }

    /// <summary>
    /// Human-readable thread name for a logical key. Template placeholders:
    /// {key} — raw key, {type} — channel/category prefix, {sender} — partner/sender part.
    /// </summary>
    public static string RenderThreadName(string key, ChatLogEntry sample, string template)
    {
        if (string.IsNullOrWhiteSpace(template)) template = "{key}";

        var (kind, value) = SplitKey(key);
        var typeLabel = kind switch
        {
            "tell"    => "Tell",
            "channel" => value,
            "sender"  => "Chat",
            _ => kind,
        };

        var senderLabel = kind == "channel" ? sample.Sender : value;

        var name = template
            .Replace("{key}", PrettyKey(kind, value))
            .Replace("{type}", typeLabel)
            .Replace("{sender}", senderLabel ?? "");

        // Discord thread names: max 100 chars.
        return Truncate(name.Trim(), 100);
    }

    private static (string Kind, string Value) SplitKey(string key)
    {
        var i = key.IndexOf(':');
        return i < 0 ? (key, "") : (key[..i], key[(i + 1)..]);
    }

    private static string PrettyKey(string kind, string value) => kind switch
    {
        "tell"    => $"Tell - {value}",
        "channel" => value,
        "sender"  => value,
        _ => $"{kind}:{value}",
    };

    private static string Truncate(string s, int max)
        => string.IsNullOrEmpty(s) || s.Length <= max ? s : s[..max];
}
