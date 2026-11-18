namespace SvitloTray.Core;

public enum SlotStatus { On, Off, Maybe }

// 48 half-hour slots per group, index 0 = 00:00-00:30 Kyiv time
public sealed record DaySchedule(DateOnly Date, Dictionary<string, SlotStatus[]> Groups);

public sealed record ScheduleSnapshot(DateTimeOffset? LastUpdated, IReadOnlyList<DaySchedule> Days);

public sealed record Slot(DateTime Start, SlotStatus Status)
{
    public DateTime End => Start.AddMinutes(30);
}

public sealed record GroupState(SlotStatus? Current, DateTime? Since, DateTime? Until, DateTime? NextOff);

public sealed record Region(string Id, string Name);

public sealed record StatusView(SlotStatus? Status, string Title, string Info, string Countdown, string Until, double Progress, int? MinutesLeft);

public static class Regions
{
    public const string Default = "Khmelnytskoblenerho";

    public static readonly Region[] All =
    [
        new("Khmelnytskoblenerho", "Хмельницька обл."),
        new("yasno-25-902", "Київ (ДТЕК)"),
        new("yasno-3-301", "Дніпро (ДнЕМ)"),
        new("yasno-3-303", "Дніпро (ЦЕК)"),
        new("Zhytomyroblenergo", "Житомирська обл."),
        new("Zakarpattiaoblenerho", "Закарпатська обл."),
        new("Zaporizhzhiaoblenergo", "Запорізька обл."),
        new("Prykarpattiaoblenerho", "Івано-Франківська обл."),
        new("Lvivoblenerho", "Львівська обл."),
        new("Poltavaoblenergo", "Полтавська обл."),
        new("Rivneoblenergo", "Рівненська обл."),
        new("Ternopiloblenerho", "Тернопільська обл."),
        new("Kharkivoblenerho", "Харківська обл."),
        new("Cherkasyoblenergo", "Черкаська обл."),
        new("Chernihivoblenergo", "Чернігівська обл."),
    ];

    public static string NameOf(string id) => All.FirstOrDefault(r => r.Id == id)?.Name ?? id;

    public static readonly string[] Groups =
        Enumerable.Range(1, 6).SelectMany(q => new[] { $"GPV{q}.1", $"GPV{q}.2" }).ToArray();

    public static string GroupLabel(string id) => id.StartsWith("GPV") ? id[3..] : id;
}

public static class KyivTime
{
    public static readonly TimeZoneInfo Zone = Find();

    public static DateTime Now => TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Zone).DateTime;

    public static DateTime FromUnix(long seconds) =>
        TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeSeconds(seconds), Zone).DateTime;

    public static DateTime FromOffset(DateTimeOffset value) => TimeZoneInfo.ConvertTime(value, Zone).DateTime;

    static TimeZoneInfo Find()
    {
        foreach (var id in new[] { "Europe/Kyiv", "Europe/Kiev", "FLE Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.Local;
    }
}
