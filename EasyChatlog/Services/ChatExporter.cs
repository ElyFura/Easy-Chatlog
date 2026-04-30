using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Dalamud.Game.Text;
using EasyChatlog.Models;

namespace EasyChatlog.Services;

public static class ChatExporter
{
    public static string FileExtension(ExportFormat fmt) => fmt switch
    {
        ExportFormat.Txt      => ".txt",
        ExportFormat.Json     => ".json",
        ExportFormat.Html     => ".html",
        ExportFormat.Markdown => ".md",
        _ => ".txt",
    };

    public static async Task<string> ExportAsync(
        IReadOnlyList<ChatLogEntry> entries, string directory, ExportFormat format)
    {
        Directory.CreateDirectory(directory);
        var fileName = $"chatlog-{DateTime.Now:yyyyMMdd-HHmmss}{FileExtension(format)}";
        var path = Path.Combine(directory, fileName);

        var content = format switch
        {
            ExportFormat.Txt      => RenderTxt(entries),
            ExportFormat.Json     => RenderJson(entries),
            ExportFormat.Html     => RenderHtml(entries),
            ExportFormat.Markdown => RenderMarkdown(entries),
            _ => RenderTxt(entries),
        };

        await File.WriteAllTextAsync(path, content, Encoding.UTF8).ConfigureAwait(false);
        return path;
    }

    private static string RenderTxt(IReadOnlyList<ChatLogEntry> e)
    {
        var sb = new StringBuilder();
        foreach (var x in e)
            sb.AppendLine($"[{x.Timestamp:yyyy-MM-dd HH:mm:ss}] [{x.TypeLabel}] {x.Sender}: {x.Message}");
        return sb.ToString();
    }

    private static string RenderJson(IReadOnlyList<ChatLogEntry> e)
    {
        var data = new List<object>(e.Count);
        foreach (var x in e)
        {
            data.Add(new
            {
                timestamp = x.Timestamp.ToString("o"),
                type = x.TypeLabel,
                sender = x.Sender,
                message = x.Message,
            });
        }
        return JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string RenderMarkdown(IReadOnlyList<ChatLogEntry> e)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Chatlog Export — {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        foreach (var x in e)
        {
            sb.Append('`').Append(x.Timestamp.ToString("HH:mm:ss")).Append("` ");
            sb.Append('*').Append(x.TypeLabel).Append("* ");
            sb.Append("**").Append(x.Sender).Append("**: ");
            sb.AppendLine(x.Message);
        }
        return sb.ToString();
    }

    private static string RenderHtml(IReadOnlyList<ChatLogEntry> e)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!doctype html><html><head><meta charset=\"utf-8\"><title>FFXIV Chatlog</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body{font-family:Consolas,monospace;background:#1e1e1e;color:#ddd;padding:1em;}");
        sb.AppendLine(".row{padding:2px 0;} .ts{color:#888;} .sender{font-weight:bold;}");
        sb.AppendLine(".say{color:#ffffff;} .shout{color:#ffa500;} .yell{color:#ffff00;}");
        sb.AppendLine(".party{color:#76d6ff;} .alliance{color:#ff9248;}");
        sb.AppendLine(".fc{color:#a6e22e;} .ls{color:#c5a8ff;} .cwls{color:#9ad1ff;}");
        sb.AppendLine(".tell{color:#ff9bd2;} .system{color:#888;}");
        sb.AppendLine("</style></head><body>");
        foreach (var x in e)
        {
            var cls = TypeCss(x.Type);
            sb.Append("<div class=\"row ").Append(cls).Append("\">");
            sb.Append("<span class=\"ts\">[").Append(x.Timestamp.ToString("HH:mm:ss")).Append("] [").Append(x.TypeLabel).Append("]</span> ");
            sb.Append("<span class=\"sender\">").Append(HtmlEscape(x.Sender)).Append("</span>: ");
            sb.Append(HtmlEscape(x.Message));
            sb.AppendLine("</div>");
        }
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    private static string TypeCss(XivChatType t) => t switch
    {
        XivChatType.Say => "say",
        XivChatType.Shout => "shout",
        XivChatType.Yell => "yell",
        XivChatType.Party or XivChatType.CrossParty => "party",
        XivChatType.Alliance => "alliance",
        XivChatType.FreeCompany => "fc",
        XivChatType.Ls1 or XivChatType.Ls2 or XivChatType.Ls3 or XivChatType.Ls4
            or XivChatType.Ls5 or XivChatType.Ls6 or XivChatType.Ls7 or XivChatType.Ls8 => "ls",
        XivChatType.CrossLinkShell1 or XivChatType.CrossLinkShell2 or XivChatType.CrossLinkShell3 or XivChatType.CrossLinkShell4
            or XivChatType.CrossLinkShell5 or XivChatType.CrossLinkShell6 or XivChatType.CrossLinkShell7 or XivChatType.CrossLinkShell8 => "cwls",
        XivChatType.TellIncoming or XivChatType.TellOutgoing => "tell",
        _ => "system",
    };

    private static string HtmlEscape(string s)
        => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
