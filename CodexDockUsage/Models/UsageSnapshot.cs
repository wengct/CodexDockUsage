using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace CodexDockUsage.Models;

public sealed record UsageSnapshot(
    string Label,
    double? UsedPercent,
    DateTimeOffset? ResetsAt,
    string? Credits,
    long? WindowDurationMins = null)
{
    public double? RemainingPercent => UsedPercent is null ? null : Math.Clamp(100 - UsedPercent.Value, 0, 100);
}

public sealed record QuotaWindowSnapshot(
    string Label,
    double? UsedPercent,
    long? WindowDurationMins,
    DateTimeOffset? ResetsAt)
{
    public double? RemainingPercent => UsedPercent is null ? null : Math.Clamp(100 - UsedPercent.Value, 0, 100);

    public string FormatRemainingShort()
    {
        return RemainingPercent is null
            ? "N/A"
            : string.Create(CultureInfo.InvariantCulture, $"{RemainingPercent.Value:0.#}%");
    }

    public string FormatUsedShort()
    {
        return UsedPercent is null
            ? "N/A"
            : string.Create(CultureInfo.InvariantCulture, $"{UsedPercent.Value:0.#}%");
    }

    public string FormatResetTime(DateTimeOffset now)
    {
        if (ResetsAt is null)
        {
            return "N/A";
        }

        var local = ResetsAt.Value.ToLocalTime();
        var diff = local - now.ToLocalTime();
        string relative;
        if (diff <= TimeSpan.Zero)
        {
            relative = "即將重置";
        }
        else if (diff.TotalDays >= 1)
        {
            relative = $"{(int)diff.TotalDays}天 {diff.Hours}小時後";
        }
        else if (diff.TotalHours >= 1)
        {
            relative = $"{(int)diff.TotalHours}小時 {diff.Minutes}分後";
        }
        else
        {
            relative = $"{Math.Max(1, diff.Minutes)}分鐘後";
        }

        return $"{local:yyyy-MM-dd HH:mm} ({relative})";
    }
}

public sealed record CreditsInfo(
    bool HasCredits,
    bool Unlimited,
    string? RawBalance,
    decimal? ParsedBalance)
{
    public string FormatShort()
    {
        if (Unlimited)
        {
            return "Unlimited";
        }

        if (ParsedBalance is not null)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{ParsedBalance.Value:0.##}");
        }

        if (!string.IsNullOrWhiteSpace(RawBalance))
        {
            return RawBalance;
        }

        return "N/A";
    }

    public string FormatDetailed()
    {
        if (Unlimited)
        {
            return "無上限 (Unlimited)";
        }

        if (ParsedBalance is not null)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{ParsedBalance.Value:N2} (原始值: {RawBalance})");
        }

        if (!string.IsNullOrWhiteSpace(RawBalance))
        {
            return RawBalance;
        }

        return "N/A";
    }
}

public sealed record ResetCreditItem(
    string Id,
    string? Title,
    string? Description,
    string Status,
    DateTimeOffset GrantedAt,
    DateTimeOffset? ExpiresAt);

public sealed record ResetCreditsInfo(
    long AvailableCount,
    IReadOnlyList<ResetCreditItem> Credits)
{
    public string FormatSummary()
    {
        if (AvailableCount <= 0 && Credits.Count == 0)
        {
            return "0";
        }

        var firstTitle = Credits.Count > 0 ? Credits[0].Title : null;
        return string.IsNullOrWhiteSpace(firstTitle)
            ? string.Create(CultureInfo.InvariantCulture, $"{AvailableCount} 次可用")
            : string.Create(CultureInfo.InvariantCulture, $"{AvailableCount} 次可用 ({firstTitle})");
    }
}

public sealed record TokenDailyBucket(
    string StartDate,
    long Tokens);

