using System.Collections.Generic;
using Dalamud.Game.Text;

namespace EasyChatlog.Services;

public static class ChannelFilter
{
    /// <summary>
    /// Default-on channels: public chat, party/alliance, FC, all linkshells and CWLS.
    /// Tells stay opt-in (handled separately via Configuration.IncludeTells).
    /// </summary>
    public static Dictionary<XivChatType, bool> DefaultMap()
    {
        return new Dictionary<XivChatType, bool>
        {
            // Public
            { XivChatType.Say, true },
            { XivChatType.Shout, true },
            { XivChatType.Yell, true },

            // Party / Alliance
            { XivChatType.Party, true },
            { XivChatType.CrossParty, true },
            { XivChatType.Alliance, true },

            // Free Company / Novice Network / PvPTeam
            { XivChatType.FreeCompany, true },
            { XivChatType.NoviceNetwork, false },
            { XivChatType.PvPTeam, true },

            // LinkShells
            { XivChatType.Ls1, true }, { XivChatType.Ls2, true }, { XivChatType.Ls3, true }, { XivChatType.Ls4, true },
            { XivChatType.Ls5, true }, { XivChatType.Ls6, true }, { XivChatType.Ls7, true }, { XivChatType.Ls8, true },

            // CrossWorld LinkShells
            { XivChatType.CrossLinkShell1, true }, { XivChatType.CrossLinkShell2, true },
            { XivChatType.CrossLinkShell3, true }, { XivChatType.CrossLinkShell4, true },
            { XivChatType.CrossLinkShell5, true }, { XivChatType.CrossLinkShell6, true },
            { XivChatType.CrossLinkShell7, true }, { XivChatType.CrossLinkShell8, true },

            // Tells (off by default; gated by IncludeTells additionally)
            { XivChatType.TellIncoming, false },
            { XivChatType.TellOutgoing, false },

            // Misc / system
            { XivChatType.Echo, false },
            { XivChatType.SystemMessage, false },
        };
    }

    /// <summary>
    /// UI grouping for the config window.
    /// </summary>
    public static readonly (string Group, XivChatType[] Types)[] Groups =
    {
        ("Public",     new[] { XivChatType.Say, XivChatType.Shout, XivChatType.Yell }),
        ("Group",      new[] { XivChatType.Party, XivChatType.CrossParty, XivChatType.Alliance, XivChatType.PvPTeam }),
        ("Free Company", new[] { XivChatType.FreeCompany, XivChatType.NoviceNetwork }),
        ("LinkShells", new[]
        {
            XivChatType.Ls1, XivChatType.Ls2, XivChatType.Ls3, XivChatType.Ls4,
            XivChatType.Ls5, XivChatType.Ls6, XivChatType.Ls7, XivChatType.Ls8,
        }),
        ("CWLS",       new[]
        {
            XivChatType.CrossLinkShell1, XivChatType.CrossLinkShell2, XivChatType.CrossLinkShell3, XivChatType.CrossLinkShell4,
            XivChatType.CrossLinkShell5, XivChatType.CrossLinkShell6, XivChatType.CrossLinkShell7, XivChatType.CrossLinkShell8,
        }),
        ("Tells",      new[] { XivChatType.TellIncoming, XivChatType.TellOutgoing }),
        ("System",     new[] { XivChatType.Echo, XivChatType.SystemMessage }),
    };
}
