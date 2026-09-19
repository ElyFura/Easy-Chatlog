using System.Collections.Generic;

namespace EasyChatlog.Localization;

/// <summary>
/// Every user-facing string in one place. The field initialisers are the English
/// text; <see cref="German"/> overrides each one. Log output stays English on purpose.
/// </summary>
public sealed class Strings
{
    // --- Window titles / general ---------------------------------------------------------
    public string ConfigTitle          = "Easy Chatlog — Settings";
    public string ConfigTitleFmt       = "Easy Chatlog — Settings ({0})";
    public string LanguageLabel        = "Language / Sprache";
    public string LanguageAuto         = "Automatic (Dalamud)";
    public string LanguageEnglish      = "English";
    public string LanguageGerman       = "Deutsch";

    // --- Discord section -----------------------------------------------------------------
    public string HeaderDiscord        = "Discord";
    public string ForwardToDiscord     = "Forward chat to Discord";
    public string ModeWebhook          = "Webhook";
    public string ModeBot              = "Bot";
    public string WebhookUrl           = "Webhook URL";
    public string DisplayName          = "Display name";
    public string BotToken             = "Bot Token";
    public string ChannelId            = "Channel ID";
    public string TestSend             = "Test send";

    // --- Rendering section ---------------------------------------------------------------
    public string HeaderRendering      = "Rendering";
    public string PerSenderIdentity    = "Show each FFXIV speaker as its own Discord identity";
    public string PerSenderTooltip     =
        "Webhook: each message uses the speaker's name/avatar — Discord auto-groups runs.\n"
      + "Bot: one embed per speaker run with a unique color stripe.";
    public string UseIdenticon         = "Use identicon avatars (dicebear.com)";

    // --- Buffer section ------------------------------------------------------------------
    public string HeaderBuffer         = "Buffer / Flush";
    public string FlushAfterMessages   = "Flush after N messages";
    public string FlushAfterSeconds    = "Flush after N seconds";
    public string InMemoryHistory      = "In-memory history (entries)";

    // --- Channels section ----------------------------------------------------------------
    public string HeaderChannels       = "Channels";
    public string IncludeTells         = "Include Tells (privacy-sensitive!)";
    public Dictionary<string, string> ChannelGroups = new();

    // --- Export section ------------------------------------------------------------------
    public string HeaderExport         = "Export";
    public string ExportDirectory      = "Export directory";
    public string DefaultFormat        = "Default format";
    public string FormatTxt            = "Plain Text (.txt)";
    public string FormatJson           = "JSON (.json)";
    public string FormatHtml           = "HTML (.html)";
    public string FormatMarkdown       = "Markdown (.md)";

    // --- Threading section ---------------------------------------------------------------
    public string HeaderThreads        = "Threads";
    public string HelpButton           = "? Help";
    public string ThreadHelpHint       = "Routing keys, template placeholders, examples";
    public string ThreadingMode        = "Threading mode";
    public string ThreadingOff         = "Off";
    public string ThreadingPerTell     = "Per Tell partner";
    public string ThreadingPerChannel  = "Per channel type";
    public string ThreadingPerSender   = "Per sender";
    public string AutoArchive          = "Auto-archive";
    public string ArchiveOneHour       = "1 hour";
    public string ArchiveOneDay        = "1 day";
    public string ArchiveThreeDays     = "3 days";
    public string ArchiveOneWeek       = "1 week";
    public string ThreadNameTemplate   = "Thread name template";
    public string ThreadTemplateTip    = "Placeholders: {key}, {type}, {sender}";
    public string BotAutoCreates       = "Bot will auto-create threads in the configured channel.";
    public string BotRequiredPerms     = "Required permissions: Create Public Threads, Send Messages in Threads.";
    public string WebhookNoThreads1    = "Webhooks cannot create threads. Enter a pre-existing Thread ID per routing key,";
    public string WebhookNoThreads2    = "or switch to Bot mode for automatic thread creation.";
    public string NoThreadsYet         = "No threads created yet.";
    public string ColumnKey            = "Key";
    public string ColumnThreadId       = "Thread ID";
    public string Forget               = "Forget";
    public string Remove               = "Remove";
    public string Add                  = "Add";
    public string HintKey              = "key (e.g. tell:Foo@Bar)";
    public string HintThreadId         = "thread id";

