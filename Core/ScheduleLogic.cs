namespace SvitloTray.Core;

public static class ScheduleLogic
{
    public static List<Slot> Timeline(ScheduleSnapshot snapshot, string group) =>
        snapshot.Days
            .Where(d => d.Groups.ContainsKey(group))
            .SelectMany(d => d.Groups[group].Select((s, i) =>
                new Slot(d.Date.ToDateTime(TimeOnly.MinValue).AddMinutes(30 * i), s)))
            .ToList();

    public static GroupState GetState(ScheduleSnapshot? snapshot, string group, DateTime now)
    {
        if (snapshot is null) return new(null, null, null, null);

        var tl = Timeline(snapshot, group);
        var i = tl.FindIndex(s => s.Start <= now && now < s.End);
        if (i < 0) return new(null, null, null, null);

        var current = tl[i].Status;
        var s0 = i;
        while (s0 > 0 && tl[s0 - 1].Status == current && tl[s0 - 1].End == tl[s0].Start) s0--;
        var j = i + 1;
        while (j < tl.Count && tl[j].Status == current && tl[j - 1].End == tl[j].Start) j++;
        DateTime? until = j < tl.Count && tl[j - 1].End == tl[j].Start ? tl[j].Start : null;

        DateTime? nextOff = null;
        for (var k = i + 1; k < tl.Count; k++)
        {
            if (tl[k].Status == SlotStatus.Off && tl[k - 1].Status != SlotStatus.Off)
            {
                nextOff = tl[k].Start;
                break;
            }
        }

        return new(current, tl[s0].Start, until, nextOff);
    }

    public static (TimeSpan Off, TimeSpan Maybe) Totals(SlotStatus[] slots) =>
        (TimeSpan.FromMinutes(30 * slots.Count(s => s == SlotStatus.Off)),
         TimeSpan.FromMinutes(30 * slots.Count(s => s == SlotStatus.Maybe)));

    public static (string Title, string Detail) Describe(GroupState state, DateTime now)
    {
        if (state.Current is not { } current)
            return ("Немає графіка на зараз", "Відключень не заплановано або графік ще не опублікували");

        var title = StatusName(current);
        if (state.Until is not { } until)
            return (title, "до кінця опублікованого графіка");

        return (title, $"до {FormatTime(until, now)} · ще {FormatDuration(until - now)}");
    }

    public static StatusView Present(ScheduleSnapshot? snapshot, string group, DateTime now)
    {
        if (snapshot is null)
            return new(null, "Завантажую...", "", "", "", 0, null);

        var state = GetState(snapshot, group, now);
        if (state.Current is not { } current)
            return new(null, "Немає графіка на зараз", "Відключень не заплановано або графік ще не опублікували", "", "", 0, null);

        var info = new List<string>();
        if (state.Since is { } since) info.Add($"з {FormatTime(since, now)}");
        if (current != SlotStatus.Off && state.NextOff is { } off) info.Add($"наступне відключення о {FormatTime(off, now)}");
        else if (current == SlotStatus.On) info.Add("далі в графіку відключень немає");
        var line = string.Join(" · ", info);
        line = line.Length > 0 ? char.ToUpper(line[0]) + line[1..] : "";

        if (state.Until is not { } until)
            return new(current, StatusName(current), line, "", "до кінця опублікованого графіка", 0, null);

        var left = until - now;
        var progress = state.Since is { } s && until > s ? Math.Clamp((now - s) / (until - s), 0, 1) : 0;
        var minutes = (int)Math.Ceiling(left.TotalMinutes);
        return new(current, StatusName(current), line, FormatDuration(left), $"до {FormatTime(until, now)}",
            progress, minutes is > 0 and < 60 ? minutes : null);
    }

    public static string? DescribeChange(ScheduleSnapshot old, ScheduleSnapshot updated, string group, DateOnly today)
    {
        var before = ForGroup(old, group, today);
        var after = ForGroup(updated, group, today);
        var label = Regions.GroupLabel(group);

        var added = after.Keys.Where(d => !before.ContainsKey(d)).OrderBy(d => d).ToList();
        if (added.Count > 0)
            return $"З'явився графік на {added[0]:dd.MM} для підчерги {label}";

        foreach (var (date, slots) in after)
        {
            if (!before[date].SequenceEqual(slots))
                return $"Графік на {date:dd.MM} для підчерги {label} змінився";
        }
        return null;
    }

    public static string StatusName(SlotStatus status) => status switch
    {
        SlotStatus.On => "Світло є",
        SlotStatus.Off => "Світла немає",
        _ => "Можливе відключення",
    };

    public static string FormatTime(DateTime time, DateTime now) =>
        time.Date == now.Date ? time.ToString("HH:mm")
        : time.Date == now.Date.AddDays(1) ? $"{time:HH:mm} завтра"
        : time.ToString("dd.MM HH:mm");

    public static string FormatDuration(TimeSpan span)
    {
        var minutes = Math.Max(0, (int)Math.Ceiling(span.TotalMinutes));
        var h = minutes / 60;
        var m = minutes % 60;
        return h > 0 ? (m > 0 ? $"{h} год {m} хв" : $"{h} год") : $"{m} хв";
    }

    static Dictionary<DateOnly, SlotStatus[]> ForGroup(ScheduleSnapshot s, string group, DateOnly today) =>
        s.Days.Where(d => d.Date >= today && d.Groups.ContainsKey(group))
            .ToDictionary(d => d.Date, d => d.Groups[group]);
}