public sealed record TokenUsageInfo(
    long? LifetimeTokens,
    long? PeakDailyTokens,
    long? LongestRunningTurnSec,
    long? CurrentStreakDays,
    long? LongestStreakDays,
    IReadOnlyList<TokenDailyBucket> DailyBuckets)
{
    public TokenDailyBucket? LatestDay => DailyBuckets.Count > 0 ? DailyBuckets[^1] : null;

    public long? Recent7EntriesTokens =>
        DailyBuckets.Count == 0
            ? null
            : DailyBuckets.TakeLast(7).Sum(b => b.Tokens);

    public static string FormatTokenCountCompact(long? tokens)
    {
        if (tokens is null)
        {
            return "N/A";
        }

        double value = tokens.Value;
        if (value >= 1_000_000_000)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{value / 1_000_000_000.0:0.##}B");
        }

        if (value >= 1_000_000)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{value / 1_000_000.0:0.##}M");
        }

        if (value >= 1_000)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{value / 1_000.0:0.#}K");
        }

        return tokens.Value.ToString("N0", CultureInfo.InvariantCulture);
    }

    public static string FormatTokenCountFull(long? tokens)
    {
        return tokens is null
            ? "N/A"
            : $"{tokens.Value.ToString("N0", CultureInfo.InvariantCulture)} ({FormatTokenCountCompact(tokens)})";
    }

    public static string FormatDuration(long? seconds)
    {
        if (seconds is null)
        {
            return "N/A";
        }

        var ts = TimeSpan.FromSeconds(seconds.Value);
        if (ts.TotalHours >= 1)
        {
            return $"{(int)ts.TotalHours}小時 {ts.Minutes}分 {ts.Seconds}秒 ({seconds.Value:N0}s)";
        }

        if (ts.TotalMinutes >= 1)
        {
            return $"{ts.Minutes}分 {ts.Seconds}秒 ({seconds.Value:N0}s)";
        }

        return $"{seconds.Value:N0} 秒";
    }
}

