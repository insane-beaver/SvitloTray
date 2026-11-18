# SvitloTray

Windows tray app that shows the hourly power outage schedule (ГПВ) for your queue in Ukraine.

- Tray icon color: green = power on, red = outage, amber = possible outage, gray = no schedule. In the last hour before a change it shows the minutes left
- Left click: compact popup with the current status and today's timeline. Double click: full window
- Full window: 24h timeline for your queue (today and tomorrow when published), all queues on demand, daily totals
- Windows notifications: N minutes before an outage, when power should be back, when the schedule changes
- Light and dark theme follow Windows (Fluent)
- Regions: Khmelnytskyi (default), Kyiv and Dnipro (DTEK via Yasno), Cherkasy, Chernihiv, Ivano-Frankivsk, Kharkiv, Lviv, Poltava, Rivne, Ternopil, Zakarpattia, Zaporizhzhia, Zhytomyr

Find your queue on your oblenergo site (Khmelnytskyi: https://hoe.com.ua/shutdown/new).

## Download

Grab `SvitloTray.exe` from [Releases](../../releases). It's a single self-contained file, no .NET install needed.

## Build

Requires .NET 10 SDK.

```
dotnet run
```

Or open `SvitloTray.csproj` in Visual Studio and press F5.

Release build: Visual Studio > Build > Publish Selection > Publish, or run `build-release.cmd`. Output: `publish\SvitloTray.exe`.

## Data

- [yaroslav2901/OE_OUTAGE_DATA](https://github.com/yaroslav2901/OE_OUTAGE_DATA): parses official oblenergo sites every 5 minutes
- [Yasno](https://yasno.ua) public API for Kyiv and Dnipro

Unscheduled emergency outages are not included.

Settings and cache: `%AppData%\SvitloTray`.
