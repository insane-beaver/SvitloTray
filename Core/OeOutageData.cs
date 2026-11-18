using System.Text.Json;

namespace SvitloTray.Core;

// Data: https://github.com/yaroslav2901/OE_OUTAGE_DATA (parses oblenergo sites every 5 min)
public static class OeOutageData
{
    const string BaseUrl = "https://raw.githubusercontent.com/yaroslav2901/OE_OUTAGE_DATA/main/data/";

    public static string Url(string regionId) => BaseUrl + regionId + ".json";

    public static ScheduleSnapshot Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        DateTimeOffset? updated = root.TryGetProperty("lastUpdated", out var lu)
            && lu.ValueKind == JsonValueKind.String && lu.TryGetDateTimeOffset(out var dto) ? dto : null;

        var days = new List<DaySchedule>();
        if (root.TryGetProperty("fact", out var fact)
            && fact.TryGetProperty("data", out var data)
            && data.ValueKind == JsonValueKind.Object)
        {
            foreach (var day in data.EnumerateObject())
            {
                if (!long.TryParse(day.Name, out var ts) || day.Value.ValueKind != JsonValueKind.Object) continue;

                var groups = new Dictionary<string, SlotStatus[]>();
                foreach (var group in day.Value.EnumerateObject())
                {
                    if (group.Value.ValueKind != JsonValueKind.Object) continue;

                    var slots = new SlotStatus[48];
                    for (var h = 1; h <= 24; h++)
                    {
                        var raw = group.Value.TryGetProperty(h.ToString(), out var v) && v.ValueKind == JsonValueKind.String
                            ? v.GetString() : null;
                        (slots[(h - 1) * 2], slots[(h - 1) * 2 + 1]) = Map(raw);
                    }
                    groups[group.Name] = slots;
                }

                days.Add(new DaySchedule(DateOnly.FromDateTime(KyivTime.FromUnix(ts)), groups));
            }
        }

        days.Sort((a, b) => a.Date.CompareTo(b.Date));
        return new ScheduleSnapshot(updated, days);
    }

    static (SlotStatus, SlotStatus) Map(string? value) => value switch
    {
        "yes" => (SlotStatus.On, SlotStatus.On),
        "no" => (SlotStatus.Off, SlotStatus.Off),
        "maybe" => (SlotStatus.Maybe, SlotStatus.Maybe),
        "first" => (SlotStatus.Off, SlotStatus.On),
        "second" => (SlotStatus.On, SlotStatus.Off),
        "mfirst" => (SlotStatus.Maybe, SlotStatus.On),
        "msecond" => (SlotStatus.On, SlotStatus.Maybe),
        _ => (SlotStatus.Maybe, SlotStatus.Maybe),
    };
}
