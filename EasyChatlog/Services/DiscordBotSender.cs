using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using Discord;
using Discord.WebSocket;
using EasyChatlog.Models;

namespace EasyChatlog.Services;

public sealed class DiscordBotSender : IDiscordSender
{
    private readonly CharacterConfig config;
    private readonly IPluginLog log;
    private readonly SemaphoreSlim startLock = new(1, 1);

    private DiscordSocketClient? client;
    private bool started;

    public DiscordBotSender(CharacterConfig config, IPluginLog log)
    {
        this.config = config;
        this.log = log;
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
        foreach (var chunk in DiscordWebhookSender.FormatBatch(entries))
        {
            await SendRawAsync(chunk, ct).ConfigureAwait(false);
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

        // Wait briefly for the gateway to be ready so GetChannel resolves.
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (client.ConnectionState != ConnectionState.Connected && DateTime.UtcNow < deadline)
        {
            await Task.Delay(200, ct).ConfigureAwait(false);
        }

        if (client.GetChannel(config.BotChannelId) is not IMessageChannel ch)
        {
            log.Warning("Discord channel {Id} not visible to bot.", config.BotChannelId);
            return;
        }

        await ch.SendMessageAsync(content, allowedMentions: AllowedMentions.None).ConfigureAwait(false);
    }

    public void Dispose()
    {
        try { client?.LogoutAsync().Wait(TimeSpan.FromSeconds(2)); } catch { }
        try { client?.StopAsync().Wait(TimeSpan.FromSeconds(2)); } catch { }
        client?.Dispose();
        startLock.Dispose();
    }
}
