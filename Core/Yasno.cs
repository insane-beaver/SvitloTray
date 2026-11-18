using System.Text.Json;

namespace SvitloTray.Core;

// Kyiv and Dnipro (DTEK) via Yasno public API, format as used by denysdovhan/ha-yasno-outages
public static class Yasno
{
    const string Prefix = "yasno-";

    public static bool Handles(string regionId) => regionId.StartsWith(Prefix);

    // regionId format: yasno-{region}-{dso}
    public static string Url(string regionId)
    {
        var parts = regionId[Prefix.Length..].Split('-');
        return $"https://app.yasno.ua/api/blackout-service/public/shutdowns/regions/{parts[0]}/dsos/{parts[1]}/planned-outages";
    }

    public static ScheduleSnapshot Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var byDate = new SortedDictionary<DateOnly, Dictionary<string, SlotStatus[]>>();
        DateTimeOffset? updated = null;

        if (root.ValueKind != JsonValueKind.Object) return new(null, []);

        foreach (var group in root.EnumerateObject())
        {
            if (group.Value.ValueKind != JsonValueKind.Object) continue;
            var groupId = "GPV" + group.Name;

            if (group.Value.TryGetProperty("updatedOn", out var u) && u.ValueKind == JsonValueKind.String
                && u.TryGetDateTimeOffset(out var dto) && (updated is null || dto > updated))
                updated = dto;

            foreach (var key in new[] { "today", "tomorrow" })
            {
                if (!group.Value.TryGetProperty(key, out var day) || day.ValueKind != JsonValueKind.Object) continue;
                if (!day.TryGetProperty("date", out var d) || !d.TryGetDateTimeOffset(out var date)) continue;

                var slots = ParseDay(day);
                if (slots is null) continue;

                var dateOnly = DateOnly.FromDateTime(date.DateTime);
                if (!byDate.TryGetValue(dateOnly, out var groups)) byDate[dateOnly] = groups = [];
                groups[groupId] = slots;
            }
        }

        return new(updated, byDate.Select(kv => new DaySchedule(kv.Key, kv.Value)).ToList());
    }

    static SlotStatus[]? ParseDay(JsonElement day)
    {
        var status = day.TryGetProperty("status", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString() : null;
        if (status == "WaitingForSchedule") return null;

        var slots = new SlotStatus[48];
        if (status == "NoOutages") return slots;
        if (status == "EmergencyShutdowns") return Enumerable.Repeat(SlotStatus.Maybe, 48).ToArray();

        if (!day.TryGetProperty("slots", out var list) || list.ValueKind != JsonValueKind.Array) return slots;

        foreach (var item in list.EnumerateArray())
        {
            if (!item.TryGetProperty("start", out var st) || !st.TryGetInt32(out var start)) continue;
            if (!item.TryGetProperty("end", out var en) || !en.TryGetInt32(out var end)) continue;
            var type = item.TryGetProperty("type", out var t) ? t.GetString() : null;
            var value = type switch
            {
                "Definite" => SlotStatus.Off,
                "NotPlanned" => SlotStatus.On,
                _ => SlotStatus.Maybe,
            };

            for (var i = Math.Max(0, start / 30); i < Math.Min(48, (end + 29) / 30); i++)
                slots[i] = value;
        }
        return slots;
    }
}
