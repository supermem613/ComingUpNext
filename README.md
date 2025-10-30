# ComingUpNext Tray

A lightweight Windows 11 tray application that displays your next upcoming meeting from a public calendar ICS (.ics) feed.

## Features
- Tray icon with dynamic tooltip showing: `Next: <Title> (In X min|In Y h|Mon HH:mm)`
- Auto balloon notification when a meeting is within 15 minutes (once per meeting)
- Context menu: Open Meeting, Refresh, Set Calendar URL, Exit
- Stores configuration in `%APPDATA%/ComingUpNext/config.json`
- Lightweight ICS parsing (skips malformed events)

## ICS Format Expectations
Provide a publicly accessible `.ics` URL (can be from Outlook, Google Calendar, etc.). The app reads `VEVENT` blocks and uses:

- `SUMMARY` → Meeting title
- `DTSTART` → Start time (all-day events treated as midnight local)
- `DTEND` → End time (if missing/invalid defaults to +1 hour)
- `URL` → Meeting link (if present)
- `DESCRIPTION` → Fallback scan for first `http`/`https` link if `URL` absent (helpful for Teams links embedded in description)

Example snippet:
```ics
BEGIN:VEVENT
SUMMARY:Daily Standup
DTSTART:20251030T090000Z
DTEND:20251030T091500Z
URL:https://teams.microsoft.com/l/meetup-join/...
END:VEVENT
```

Timezone handling: `Z` (UTC) times converted to local; floating times (no TZ) treated as local. Date-only values (`YYYYMMDD`) treated as all-day.

## Build & Run
Requires .NET 9 SDK.

```powershell
# Build
dotnet build

# Run (from project directory)
cd src/ComingUpNextTray
dotnet run
```

On first run, use the tray icon context menu "Set Calendar URL" to paste the ICS URL.

## Packaging
Produce a single-file self-contained executable:

```powershell
dotnet publish src/ComingUpNextTray/ComingUpNextTray.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeAllContentForSelfExtract=true -o publish
```
The output folder `publish` will contain the EXE.

## Auto Start (Optional)
Create a shortcut to the published EXE in:
```text
%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup
```

## Future Improvements
- Add Windows Toast notifications using Windows App SDK for richer UX
- Optional filtering (e.g. ignore all-day events or past events spanning multiple days)
- Unit tests project (ICS parsing, time formatting)
- Auto-update mechanism

## License
See `LICENSE`.
