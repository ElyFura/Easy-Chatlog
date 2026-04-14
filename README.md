# Easy Chatlog

A Dalamud plugin for Final Fantasy XIV that captures your in-game chat, exports it
(`.txt` / `.json` / `.html` / `.md`) and optionally forwards it live to a Discord
channel via webhook or bot.

## Installation (custom repo)

1. In-game: type `/xlsettings` → **Experimental** → **Custom Plugin Repositories**.
2. Paste this URL and click **+**, then **Save**:

   ```
   https://raw.githubusercontent.com/ElyFura/easy-chatlog/main/repo.json
   ```

3. Open `/xlplugins` → tab **All Plugins** → search for **Easy Chatlog** → Install.

## Commands

- `/easychatlog` (or `/ecl`) — open the main window
- `/easychatlog config` — open settings
- `/easychatlog export <txt|json|html|md>` — quick export to disk
- `/easychatlog discord on|off` — toggle live Discord forwarding

## Discord setup

**Webhook (recommended):** Discord channel → *Edit channel* → *Integrations* → *Webhooks*
→ *New webhook* → *Copy Webhook URL* → paste into the plugin's settings.

**Bot:** create a bot in the [Discord Developer Portal](https://discord.com/developers/applications),
invite it to your server with `Send Messages` permission, paste the token and the channel
ID into the plugin's settings.

## Building from source

Requires the .NET 10 SDK and an installed Dalamud.

```
dotnet build EasyChatlog/EasyChatlog.csproj -c Release
```

The release ZIP is produced at `EasyChatlog/bin/Release/EasyChatlog/latest.zip`.