public sealed record CodexUsageDashboard(
    DateTimeOffset FetchedAt,
    string? AccountId,
    string? PlanType,
    bool? OrdinaryUsageAllowed,
    string? RateLimitReachedType,
    bool? SpendControlReached,
    QuotaWindowSnapshot? FiveHourQuota,
    QuotaWindowSnapshot? WeeklyQuota,
    CreditsInfo? Credits,
    ResetCreditsInfo? ResetCredits,
    TokenUsageInfo? TokenUsage,
    IReadOnlyList<UsageSnapshot> AllWindows,
    string? ErrorMessage = null)
{
    public static CodexUsageDashboard CreateLoading() => new(
        FetchedAt: DateTimeOffset.Now,
        AccountId: null,
        PlanType: null,
        OrdinaryUsageAllowed: null,
        RateLimitReachedType: null,
        SpendControlReached: null,
        FiveHourQuota: null,
        WeeklyQuota: null,
        Credits: null,
        ResetCredits: null,
        TokenUsage: null,
        AllWindows: [],
        ErrorMessage: null);

    public string FormatDockTitle()
    {
        if (!string.IsNullOrWhiteSpace(ErrorMessage) && FiveHourQuota is null && WeeklyQuota is null && Credits is null)
        {
            return "Codex: 連線錯誤";
        }

        if (FiveHourQuota is null && WeeklyQuota is null && Credits is null && TokenUsage is null)
        {
            return "Codex: 載入中...";
        }

        var segments = new List<string>(3);
        if (FiveHourQuota?.RemainingPercent is not null)
        {
            segments.Add($"5h: {FiveHourQuota.FormatRemainingShort()}");
        }

        if (WeeklyQuota?.RemainingPercent is not null)
        {
            segments.Add($"週: {WeeklyQuota.FormatRemainingShort()}");
        }

        var credits = Credits?.FormatShort();
        if (!string.IsNullOrWhiteSpace(credits) && credits != "N/A")
        {
            segments.Add($"Cr: {credits}");
        }

        return segments.Count > 0 ? string.Join(" | ", segments) : "Codex Usage";
    }

    public string FormatDockSubtitle()
    {
        if (!string.IsNullOrWhiteSpace(ErrorMessage))
        {
            return $"錯誤: {ErrorMessage}";
        }

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(PlanType))
        {
            parts.Add(PlanType);
        }

        if (OrdinaryUsageAllowed == false)
        {
            parts.Add("一般額度已滿");
        }
        else if (OrdinaryUsageAllowed == true)
        {
            parts.Add("額度正常");
        }

        if (TokenUsage?.LifetimeTokens is not null)
        {
            parts.Add($"累計 {TokenUsageInfo.FormatTokenCountCompact(TokenUsage.LifetimeTokens)} tokens");
        }

        parts.Add($"更新於 {FetchedAt.ToLocalTime():HH:mm:ss}");
        return string.Join(" · ", parts);
    }

    public string ToMarkdown()
    {
        var now = DateTimeOffset.Now;
        var sb = new StringBuilder();

        sb.AppendLine("# Codex Dock Usage 監控面板");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(ErrorMessage))
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"> **連線或查詢警告：** `{ErrorMessage}`");
            sb.AppendLine();
        }

        var planDisplay = string.IsNullOrWhiteSpace(PlanType) ? "N/A" : $"`{PlanType}`";
        var allowedDisplay = OrdinaryUsageAllowed switch
        {
            true => "可用 (`true`)",
            false => "受限 / 已達上限 (`false`)",
            null => "N/A",
        };
        var limitStatusDisplay = string.IsNullOrWhiteSpace(RateLimitReachedType)
            ? "正常 (未觸發限制)"
            : $"`{RateLimitReachedType}`";

        sb.AppendLine(CultureInfo.InvariantCulture, $"- **方案類型 (Plan)：** {planDisplay}　|　**一般額度狀態：** {allowedDisplay}　|　**限制狀態：** {limitStatusDisplay}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- **最後更新時間：** `{FetchedAt.ToLocalTime():yyyy-MM-dd HH:mm:ss}`");
        sb.AppendLine();

        sb.AppendLine("## 額度與 Credits 餘額");
        sb.AppendLine();
        sb.AppendLine("| 項目 | 剩餘額度 | 已使用 | 重置時間 / 詳細資訊 |");
        sb.AppendLine("| :--- | :--- | :--- | :--- |");

        if (FiveHourQuota?.RemainingPercent is not null)
        {
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"| **5 小時額度 (5h)** | **{FiveHourQuota.FormatRemainingShort()}** | {FiveHourQuota.FormatUsedShort()} | {FiveHourQuota.FormatResetTime(now)} |");
        }

        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"| **每週額度 (Weekly)** | **{WeeklyQuota?.FormatRemainingShort() ?? "N/A"}** | {WeeklyQuota?.FormatUsedShort() ?? "N/A"} | {WeeklyQuota?.FormatResetTime(now) ?? "N/A"} |");

        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"| **Credits 餘額** | **{Credits?.FormatShort() ?? "N/A"}** | — | {Credits?.FormatDetailed() ?? "N/A"} |");

        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"| **免費重置額度** | **{ResetCredits?.FormatSummary() ?? "N/A"}** | — | {FormatResetCreditsDetail()} |");

        sb.AppendLine();
        sb.AppendLine("## Token 使用統計");
        sb.AppendLine();
        sb.AppendLine("| 統計項目 | 數值 |");
        sb.AppendLine("| :--- | :--- |");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| **累計總 Token (Lifetime)** | **{TokenUsageInfo.FormatTokenCountFull(TokenUsage?.LifetimeTokens)}** |");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| **單日最高 Token (Peak Daily)** | {TokenUsageInfo.FormatTokenCountFull(TokenUsage?.PeakDailyTokens)} |");

        var latestDayText = TokenUsage?.LatestDay is { } latest
            ? $"{TokenUsageInfo.FormatTokenCountFull(latest.Tokens)} (`{latest.StartDate}`)"
            : "N/A";
        sb.AppendLine(CultureInfo.InvariantCulture, $"| **最近單日用量 ({TokenUsage?.LatestDay?.StartDate ?? "Latest"})** | {latestDayText} |");

        sb.AppendLine(CultureInfo.InvariantCulture, $"| **近 7 筆活躍日合計** | {TokenUsageInfo.FormatTokenCountFull(TokenUsage?.Recent7EntriesTokens)} |");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| **目前連續使用天數 (Current Streak)** | {(TokenUsage?.CurrentStreakDays is { } cs ? $"{cs} 天" : "N/A")} |");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| **最長連續使用天數 (Longest Streak)** | {(TokenUsage?.LongestStreakDays is { } ls ? $"{ls} 天" : "N/A")} |");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| **最長單次執行時間 (Longest Turn)** | {TokenUsageInfo.FormatDuration(TokenUsage?.LongestRunningTurnSec)} |");

        if (TokenUsage?.DailyBuckets is { Count: > 0 } buckets)
        {
            sb.AppendLine();
            sb.AppendLine("### 近期每日 Token 用量（最近 7 筆）");
            sb.AppendLine();
            sb.AppendLine("| 日期 | Token 數量 | 簡寫 |");
            sb.AppendLine("| :--- | :--- | :--- |");
            foreach (var day in buckets.TakeLast(7).Reverse())
            {
                sb.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"| `{day.StartDate}` | {day.Tokens.ToString("N0", CultureInfo.InvariantCulture)} | `{TokenUsageInfo.FormatTokenCountCompact(day.Tokens)}` |");
            }
        }

        return sb.ToString();
    }

    private string FormatResetCreditsDetail()
    {
        if (ResetCredits is null)
        {
            return "N/A";
        }

        if (ResetCredits.Credits.Count == 0)
        {
            return "無詳細項目";
        }

        var item = ResetCredits.Credits[0];
        var exp = item.ExpiresAt is { } e
            ? $"，到期日: {e.ToLocalTime():yyyy-MM-dd}"
            : string.Empty;
        return $"{item.Description ?? item.Title ?? item.Id} (`{item.Status}`{exp})";
    }
}

