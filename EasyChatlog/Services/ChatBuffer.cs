using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using EasyChatlog.Models;

namespace EasyChatlog.Services;

/// <summary>
/// Lock-free producer side (Channel.Writer) so the game thread never blocks.
/// A single background task drains the channel, holds an in-memory ring of recent
/// entries (for the export window), and flushes batches to Discord on size/time.
/// </summary>
public sealed class ChatBuffer : IDisposable
{
    private readonly Channel<ChatLogEntry> channel = Channel.CreateUnbounded<ChatLogEntry>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    private readonly Func<CharacterConfig> getConfig;
    private readonly Func<IDiscordSender?> getDiscord;
    private readonly IPluginLog log;
    private readonly CancellationTokenSource cts = new();
    private readonly Task pumpTask;

    private readonly LinkedList<ChatLogEntry> history = new();
    private readonly object historyLock = new();

    /// <summary>
    /// Sender names selected in the UI. When non-empty, live-forward only sends
    /// messages from these senders. Thread-safe via lock on the set itself.
    /// </summary>
    public HashSet<string> SelectedSenders { get; } = new(StringComparer.OrdinalIgnoreCase);

    public ChatBuffer(Func<CharacterConfig> getConfig, Func<IDiscordSender?> getDiscord, IPluginLog log)
    {
        this.getConfig = getConfig;
        this.getDiscord = getDiscord;
        this.log = log;
        this.pumpTask = Task.Run(PumpAsync);
    }

    public bool TryEnqueue(ChatLogEntry entry) => channel.Writer.TryWrite(entry);

    public IReadOnlyList<ChatLogEntry> SnapshotHistory()
    {
        lock (historyLock)
        {
            return history.ToList();
        }
    }

    public void ClearHistory()
    {
        lock (historyLock)
        {
            history.Clear();
        }
    }

    private async Task PumpAsync()
    {
        var pending = new List<ChatLogEntry>(64);
        var lastFlush = DateTime.UtcNow;

        try
        {
            var reader = channel.Reader;
            while (!cts.IsCancellationRequested)
            {
                // Wait up to 1s for the next message; lets us flush on time even when idle.
                using var waitCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
                waitCts.CancelAfter(TimeSpan.FromSeconds(1));

                ChatLogEntry? entry = null;
                try
                {
                    if (await reader.WaitToReadAsync(waitCts.Token).ConfigureAwait(false)
                        && reader.TryRead(out var read))
                    {
                        entry = read;
                    }
                }
                catch (OperationCanceledException) when (!cts.IsCancellationRequested)
                {
                    // tick — fall through to time-based flush check
                }

                if (entry != null)
                {
                    pending.Add(entry);
                    AddToHistory(entry);

                    // Drain any further immediately-available entries
                    while (reader.TryRead(out var more))
                    {
                        pending.Add(more);
                        AddToHistory(more);
                    }
                }

                var cfg = getConfig();
                var sizeFlush = pending.Count >= Math.Max(1, cfg.FlushAfterMessages);
                var timeFlush = pending.Count > 0
                                && (DateTime.UtcNow - lastFlush).TotalSeconds >= Math.Max(1, cfg.FlushAfterSeconds);

                if (sizeFlush || timeFlush)
                {
                    var batch = pending.ToArray();
                    pending.Clear();
                    lastFlush = DateTime.UtcNow;

                    var discord = getDiscord();
                    if (cfg.DiscordEnabled && discord != null)
                    {
                        try
                        {
                            // Apply sender filter: if senders are selected, only forward their messages.
                            ChatLogEntry[] toSend;
                            lock (SelectedSenders)
                            {
                                toSend = SelectedSenders.Count == 0
                                    ? batch
                                    : batch.Where(e => SelectedSenders.Contains(e.Sender)).ToArray();
                            }

                            if (toSend.Length > 0)
                                await discord.SendBatchAsync(toSend, cts.Token).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            log.Error(ex, "Discord send failed for batch of {Count}", batch.Length);
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException) { /* shutting down */ }
        catch (Exception ex)
        {
            log.Error(ex, "ChatBuffer pump crashed");
        }
    }

    private void AddToHistory(ChatLogEntry entry)
    {
        var max = Math.Max(100, getConfig().InMemoryHistorySize);
        lock (historyLock)
        {
            history.AddLast(entry);
            while (history.Count > max)
                history.RemoveFirst();
        }
    }

    public void Dispose()
    {
        try
        {
            channel.Writer.TryComplete();
            cts.Cancel();
            pumpTask.Wait(TimeSpan.FromSeconds(2));
        }
        catch { /* swallow on dispose */ }
        cts.Dispose();
    }
}
