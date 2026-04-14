using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using EasyChatlog.Models;

namespace EasyChatlog.Services;

public sealed class DiscordWebhookSender : IDiscordSender
{
    private const int DiscordContentLimit = 1900; // a bit under 2000 for safety/codeblock fences

    private readonly HttpClient http = new();
    private readonly Configuration config;
    private readonly IPluginLog log;

    public DiscordWebhookSender(Configuration config, IPluginLog log)
    {
        this.config = config;
        this.log = log;
    }

    public Task SendBatchAsync(IReadOnlyList<ChatLogEntry> entries, CancellationToken ct)
        => SendChunksAsync(FormatBatch(entries), ct);

    public Task SendRawAsync(string content, CancellationToken ct)
        => SendChunksAsync(SplitForDiscord(content), ct);

    private async Task SendChunksAsync(IEnumerable<string> chunks, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(config.WebhookUrl))
        {
            log.Warning("Discord webhook URL is empty — skipping send.");
            return;
        }

        foreach (var chunk in chunks)
        {
            await PostOneAsync(chunk, ct).ConfigureAwait(false);
        }
    }

    private async Task PostOneAsync(string content, CancellationToken ct)
    {
        var payload = new
        {
            username = string.IsNullOrWhiteSpace(config.WebhookUsername) ? "FFXIV Chat" : config.WebhookUsername,
            content,
            allowed_mentions = new { parse = Array.Empty<string>() },
        };

        for (var attempt = 0; attempt < 3; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            using var resp = await http.PostAsJsonAsync(config.WebhookUrl, payload, ct).ConfigureAwait(false);
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
        var sender = string.IsNullOrEmpty(e.Sender) ? "" : $"**{Escape(e.Sender)}** ";
        return $"`[{e.Timestamp:HH:mm:ss}] [{e.TypeLabel}]` {sender}{Escape(e.Message)}";
    }

    private static string Escape(string s)
        // Mute @everyone/@here and other mention shapes.
        => s.Replace("@everyone", "@\u200beveryone").Replace("@here", "@\u200bhere");

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
