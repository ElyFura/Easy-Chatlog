using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.Text;
using Dalamud.Plugin.Services;
using Discord;
using Discord.WebSocket;
using EasyChatlog.Models;

namespace EasyChatlog.Services;

public sealed class DiscordBotSender : IDiscordSender
{
    private const int DiscordEmbedDescriptionLimit = 4000; // Discord caps at 4096, leave headroom
    private const int DiscordEmbedsPerMessage = 10;

    private readonly CharacterConfig config;
    private readonly IPluginLog log;
    private readonly Action? saveConfig;
    private readonly SemaphoreSlim startLock = new(1, 1);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> threadLocks = new();

    private DiscordSocketClient? client;
    private bool started;

    public DiscordBotSender(CharacterConfig config, IPluginLog log, Action? saveConfig = null)
    {
        this.config = config;
        this.log = log;
        this.saveConfig = saveConfig;
    }

    private async Task EnsureStartedAsync(CancellationToken ct)
    {
        if (started && client != null) return;

        await startLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (started && client != null) return;
            if (string.IsNullOrWhiteSpace(config.BotToken))
            {
                log.Warning("Discord bot token is empty.");
                return;
            }

            client = new DiscordSocketClient(new DiscordSocketConfig
            {
                LogLevel = LogSeverity.Warning,
                GatewayIntents = GatewayIntents.Guilds, // sending only — no message intent needed
            });
            client.Log += msg =>
            {
                log.Information("[Discord.Net] {Msg}", msg.ToString());
                return Task.CompletedTask;
            };

            await client.LoginAsync(TokenType.Bot, config.BotToken).ConfigureAwait(false);
            await client.StartAsync().ConfigureAwait(false);
            started = true;
        }
        finally
        {
            startLock.Release();
        }
    }

    public async Task SendBatchAsync(IReadOnlyList<ChatLogEntry> entries, CancellationToken ct)
    {
        await EnsureStartedAsync(ct).ConfigureAwait(false);
        if (client == null) return;
        if (config.BotChannelId == 0UL)
        {
            log.Warning("Discord bot channel ID not configured.");
            return;
        }

        await WaitForGatewayAsync(ct).ConfigureAwait(false);

        // Group consecutive entries by routing key so each chunk hits one destination.
        foreach (var (key, group) in SplitByThreadKey(entries))
        {
            var dest = await ResolveDestinationAsync(key, group.FirstOrDefault(), ct).ConfigureAwait(false);
            if (dest == null) continue;

            if (config.PerSenderIdentity)
                await SendAsEmbedsAsync(dest, group, ct).ConfigureAwait(false);
            else
                await SendAsPlainAsync(dest, group, ct).ConfigureAwait(false);
        }
    }

    public async Task SendRawAsync(string content, CancellationToken ct)
    {
        await EnsureStartedAsync(ct).ConfigureAwait(false);
        if (client == null) return;
        if (config.BotChannelId == 0UL)
        {
            log.Warning("Discord bot channel ID not configured.");
            return;
        }

        await WaitForGatewayAsync(ct).ConfigureAwait(false);
        if (client.GetChannel(config.BotChannelId) is not IMessageChannel ch)
        {
            log.Warning("Discord channel {Id} not visible to bot.", config.BotChannelId);
            return;
        }
        await ch.SendMessageAsync(content, allowedMentions: AllowedMentions.None).ConfigureAwait(false);
    }

    // ---- Destination resolution ----------------------------------------------------------

    private async Task<IMessageChannel?> ResolveDestinationAsync(string? key, ChatLogEntry? sample, CancellationToken ct)
    {
        var parent = client!.GetChannel(config.BotChannelId);
        if (parent is not IMessageChannel parentMsgCh)
        {
            log.Warning("Discord channel {Id} not visible to bot.", config.BotChannelId);
            return null;
        }

        if (key == null || sample == null || config.Threading == ThreadingMode.Off)
            return parentMsgCh;

        if (parent is not ITextChannel parentText)
        {
            // Threads only work in text channels; fall back to parent.
            return parentMsgCh;
        }

        var keyLock = threadLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await keyLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Cached?
            if (config.ThreadMap.TryGetValue(key, out var cachedId) && cachedId != 0UL)
            {
                if (client.GetChannel(cachedId) is SocketThreadChannel cached)
                {
                    if (cached.IsArchived)
                    {
                        try
                        {
                            await cached.ModifyAsync(p => p.Archived = false).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            log.Warning(ex, "Could not unarchive thread {Id}; creating a new one.", cachedId);
                            config.ThreadMap.Remove(key);
                            saveConfig?.Invoke();
                            return await CreateAndCacheThreadAsync(parentText, key, sample).ConfigureAwait(false);
                        }
                    }
                    return cached;
                }
                // Cached id no longer resolves — drop and recreate.
                config.ThreadMap.Remove(key);
                saveConfig?.Invoke();
            }

            return await CreateAndCacheThreadAsync(parentText, key, sample).ConfigureAwait(false);
        }
        finally
        {
            keyLock.Release();
        }
    }

    private async Task<IMessageChannel?> CreateAndCacheThreadAsync(ITextChannel parent, string key, ChatLogEntry sample)
    {
        var name = ThreadRouter.RenderThreadName(key, sample, config.ThreadNameTemplate);
        try
        {
            var thread = await parent.CreateThreadAsync(
                name,
                autoArchiveDuration: MapArchive(config.ThreadArchive),
                type: ThreadType.PublicThread).ConfigureAwait(false);

            config.ThreadMap[key] = thread.Id;
            saveConfig?.Invoke();
            return thread;
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to create Discord thread '{Name}' under channel {Id}", name, parent.Id);
            return parent; // fall back to parent
        }
    }

    private static ThreadArchiveDuration MapArchive(ThreadAutoArchive a) => a switch
    {
        ThreadAutoArchive.OneHour   => ThreadArchiveDuration.OneHour,
        ThreadAutoArchive.OneDay    => ThreadArchiveDuration.OneDay,
        ThreadAutoArchive.ThreeDays => ThreadArchiveDuration.ThreeDays,
        ThreadAutoArchive.OneWeek   => ThreadArchiveDuration.OneWeek,
        _ => ThreadArchiveDuration.OneDay,
    };

    private async Task WaitForGatewayAsync(CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (client!.ConnectionState != ConnectionState.Connected && DateTime.UtcNow < deadline)
            await Task.Delay(200, ct).ConfigureAwait(false);
    }

    private IEnumerable<(string? Key, List<ChatLogEntry> Group)> SplitByThreadKey(IReadOnlyList<ChatLogEntry> entries)
    {
        var group = new List<ChatLogEntry>();
        string? currentKey = null;
        var hasKey = false;

        foreach (var e in entries)
        {
            var key = ThreadRouter.GetKey(e, config.Threading);
            if (hasKey && key != currentKey)
            {
                yield return (currentKey, group);
                group = new List<ChatLogEntry>();
            }
            group.Add(e);
            currentKey = key;
            hasKey = true;
        }
        if (group.Count > 0) yield return (currentKey, group);
    }

    // ---- Sending: embeds vs plain --------------------------------------------------------

    private async Task SendAsEmbedsAsync(IMessageChannel dest, IReadOnlyList<ChatLogEntry> entries, CancellationToken ct)
    {
        var pendingEmbeds = new List<Embed>();

        foreach (var run in GroupRuns(entries))
        {
            foreach (var embed in BuildEmbedsForRun(run))
            {
                pendingEmbeds.Add(embed);
                if (pendingEmbeds.Count >= DiscordEmbedsPerMessage)
                {
                    await dest.SendMessageAsync(embeds: pendingEmbeds.ToArray(), allowedMentions: AllowedMentions.None).ConfigureAwait(false);
                    pendingEmbeds.Clear();
                }
            }
        }
        if (pendingEmbeds.Count > 0)
            await dest.SendMessageAsync(embeds: pendingEmbeds.ToArray(), allowedMentions: AllowedMentions.None).ConfigureAwait(false);
    }

    private static IEnumerable<List<ChatLogEntry>> GroupRuns(IReadOnlyList<ChatLogEntry> entries)
    {
        var run = new List<ChatLogEntry>();
        string? lastSender = null;
        XivChatType? lastType = null;

        foreach (var e in entries)
        {
            var s = ResolveSenderName(e);
            if (run.Count > 0 && (s != lastSender || e.Type != lastType))
            {
                yield return run;
                run = new List<ChatLogEntry>();
            }
            run.Add(e);
            lastSender = s;
            lastType = e.Type;
        }
        if (run.Count > 0) yield return run;
    }

    private static IEnumerable<Embed> BuildEmbedsForRun(List<ChatLogEntry> run)
    {
        var sender = ResolveSenderName(run[0]);
        var color = SenderColor(sender);
        var iconUrl = string.IsNullOrEmpty(sender) ? null : $"https://api.dicebear.com/7.x/identicon/png?seed={Uri.EscapeDataString(sender)}";

        var sb = new StringBuilder();
        foreach (var e in run)
        {
            var line = FormatLineNoSender(e);
            if (sb.Length + line.Length + 1 > DiscordEmbedDescriptionLimit)
            {
                yield return BuildEmbed(sender, iconUrl, color, sb.ToString());
                sb.Clear();
            }
            sb.AppendLine(line);
        }
        if (sb.Length > 0)
            yield return BuildEmbed(sender, iconUrl, color, sb.ToString());
    }

    private static Embed BuildEmbed(string sender, string? iconUrl, Color color, string description)
    {
        var builder = new EmbedBuilder()
            .WithColor(color)
            .WithDescription(description);
        if (!string.IsNullOrEmpty(sender))
            builder.WithAuthor(sender, iconUrl);
        return builder.Build();
    }

    private static Color SenderColor(string sender)
    {
        if (string.IsNullOrEmpty(sender)) return new Color(0x5865F2);
        unchecked
        {
            uint hash = 2166136261u;
            foreach (var ch in sender)
            {
                hash ^= ch;
                hash *= 16777619u;
            }
            return new Color(hash & 0xFFFFFFu);
        }
    }

    private static string ResolveSenderName(ChatLogEntry e) =>
        e.Type == XivChatType.TellOutgoing && !string.IsNullOrEmpty(e.LocalPlayerName)
            ? e.LocalPlayerName
            : e.Sender ?? "";

    private static string FormatLineNoSender(ChatLogEntry e)
    {
        var ts = e.Timestamp.ToString("HH:mm:ss");
        return e.Type switch
        {
            XivChatType.TellOutgoing => $"`[{ts}]` >> {e.Message}",
            XivChatType.TellIncoming => $"`[{ts}]` {e.Message}",
            _                        => $"`[{ts}] [{e.TypeLabel}]` {e.Message}",
        };
    }

    private async Task SendAsPlainAsync(IMessageChannel dest, IReadOnlyList<ChatLogEntry> entries, CancellationToken ct)
    {
        foreach (var chunk in DiscordWebhookSender.FormatBatch(entries))
            await dest.SendMessageAsync(chunk, allowedMentions: AllowedMentions.None).ConfigureAwait(false);
    }

    public void Dispose()
    {
        try { client?.LogoutAsync().Wait(TimeSpan.FromSeconds(2)); } catch { }
        try { client?.StopAsync().Wait(TimeSpan.FromSeconds(2)); } catch { }
        client?.Dispose();
        startLock.Dispose();
        foreach (var s in threadLocks.Values) s.Dispose();
    }
}