    // --- Threading help popup ------------------------------------------------------------
    public string HelpRoutingKeys      = "Thread routing keys";
    public string HelpRoutingIntro     =
        "Every chat entry is mapped to a logical \"key\" based on the threading mode. The key decides "
      + "which Discord thread the message lands in. Webhook mode looks the key up in the Overrides table "
      + "below; Bot mode auto-creates the thread and remembers the id.";
    public string HelpBulletOff        = "Off:                no threading, everything goes to the parent channel.";
    public string HelpBulletTell       = "Per Tell partner:   one thread per Tell partner. Key: tell:<Name@World>";
    public string HelpBulletChannel    = "Per channel type:   one thread per chat type.   Key: channel:<XivChatType>";
    public string HelpBulletSender     = "Per sender:         one thread per sender.      Key: sender:<Name@World>";
    public string HelpExamples         = "Examples";
    public string HelpExSay            = "channel:Say        — all /say messages";
    public string HelpExParty          = "channel:Party      — all party chat";
    public string HelpExLs1            = "channel:Ls1        — linkshell 1";
    public string HelpExTell           = "tell:Missi Ashcroft@Odin — both directions of tells with that character";
    public string HelpPlaceholders     = "Thread name template — placeholders";
    public string HelpPhKey            = "{key}    — full routing key, e.g. \"Tell - Missi Ashcroft@Odin\"";
    public string HelpPhType           = "{type}   — \"Tell\" / channel type / \"Chat\"";
    public string HelpPhSender         = "{sender} — partner / sender (empty for channel mode)";
    public string HelpWebhookSetup     = "Webhook setup";
    public string HelpWebhookBody      =
        "Webhooks cannot create threads — create the thread in Discord first, then copy its ID via "
      + "Right-click > Copy Thread ID (with Developer Mode on). The webhook URL itself must point to the "
      + "PARENT text channel; the plugin appends ?thread_id=… automatically.";
    public string HelpQuickFill        = "Quick-fill key for the override form below:";
    public string Close                = "Close";

    // --- Profile bar ---------------------------------------------------------------------
    public string EditingProfile       = "Editing profile:";
    public string IsDefaultMarker      = "(default)";
    public string New                  = "New";
    public string Rename               = "Rename";
    public string Duplicate            = "Duplicate";
    public string Delete               = "Delete";
    public string SetAsDefault         = "Set as default";
    public string DefaultFallbackHint  = "(current default fallback for unassigned characters)";
    public string ActiveProfileForFmt  = "Profile active for {0}:";
    public string EditThisOne          = "Edit this one";
    public string CopySuffix           = " (copy)";
    public string NewProfileName       = "New profile";

    // --- Character assignments -----------------------------------------------------------
    public string HeaderAssignments    = "Character assignments";
    public string NoCharactersYet      = "No characters recorded yet. Log in on a character to populate this list.";
    public string Clear                = "Clear";
    public string CharacterFallbackFmt = "Character {0}";

    // --- Popups --------------------------------------------------------------------------
    public string NewProfilePrompt     = "New profile name:";
    public string Create               = "Create";
    public string Cancel               = "Cancel";
    public string RenameProfilePrompt  = "Rename profile:";
    public string Save                 = "Save";
    public string DeleteProfileFmt     = "Delete profile \"{0}\"?\nCharacters using it will fall back to the default profile.";
    public string ProfileNotFound      = "Profile not found.";

    // --- Export window -------------------------------------------------------------------
    public string OpenSettings         = "Open Settings";
    public string ClearBuffer          = "Clear buffer";
    public string DiscordLiveForward   = "Discord live-forward";
    public string FilterBySenders      = "Filter by sender(s):";
    public string FilterAll            = "(all — live-forward sends everything)";
    public string FilterSelectedFmt    = "({0} selected — live-forward + export use this filter)";
    public string ClearFilter          = "Clear filter";
    public string SearchSender         = "Search sender...";
    public string ExportTxt            = "Export TXT";
    public string ExportJson           = "Export JSON";
    public string ExportHtml           = "Export HTML";
    public string ExportMd             = "Export MD";
    public string SendFiltered         = "Send filtered to Discord";
    public string ShowingEntriesFmt    = "Showing {0} / {1} entries";
    public string ColumnTime           = "Time";
    public string ColumnChannel        = "Channel";
    public string ColumnSender         = "Sender";
    public string ColumnMessage        = "Message";

