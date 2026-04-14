using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EasyChatlog.Models;

namespace EasyChatlog.Services;

public interface IDiscordSender : IDisposable
{
    Task SendBatchAsync(IReadOnlyList<ChatLogEntry> entries, CancellationToken ct);
    Task SendRawAsync(string content, CancellationToken ct);
}
