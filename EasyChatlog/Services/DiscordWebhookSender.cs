using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.Text;
using Dalamud.Plugin.Services;
using EasyChatlog.Models;

namespace EasyChatlog.Services;

public sealed class DiscordWebhookSender : IDiscordSender
{
    private const int DiscordContentLimit = 1900; // a bit under 2000 for safety/codeblock fences
    private const int DiscordUsernameLimit = 80;

    private readonly HttpClient http = new();
    private readonly CharacterConfig config;
    private readonly IPluginLog log;

    public DiscordWebhookSender(CharacterConfig config, IPluginLog log)
    {
        this.config = config;
        this.log = log;
    }

    public Task SendBatchAsync(IReadOnlyList<ChatLogEntry> entries, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(config.WebhookUrl))
        {
            log.Warning("Discord webhook URL is empty — skipping send.");
            return Task.CompletedTask;
        }

        return config.PerSenderIdentity
            ? SendPerSenderAsync(entries, ct)
            : SendLegacyAsync(entries, ct);
    }

    public Task SendRawAsync(string content, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(config.WebhookUrl))
        {
            log.Warning("Discord webhook URL is empty — skipping send.");
            return Task.CompletedTask;
        }
        return SendChunksAsync(SplitForDiscord(content), defaultPayload: true, username: null, avatarUrl: null, threadId: 0UL, ct);
    }

    // ---- Per-sender path -----------------------------------------------------------------

    private async Task SendPerSenderAsync(IReadOnlyList<ChatLogEntry> entries, CancellationToken ct)
    {
        foreach (var run in GroupRuns(entries, config.Threading))
        {
            var sender = ResolveSenderName(run[0]);
            var username = Truncate(string.IsNullOrEmpty(sender) ? (config.WebhookUsername ?? "FFXIV Chat") : sender, DiscordUsernameLimit);
            var avatar = config.UseIdenticonAvatar && !string.IsNullOrEmpty(sender)
                ? BuildIdenticonUrl(sender)
                : null;
            var threadId = ResolveThreadId(run[0]);

            await SendChunksAsync(FormatRun(run), defaultPayload: false, username, avatar, threadId, ct).ConfigureAwait(false);
        }
    }

    private ulong ResolveThreadId(ChatLogEntry e)
    {
        if (config.Threading == ThreadingMode.Off) return 0UL;
        var key = ThreadRouter.GetKey(e, config.Threading);
        if (key == null) return 0UL;
        return config.WebhookThreadOverrides.TryGetValue(key, out var id) ? id : 0UL;
    }

    /// <summary>
    /// Yields runs of consecutive entries that share (sender, type). Each run is a single
    /// webhook POST. When threading is on, runs are also broken on thread-key changes so
    /// every POST has a single thread destination.
    /// </summary>
    internal static IEnumerable<List<ChatLogEntry>> GroupRuns(IReadOnlyList<ChatLogEntry> entries, ThreadingMode mode)
    {
        var run = new List<ChatLogEntry>();
        string? lastSender = null;
        XivChatType? lastType = null;
        string? lastKey = null;

        foreach (var e in entries)
        {
            var sender = ResolveSenderName(e);
            var key = ThreadRouter.GetKey(e, mode);

            if (run.Count > 0 && (sender != lastSender || e.Type != lastType || key != lastKey))
            {
                yield return run;
                run = new List<ChatLogEntry>();
            }

            run.Add(e);
            lastSender = sender;
            lastType = e.Type;
            lastKey = key;
        }
        if (run.Count > 0) yield return run;
    }

    private static IEnumerable<string> FormatRun(IReadOnlyList<ChatLogEntry> run)
    {
        var sb = new StringBuilder();
        foreach (var e in run)
        {
            var line = FormatLineNoSender(e);
            if (sb.Length + line.Length + 1 > DiscordContentLimit)
            {
                yield return sb.ToString();
                sb.Clear();
            }
            sb.AppendLine(line);
        }
        if (sb.Length > 0) yield return sb.ToString();
    }

    internal static string FormatLineNoSender(ChatLogEntry e)
    {
        var ts = e.Timestamp.ToString("HH:mm:ss");
        return e.Type switch
        {
            XivChatType.TellOutgoing => $"`[{ts}]` >> {Escape(e.Message)}",
            XivChatType.TellIncoming => $"`[{ts}]` {Escape(e.Message)}",
            _                        => $"`[{ts}] [{e.TypeLabel}]` {Escape(e.Message)}",
        };
    }

    private static string ResolveSenderName(ChatLogEntry e) =>
        e.Type == XivChatType.TellOutgoing && !string.IsNullOrEmpty(e.LocalPlayerName)
            ? e.LocalPlayerName
            : e.Sender ?? "";

    private static string BuildIdenticonUrl(string seed) =>
        $"https://api.dicebear.com/7.x/identicon/png?seed={Uri.EscapeDataString(seed)}";

    // ---- Legacy path (PerSenderIdentity off) ---------------------------------------------

    private Task SendLegacyAsync(IReadOnlyList<ChatLogEntry> entries, CancellationToken ct)
        => SendChunksAsync(FormatBatch(entries), defaultPayload: true, username: null, avatarUrl: null, threadId: 0UL, ct);

    internal static IEnumerable<string> FormatBatch(IReadOnlyList<ChatLogEntry> entries)
    {
        var sb = new StringBuilder();
        foreach (var e in entries)
        {
            var line = FormatLine(e);
            if (sb.Length + line.Length + 1 > DiscordContentLimit)
            {
                yield return sb.ToString();
                sb.Clear();
            }
            sb.AppendLine(line);
        }
        if (sb.Length > 0) yield return sb.ToString();
    }

    internal static string FormatLine(ChatLogEntry e)
    {
        var ts = e.Timestamp.ToString("HH:mm:ss");

        switch (e.Type)
        {
            case XivChatType.TellOutgoing:
            {
                var name = string.IsNullOrEmpty(e.LocalPlayerName) ? e.Sender : e.LocalPlayerName;
                return $"`[{ts}]` **{Escape(name)} >>** {Escape(e.Message)}";
            }

            case XivChatType.TellIncoming:
                return $"`[{ts}]` **{Escape(e.Sender)}** {Escape(e.Message)}";

            default:
            {
                var sender = string.IsNullOrEmpty(e.Sender) ? "" : $"**{Escape(e.Sender)}** ";
                return $"`[{ts}] [{e.TypeLabel}]` {sender}{Escape(e.Message)}";
            }
        }
    }

    // ---- HTTP ----------------------------------------------------------------------------

    private async Task SendChunksAsync(IEnumerable<string> chunks, bool defaultPayload, string? username, string? avatarUrl, ulong threadId, CancellationToken ct)
    {
        foreach (var chunk in chunks)
        {
            await PostOneAsync(chunk, defaultPayload, username, avatarUrl, threadId, ct).ConfigureAwait(false);
        }
    }

    private async Task PostOneAsync(string content, bool defaultPayload, string? username, string? avatarUrl, ulong threadId, CancellationToken ct)
    {
        object payload;
        if (defaultPayload)
        {
            payload = new
            {
                username = string.IsNullOrWhiteSpace(config.WebhookUsername) ? "FFXIV Chat" : config.WebhookUsername,
                content,
                allowed_mentions = new { parse = Array.Empty<string>() },
            };
        }
        else if (avatarUrl != null)
        {
            payload = new
            {
                username,
                avatar_url = avatarUrl,
                content,
                allowed_mentions = new { parse = Array.Empty<string>() },
            };
        }
        else
        {
            payload = new
            {
                username,
                content,
                allowed_mentions = new { parse = Array.Empty<string>() },
            };
        }

        var url = threadId == 0UL
            ? config.WebhookUrl
            : AppendQuery(config.WebhookUrl, $"thread_id={threadId}");

        for (var attempt = 0; attempt < 3; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            using var resp = await http.PostAsJsonAsync(url, payload, ct).ConfigureAwait(false);
            if (resp.IsSuccessStatusCode) return;

            if ((int)resp.StatusCode == 429)
            {
                var retryAfter = resp.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(2);
                log.Warning("Discord webhook 429 — retrying after {Delay}", retryAfter);
                await Task.Delay(retryAfter, ct).ConfigureAwait(false);
                continue;
            }

            var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            log.Error("Discord webhook failed: {Status} {Body}", resp.StatusCode, body);
            return;
        }
    }

    private static string AppendQuery(string url, string query)
        => url.Contains('?') ? $"{url}&{query}" : $"{url}?{query}";

    private static string Escape(string s)
        // Mute @everyone/@here and other mention shapes.
        => (s ?? "").Replace("@everyone", "@​everyone").Replace("@here", "@​here");

    private static string Truncate(string s, int max)
        => string.IsNullOrEmpty(s) || s.Length <= max ? s : s[..max];

    private static IEnumerable<string> SplitForDiscord(string content)
    {
        if (content.Length <= DiscordContentLimit)
        {
            yield return content;
            yield break;
        }

        for (var i = 0; i < content.Length; i += DiscordContentLimit)
        {
            yield return content.Substring(i, Math.Min(DiscordContentLimit, content.Length - i));
        }
    }

    public void Dispose() => http.Dispose();
}