    // --- Notifications / commands --------------------------------------------------------
    public string NothingToExport      = "Nothing to export.";
    public string ExportedFmt          = "Exported {0} entries → {1}";
    public string ExportFailedFmt      = "Export failed: {0}";
    public string NothingToSend        = "Nothing to send.";
    public string DiscordNotConfigured = "Discord is not configured.";
    public string SentToDiscordFmt     = "Sent {0} entries to Discord.";
    public string DiscordSendFailedFmt = "Discord send failed: {0}";
    public string SenderNotConfigured  = "Discord sender not configured.";
    public string DiscordTestBodyFmt   = "Easy Chatlog test from {0} ({1})";
    public string DiscordTestSent      = "Discord test sent.";
    public string DiscordTestFailedFmt = "Discord test failed: {0}";
    public string LiveForwardOnOffFmt  = "Discord live-forward {0}.";
    public string Enabled              = "ENABLED";
    public string Disabled             = "DISABLED";
    public string UnknownSubcommandFmt = "Unknown subcommand: {0}";
    public string CommandHelp          = "Open Easy Chatlog. Subcommands: config | export <txt|json|html|md> | discord on|off";
    public string CommandHelpAlias     = "Alias for /easychatlog";

    /// <summary>Localized name for a <see cref="Services.ChannelFilter.Groups"/> key.</summary>
    public string ChannelGroup(string key) => ChannelGroups.GetValueOrDefault(key, key);

    public static readonly Strings English = new()
    {
        ChannelGroups = new Dictionary<string, string>
        {
            ["Public"]       = "Public",
            ["Group"]        = "Group",
            ["Free Company"] = "Free Company",
            ["LinkShells"]   = "LinkShells",
            ["CWLS"]         = "CWLS",
            ["Tells"]        = "Tells",
            ["System"]       = "System",
        },
    };

