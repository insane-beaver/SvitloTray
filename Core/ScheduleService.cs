using System.IO;
using System.Text.Json;

namespace SvitloTray.Core;

public sealed class AppSettings
{
    public string Region { get; set; } = Regions.Default;
    public string Group { get; set; } = "GPV1.1";
    public int WarnMinutes { get; set; } = 15;

    public static string Dir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SvitloTray");

    static string FilePath => Path.Combine(Dir, "settings.json");

    public static bool Exists => File.Exists(FilePath);

    public static string CachePath(string region) => Path.Combine(Dir, $"cache-{region}.json");

    public static AppSettings Load()
    {
        try { return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new(); }
        catch { return new(); }
    }

    public void Save()
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}

// Call everything from the UI thread: awaits resume there, so events fire on it too
public sealed class ScheduleService(AppSettings settings)
{
    public AppSettings Settings { get; } = settings;
    public ScheduleSnapshot? Snapshot { get; private set; }
    public string? LastError { get; private set; }
    public DateTime? LastFetch { get; private set; }

    public event Action? Updated;
    public event Action? SettingsChanged;
    public event Action<string>? ScheduleChanged;

    public void LoadCache()
    {
        try
        {
            var path = AppSettings.CachePath(Settings.Region);
            if (File.Exists(path)) Snapshot = ScheduleSource.Parse(Settings.Region, File.ReadAllText(path));
        }
        catch { Snapshot = null; }
    }

    public async Task RefreshAsync()
    {
        var region = Settings.Region;
        try
        {
            var raw = await ScheduleSource.FetchAsync(region);
            var fresh = ScheduleSource.Parse(region, raw);
            if (region != Settings.Region) return;

            var old = Snapshot;
            Snapshot = fresh;
            LastError = null;
            LastFetch = KyivTime.Now;

            Directory.CreateDirectory(AppSettings.Dir);
            File.WriteAllText(AppSettings.CachePath(region), raw);

            if (old is not null)
            {
                var change = ScheduleLogic.DescribeChange(old, fresh, Settings.Group, DateOnly.FromDateTime(KyivTime.Now));
                if (change is not null) ScheduleChanged?.Invoke(change);
            }
        }
        catch (Exception ex)
        {
            if (region == Settings.Region) LastError = ex.Message;
        }
        Updated?.Invoke();
    }

    public async Task SetRegionAsync(string region)
    {
        if (region == Settings.Region) return;
        Settings.Region = region;
        Settings.Save();
        Snapshot = null;
        LastError = null;
        LoadCache();
        SettingsChanged?.Invoke();
        Updated?.Invoke();
        await RefreshAsync();
    }

    public void SetGroup(string group)
    {
        if (group == Settings.Group) return;
        Settings.Group = group;
        Settings.Save();
        SettingsChanged?.Invoke();
        Updated?.Invoke();
    }

    public void SetWarnMinutes(int minutes)
    {
        if (minutes == Settings.WarnMinutes) return;
        Settings.WarnMinutes = minutes;
        Settings.Save();
        SettingsChanged?.Invoke();
    }
}
