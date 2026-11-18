using System.Net.Http;

namespace SvitloTray.Core;

// Picks the data source by region id
public static class ScheduleSource
{
    static readonly HttpClient Http = CreateClient();

    public static async Task<string> FetchAsync(string regionId, CancellationToken ct = default)
    {
        using var resp = await Http.GetAsync(Url(regionId), ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsStringAsync(ct);
    }

    public static ScheduleSnapshot Parse(string regionId, string json) =>
        Yasno.Handles(regionId) ? Yasno.Parse(json) : OeOutageData.Parse(json);

    public static (string Name, string Url) Credit(string regionId) =>
        Yasno.Handles(regionId)
            ? ("yasno.ua", "https://yasno.ua")
            : ("OE_OUTAGE_DATA", "https://github.com/yaroslav2901/OE_OUTAGE_DATA");

    static string Url(string regionId) => Yasno.Handles(regionId) ? Yasno.Url(regionId) : OeOutageData.Url(regionId);

    static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("SvitloTray");
        return client;
    }
}