    public static readonly Strings German = new()
    {
        ConfigTitle          = "Easy Chatlog — Einstellungen",
        ConfigTitleFmt       = "Easy Chatlog — Einstellungen ({0})",
        LanguageLabel        = "Sprache / Language",
        LanguageAuto         = "Automatisch (Dalamud)",

        HeaderDiscord        = "Discord",
        ForwardToDiscord     = "Chat an Discord weiterleiten",
        WebhookUrl           = "Webhook-URL",
        DisplayName          = "Anzeigename",
        BotToken             = "Bot-Token",
        ChannelId            = "Kanal-ID",
        TestSend             = "Testnachricht senden",

        HeaderRendering      = "Darstellung",
        PerSenderIdentity    = "Jeden FFXIV-Sprecher als eigene Discord-Identität anzeigen",
        PerSenderTooltip     =
            "Webhook: Jede Nachricht nutzt Name/Avatar des Sprechers — Discord gruppiert Folgenachrichten automatisch.\n"
          + "Bot: Ein Embed pro Sprecher-Block mit eigener Farbmarkierung.",
        UseIdenticon         = "Identicon-Avatare verwenden (dicebear.com)",

        HeaderBuffer         = "Puffer / Senden",
        FlushAfterMessages   = "Senden nach N Nachrichten",
        FlushAfterSeconds    = "Senden nach N Sekunden",
        InMemoryHistory      = "Verlauf im Speicher (Einträge)",

        HeaderChannels       = "Kanäle",
        IncludeTells         = "Tells einbeziehen (datenschutzrelevant!)",
        ChannelGroups        = new Dictionary<string, string>
        {
            ["Public"]       = "Öffentlich",
            ["Group"]        = "Gruppe",
            ["Free Company"] = "Freie Gesellschaft",
            ["LinkShells"]   = "Linkshells",
            ["CWLS"]         = "Welten-Linkshells",
            ["Tells"]        = "Tells",
            ["System"]       = "System",
        },

        HeaderExport         = "Export",
        ExportDirectory      = "Export-Verzeichnis",
        DefaultFormat        = "Standardformat",
        FormatTxt            = "Klartext (.txt)",
        FormatJson           = "JSON (.json)",
        FormatHtml           = "HTML (.html)",
        FormatMarkdown       = "Markdown (.md)",

        HeaderThreads        = "Threads",
        HelpButton           = "? Hilfe",
        ThreadHelpHint       = "Routing-Schlüssel, Platzhalter, Beispiele",
        ThreadingMode        = "Thread-Modus",
        ThreadingOff         = "Aus",
        ThreadingPerTell     = "Pro Tell-Partner",
        ThreadingPerChannel  = "Pro Kanaltyp",
        ThreadingPerSender   = "Pro Absender",
        AutoArchive          = "Auto-Archivierung",
        ArchiveOneHour       = "1 Stunde",
        ArchiveOneDay        = "1 Tag",
        ArchiveThreeDays     = "3 Tage",
        ArchiveOneWeek       = "1 Woche",
        ThreadNameTemplate   = "Vorlage für Thread-Namen",
        ThreadTemplateTip    = "Platzhalter: {key}, {type}, {sender}",
        BotAutoCreates       = "Der Bot erstellt Threads automatisch im konfigurierten Kanal.",
        BotRequiredPerms     = "Benötigte Rechte: Öffentliche Threads erstellen, In Threads schreiben.",
        WebhookNoThreads1    = "Webhooks können keine Threads erstellen. Trage pro Routing-Schlüssel eine vorhandene Thread-ID ein,",
        WebhookNoThreads2    = "oder wechsle in den Bot-Modus für automatische Thread-Erstellung.",
        NoThreadsYet         = "Noch keine Threads erstellt.",
        ColumnKey            = "Schlüssel",
        ColumnThreadId       = "Thread-ID",
        Forget               = "Vergessen",
        Remove               = "Entfernen",
        Add                  = "Hinzufügen",
        HintKey              = "Schlüssel (z. B. tell:Foo@Bar)",
        HintThreadId         = "Thread-ID",

        HelpRoutingKeys      = "Thread-Routing-Schlüssel",
        HelpRoutingIntro     =
            "Jeder Chat-Eintrag wird je nach Thread-Modus auf einen logischen \"Schlüssel\" abgebildet. Der Schlüssel "
          + "bestimmt, in welchem Discord-Thread die Nachricht landet. Im Webhook-Modus wird der Schlüssel in der "
          + "Überschreibungs-Tabelle unten nachgeschlagen; im Bot-Modus wird der Thread automatisch erstellt und die ID gemerkt.",
        HelpBulletOff        = "Aus:              kein Threading, alles geht in den übergeordneten Kanal.",
        HelpBulletTell       = "Pro Tell-Partner: ein Thread je Tell-Partner. Schlüssel: tell:<Name@Welt>",
        HelpBulletChannel    = "Pro Kanaltyp:     ein Thread je Chat-Typ.    Schlüssel: channel:<XivChatType>",
        HelpBulletSender     = "Pro Absender:     ein Thread je Absender.    Schlüssel: sender:<Name@Welt>",
        HelpExamples         = "Beispiele",
        HelpExSay            = "channel:Say        — alle /say-Nachrichten",
        HelpExParty          = "channel:Party      — der gesamte Gruppenchat",
        HelpExLs1            = "channel:Ls1        — Linkshell 1",
        HelpExTell           = "tell:Missi Ashcroft@Odin — Tells in beide Richtungen mit diesem Charakter",
        HelpPlaceholders     = "Vorlage für Thread-Namen — Platzhalter",
        HelpPhKey            = "{key}    — vollständiger Routing-Schlüssel, z. B. \"Tell - Missi Ashcroft@Odin\"",
        HelpPhType           = "{type}   — \"Tell\" / Kanaltyp / \"Chat\"",
        HelpPhSender         = "{sender} — Partner / Absender (leer im Kanal-Modus)",
        HelpWebhookSetup     = "Webhook-Einrichtung",
        HelpWebhookBody      =
            "Webhooks können keine Threads erstellen — lege den Thread zuerst in Discord an und kopiere seine ID per "
          + "Rechtsklick > Thread-ID kopieren (Entwicklermodus muss aktiv sein). Die Webhook-URL selbst muss auf den "
          + "ÜBERGEORDNETEN Textkanal zeigen; das Plugin hängt ?thread_id=… automatisch an.",
        HelpQuickFill        = "Schlüssel schnell in das Formular unten übernehmen:",
        Close                = "Schließen",

        EditingProfile       = "Bearbeitetes Profil:",
        IsDefaultMarker      = "(Standard)",
        New                  = "Neu",
        Rename               = "Umbenennen",
        Duplicate            = "Duplizieren",
        Delete               = "Löschen",
        SetAsDefault         = "Als Standard festlegen",
        DefaultFallbackHint  = "(aktueller Standard für nicht zugewiesene Charaktere)",
        ActiveProfileForFmt  = "Aktives Profil für {0}:",
        EditThisOne          = "Dieses bearbeiten",
        CopySuffix           = " (Kopie)",
        NewProfileName       = "Neues Profil",

        HeaderAssignments    = "Charakter-Zuweisungen",
        NoCharactersYet      = "Noch keine Charaktere erfasst. Logge dich mit einem Charakter ein, um die Liste zu füllen.",
        Clear                = "Zurücksetzen",
        CharacterFallbackFmt = "Charakter {0}",

        NewProfilePrompt     = "Name des neuen Profils:",
        Create               = "Erstellen",
        Cancel               = "Abbrechen",
        RenameProfilePrompt  = "Profil umbenennen:",
        Save                 = "Speichern",
        DeleteProfileFmt     = "Profil \"{0}\" löschen?\nCharaktere, die es nutzen, fallen auf das Standardprofil zurück.",
        ProfileNotFound      = "Profil nicht gefunden.",

        OpenSettings         = "Einstellungen öffnen",
        ClearBuffer          = "Puffer leeren",
        DiscordLiveForward   = "Discord-Liveweiterleitung",
        FilterBySenders      = "Nach Absender filtern:",
        FilterAll            = "(alle — Liveweiterleitung sendet alles)",
        FilterSelectedFmt    = "({0} ausgewählt — Liveweiterleitung + Export nutzen diesen Filter)",
        ClearFilter          = "Filter zurücksetzen",
        SearchSender         = "Absender suchen...",
        ExportTxt            = "TXT exportieren",
        ExportJson           = "JSON exportieren",
        ExportHtml           = "HTML exportieren",
        ExportMd             = "MD exportieren",
        SendFiltered         = "Gefilterte an Discord senden",
        ShowingEntriesFmt    = "Zeige {0} / {1} Einträge",
        ColumnTime           = "Zeit",
        ColumnChannel        = "Kanal",
        ColumnSender         = "Absender",
        ColumnMessage        = "Nachricht",

        NothingToExport      = "Nichts zu exportieren.",
        ExportedFmt          = "{0} Einträge exportiert → {1}",
        ExportFailedFmt      = "Export fehlgeschlagen: {0}",
        NothingToSend        = "Nichts zu senden.",
        DiscordNotConfigured = "Discord ist nicht konfiguriert.",
        SentToDiscordFmt     = "{0} Einträge an Discord gesendet.",
        DiscordSendFailedFmt = "Senden an Discord fehlgeschlagen: {0}",
        SenderNotConfigured  = "Discord-Sender nicht konfiguriert.",
        DiscordTestBodyFmt   = "Easy-Chatlog-Test von {0} ({1})",
        DiscordTestSent      = "Discord-Test gesendet.",
        DiscordTestFailedFmt = "Discord-Test fehlgeschlagen: {0}",
        LiveForwardOnOffFmt  = "Discord-Liveweiterleitung {0}.",
        Enabled              = "AKTIVIERT",
        Disabled             = "DEAKTIVIERT",
        UnknownSubcommandFmt = "Unbekannter Unterbefehl: {0}",
        CommandHelp          = "Easy Chatlog öffnen. Unterbefehle: config | export <txt|json|html|md> | discord on|off",
        CommandHelpAlias     = "Alias für /easychatlog",
    };
}