public static class UsageParser
{
    public static IReadOnlyList<UsageSnapshot> Parse(JsonElement response)
    {
        var list = new List<UsageSnapshot>();
        if (response.ValueKind != JsonValueKind.Object)
        {
            return list;
        }

        if (response.TryGetProperty("rateLimitsByLimitId", out var buckets) && buckets.ValueKind == JsonValueKind.Object)
        {
            foreach (var bucket in buckets.EnumerateObject())
            {
                Add(bucket.Name, bucket.Value);
            }
        }
        else if (response.TryGetProperty("rateLimits", out var single) && single.ValueKind == JsonValueKind.Object)
        {
            Add("codex", single);
        }

        return list;

        void Add(string name, JsonElement bucket)
        {
            if (bucket.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            string? credits = null;
            if (bucket.TryGetProperty("credits", out var c) && c.ValueKind == JsonValueKind.Object)
            {
                credits = c.TryGetProperty("balance", out var b) && b.ValueKind != JsonValueKind.Null ? b.ToString() : null;
            }

            foreach (var key in new[] { "primary", "secondary" })
            {
                if (!bucket.TryGetProperty(key, out var win) || win.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                double? used = win.TryGetProperty("usedPercent", out var u) && u.ValueKind == JsonValueKind.Number
                    ? u.GetDouble()
                    : null;
                DateTimeOffset? reset = win.TryGetProperty("resetsAt", out var t) && t.ValueKind == JsonValueKind.Number
                    ? DateTimeOffset.FromUnixTimeSeconds(t.GetInt64())
                    : null;
                long? durationMins = win.TryGetProperty("windowDurationMins", out var m) && m.ValueKind == JsonValueKind.Number
                    ? m.GetInt64()
                    : null;
                var minutesLabel = durationMins is not null
                    ? $"{durationMins.Value} min"
                    : key;
                list.Add(new UsageSnapshot($"{name} / {minutesLabel}", used, reset, credits, durationMins));
            }
        }
    }

    public static CodexUsageDashboard ParseDashboard(
        JsonElement? rateLimitsResponse,
        JsonElement? tokenUsageResponse,
        DateTimeOffset? fetchedAt = null,
        string? errorMessage = null)
    {
        var timestamp = fetchedAt ?? DateTimeOffset.Now;
        string? accountId = null;
        string? planType = null;
        bool? ordinaryUsageAllowed = null;
        string? rateLimitReachedType = null;
        bool? spendControlReached = null;
        QuotaWindowSnapshot? fiveHourQuota = null;
        QuotaWindowSnapshot? weeklyQuota = null;
        CreditsInfo? creditsInfo = null;
        ResetCreditsInfo? resetCreditsInfo = null;
        IReadOnlyList<UsageSnapshot> allWindows = [];

        if (rateLimitsResponse is { ValueKind: JsonValueKind.Object } rlRoot)
        {
            allWindows = Parse(rlRoot);
            accountId = TryGetString(rlRoot, "accountId");
            ordinaryUsageAllowed = TryGetBool(rlRoot, "ordinaryUsageAllowed");
            resetCreditsInfo = ParseResetCredits(rlRoot);

            JsonElement? primaryBucket = null;
            if (rlRoot.TryGetProperty("rateLimitsByLimitId", out var buckets) && buckets.ValueKind == JsonValueKind.Object)
            {
                if (buckets.TryGetProperty("codex", out var codexBucket) && codexBucket.ValueKind == JsonValueKind.Object)
                {
                    primaryBucket = codexBucket;
                }
                else
                {
                    foreach (var prop in buckets.EnumerateObject())
                    {
                        if (prop.Value.ValueKind == JsonValueKind.Object)
                        {
                            primaryBucket = prop.Value;
                            break;
                        }
                    }
                }
            }

            if (primaryBucket is null && rlRoot.TryGetProperty("rateLimits", out var singleBucket) && singleBucket.ValueKind == JsonValueKind.Object)
            {
                primaryBucket = singleBucket;
            }

            if (primaryBucket is { ValueKind: JsonValueKind.Object } bucket)
            {
                planType = TryGetString(bucket, "planType");
                rateLimitReachedType = TryGetString(bucket, "rateLimitReachedType");
                spendControlReached = TryGetBool(bucket, "spendControlReached");
                creditsInfo = ParseCredits(bucket);

                foreach (var slot in new[] { "primary", "secondary" })
                {
                    if (!bucket.TryGetProperty(slot, out var win) || win.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    var parsedWin = ParseQuotaWindow(slot, win);
                    if (parsedWin.WindowDurationMins is { } mins)
                    {
                        if (mins <= 720 && fiveHourQuota is null)
                        {
                            fiveHourQuota = parsedWin with { Label = "5 小時額度" };
                        }
                        else if (mins >= 1440 && weeklyQuota is null)
                        {
                            weeklyQuota = parsedWin with { Label = "每週額度" };
                        }
                        else if (fiveHourQuota is null)
                        {
                            fiveHourQuota = parsedWin;
                        }
                        else if (weeklyQuota is null)
                        {
                            weeklyQuota = parsedWin;
                        }
                    }
                    else
                    {
                        if (slot == "primary" && fiveHourQuota is null)
                        {
                            fiveHourQuota = parsedWin with { Label = "5 小時額度" };
                        }
                        else if (slot == "secondary" && weeklyQuota is null)
                        {
                            weeklyQuota = parsedWin with { Label = "每週額度" };
                        }
                    }
                }
            }
        }

        TokenUsageInfo? tokenUsageInfo = null;
        if (tokenUsageResponse is { ValueKind: JsonValueKind.Object } tuRoot)
        {
            tokenUsageInfo = ParseTokenUsage(tuRoot);
        }

        return new CodexUsageDashboard(
            FetchedAt: timestamp,
            AccountId: accountId,
            PlanType: planType,
            OrdinaryUsageAllowed: ordinaryUsageAllowed,
            RateLimitReachedType: rateLimitReachedType,
            SpendControlReached: spendControlReached,
            FiveHourQuota: fiveHourQuota,
            WeeklyQuota: weeklyQuota,
            Credits: creditsInfo,
            ResetCredits: resetCreditsInfo,
            TokenUsage: tokenUsageInfo,
            AllWindows: allWindows,
            ErrorMessage: errorMessage);
    }

    private static QuotaWindowSnapshot ParseQuotaWindow(string fallbackLabel, JsonElement win)
    {
        double? used = win.TryGetProperty("usedPercent", out var u) && u.ValueKind == JsonValueKind.Number
            ? u.GetDouble()
            : null;
        long? mins = win.TryGetProperty("windowDurationMins", out var m) && m.ValueKind == JsonValueKind.Number
            ? m.GetInt64()
            : null;
        DateTimeOffset? resetsAt = win.TryGetProperty("resetsAt", out var r) && r.ValueKind == JsonValueKind.Number
            ? DateTimeOffset.FromUnixTimeSeconds(r.GetInt64())
            : null;
        return new QuotaWindowSnapshot(fallbackLabel, used, mins, resetsAt);
    }

    private static CreditsInfo? ParseCredits(JsonElement bucket)
    {
        if (!bucket.TryGetProperty("credits", out var c) || c.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        bool hasCredits = TryGetBool(c, "hasCredits") ?? false;
        bool unlimited = TryGetBool(c, "unlimited") ?? false;
        string? rawBalance = TryGetString(c, "balance");
        decimal? parsedBalance = null;
        if (!string.IsNullOrWhiteSpace(rawBalance) &&
            decimal.TryParse(rawBalance, NumberStyles.Any, CultureInfo.InvariantCulture, out var dec))
        {
            parsedBalance = dec;
        }

        return new CreditsInfo(hasCredits, unlimited, rawBalance, parsedBalance);
    }

    private static ResetCreditsInfo? ParseResetCredits(JsonElement root)
    {
        if (!root.TryGetProperty("rateLimitResetCredits", out var rc) || rc.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        long availableCount = TryGetInt64(rc, "availableCount") ?? 0;
        var items = new List<ResetCreditItem>();
        if (rc.TryGetProperty("credits", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in arr.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var id = TryGetString(item, "id") ?? string.Empty;
                var title = TryGetString(item, "title");
                var desc = TryGetString(item, "description");
                var status = TryGetString(item, "status") ?? "unknown";
                var grantedUnix = TryGetInt64(item, "grantedAt") ?? 0;
                var expiresUnix = TryGetInt64(item, "expiresAt");
                items.Add(new ResetCreditItem(
                    Id: id,
                    Title: title,
                    Description: desc,
                    Status: status,
                    GrantedAt: DateTimeOffset.FromUnixTimeSeconds(grantedUnix),
                    ExpiresAt: expiresUnix is not null ? DateTimeOffset.FromUnixTimeSeconds(expiresUnix.Value) : null));
            }
        }

        return new ResetCreditsInfo(availableCount, items);
    }

    private static TokenUsageInfo ParseTokenUsage(JsonElement root)
    {
        long? lifetimeTokens = null;
        long? peakDailyTokens = null;
        long? longestRunningTurnSec = null;
        long? currentStreakDays = null;
        long? longestStreakDays = null;

        if (root.TryGetProperty("summary", out var summary) && summary.ValueKind == JsonValueKind.Object)
        {
            lifetimeTokens = TryGetInt64(summary, "lifetimeTokens");
            peakDailyTokens = TryGetInt64(summary, "peakDailyTokens");
            longestRunningTurnSec = TryGetInt64(summary, "longestRunningTurnSec");
            currentStreakDays = TryGetInt64(summary, "currentStreakDays");
            longestStreakDays = TryGetInt64(summary, "longestStreakDays");
        }

        var buckets = new List<TokenDailyBucket>();
        if (root.TryGetProperty("dailyUsageBuckets", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in arr.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var startDate = TryGetString(item, "startDate");
                var tokens = TryGetInt64(item, "tokens");
                if (!string.IsNullOrWhiteSpace(startDate) && tokens is not null)
                {
                    buckets.Add(new TokenDailyBucket(startDate, tokens.Value));
                }
            }
        }

        return new TokenUsageInfo(
            LifetimeTokens: lifetimeTokens,
            PeakDailyTokens: peakDailyTokens,
            LongestRunningTurnSec: longestRunningTurnSec,
            CurrentStreakDays: currentStreakDays,
            LongestStreakDays: longestStreakDays,
            DailyBuckets: buckets);
    }

    private static string? TryGetString(JsonElement obj, string propName)
    {
        if (!obj.TryGetProperty(propName, out var val) || val.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return val.ValueKind == JsonValueKind.String ? val.GetString() : val.ToString();
    }

    private static bool? TryGetBool(JsonElement obj, string propName)
    {
        if (!obj.TryGetProperty(propName, out var val))
        {
            return null;
        }

        return val.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
    }

    private static long? TryGetInt64(JsonElement obj, string propName)
    {
        if (!obj.TryGetProperty(propName, out var val) || val.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        return val.TryGetInt64(out var n) ? n : null;
    }
}
