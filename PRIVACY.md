# Radiata privacy

Radiata runs on your PC and keeps your data there. There is no analytics, no advertising, no
account, and no persistent identifier of any kind: nothing in the app tags you or your machine. The
only things that ever reach the project's own server are the two below.

**Update checks.** About a minute after it starts, and then once a day, Radiata asks
`getradiata.app/update` whether a newer version exists. The request carries one piece of
information: the version of Radiata you're running. Like any web request, the server sees your IP
address in its standard access logs. You can
turn the automatic checks off with the **Automatic** checkbox in Settings ▸ Advanced, and then
Radiata makes no update request unless you press **Check for Updates**. Nothing installs without your
approval: when you choose to update, the installer is downloaded over HTTPS from this project's GitHub
releases (so GitHub sees that request) and is checked against the SHA-256 in the update feed before it
runs.

**Crash reports.** If Radiata crashes, it writes a plain-text report to
`%APPDATA%\Radiata\pending-crash.txt` on your PC. On the next start it offers to send that report, but
only if you say yes, each time, and only after showing you the full text of exactly what would be
sent. The report holds the time, the Radiata and Windows versions, a one-line summary of the app's own
state (such as whether input was isolated or a wheel was open), and the error text with its stack
trace. Your user profile folder is replaced with `~` wherever it appears, but nothing else is
removed: the error text can still contain other file paths (for example a game's install folder) or
application or game names, exactly as written. Reports contain no file contents and no persistent
identifier of your PC or account; the review screen shows you everything that would be sent. Declining sends nothing and deletes the report. If you tick "Remember this choice" when
you send, later reports are sent without asking, with the same text, until you set
`crashAutoSend` back to `false` in `config.json` (see [CONFIG.md](CONFIG.md)); ticking it when you
decline turns the offer off (`crashPromptEnabled`). A report is written to your PC either way; it
only leaves through one of those two paths. Reports are stored as plain text on the project's web host, and the host's access logs
record your IP address like any web request.

**Diagnostic logs.** **Settings ▸ Advanced ▸ Troubleshooting ▸ Email Log to Developer…** saves a ZIP of
Radiata's local logs to your Desktop and opens an addressed email draft. Radiata never sends the ZIP
itself; you attach and send it, so review it first. Logs can include device identifiers, account names
in file paths, and application or game names.

**Other services.** Game Grid cover art is fetched from Steam's public CDN and, by default, from
SteamGridDB's CDN: about 325 curated art addresses are built into Radiata, and a request for one is
made when that game's art is shown. If you add your own SteamGridDB API key, Radiata can also search
SteamGridDB's API for art. The Discord integration uses your own Discord application credentials and
talks to the Discord app on your PC and to Discord's API; OBS control talks to OBS on your own PC.
Those requests go to those services, not to the Radiata project, which never sees them, and each
service's own privacy policy applies.

**Stored secrets.** The Discord credentials and tokens, the OBS WebSocket password and your
SteamGridDB API key are stored in your Radiata settings under `%APPDATA%\Radiata`, encrypted with
Windows DPAPI for your Windows account. They are never sent to the Radiata project.

Beyond these, Radiata contacts nothing else on its own. Nothing the project receives is sold,
shared, or used for anything beyond fixing bugs and serving updates.
