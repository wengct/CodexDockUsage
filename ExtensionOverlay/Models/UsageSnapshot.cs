using System.Text.Json;
namespace CodexDockUsage.Models;
public sealed record UsageSnapshot(string Label, double? UsedPercent, DateTimeOffset? ResetsAt, string? Credits)
{
    public double? RemainingPercent => UsedPercent is null ? null : Math.Clamp(100 - UsedPercent.Value, 0, 100);
}
public static class UsageParser
{
    public static IReadOnlyList<UsageSnapshot> Parse(JsonElement response)
    {
        var list = new List<UsageSnapshot>();
        if (response.TryGetProperty("rateLimitsByLimitId", out var buckets) && buckets.ValueKind == JsonValueKind.Object)
        {
            foreach (var bucket in buckets.EnumerateObject()) Add(bucket.Name, bucket.Value);
        }
        else if (response.TryGetProperty("rateLimits", out var single) && single.ValueKind == JsonValueKind.Object) Add("codex", single);
        return list;
        void Add(string name, JsonElement bucket)
        {
            string? credits = null;
            if (bucket.TryGetProperty("credits", out var c) && c.ValueKind == JsonValueKind.Object)
                credits = c.TryGetProperty("balance", out var b) ? b.ToString() : null;
            foreach (var key in new[] { "primary", "secondary" })
            {
                if (!bucket.TryGetProperty(key, out var win) || win.ValueKind != JsonValueKind.Object) continue;
                double? used = win.TryGetProperty("usedPercent", out var u) && u.ValueKind == JsonValueKind.Number ? u.GetDouble() : null;
                DateTimeOffset? reset = win.TryGetProperty("resetsAt", out var t) && t.ValueKind == JsonValueKind.Number ? DateTimeOffset.FromUnixTimeSeconds(t.GetInt64()) : null;
                var minutes = win.TryGetProperty("windowDurationMins", out var m) && m.ValueKind == JsonValueKind.Number ? m.GetInt64().ToString() + " min" : key;
                list.Add(new UsageSnapshot(name + " / " + minutes, used, reset, credits));
            }
        }
    }
}
