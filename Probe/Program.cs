using System.Text;
using System.Text.Json;
using CodexDockUsage.Models;
using CodexDockUsage.Services;

Console.OutputEncoding = Encoding.UTF8;

string? exeOverride = null;
bool printRawJson = true;

foreach (var arg in args)
{
    if (string.Equals(arg, "--no-raw", StringComparison.OrdinalIgnoreCase))
    {
        printRawJson = false;
    }
    else if (!arg.StartsWith('-'))
    {
        exeOverride = arg;
    }
}

var resolvedExe = CodexAppServerClient.FindCodexExecutable(exeOverride);
Console.WriteLine($"[Probe] 使用 Codex 執行檔: {resolvedExe}");

using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
var ct = timeout.Token;

await using var client = new CodexAppServerClient(exeOverride);
await client.EnsureInitializedAsync(ct);

JsonElement? rateLimits = null;
JsonElement? tokenUsage = null;
string? errorMessage = null;

try
{
    rateLimits = await client.ReadRateLimitsAsync(ct);
    if (printRawJson && rateLimits is { } rl)
    {
        Console.WriteLine("\n=== RATE LIMITS (raw JSON) ===");
        Console.WriteLine(rl.GetRawText());
    }
}
catch (Exception ex)
{
    errorMessage = $"讀取額度失敗: {ex.Message}";
    Console.Error.WriteLine(errorMessage);
}

try
{
    tokenUsage = await client.ReadTokenUsageAsync(ct);
    if (printRawJson && tokenUsage is { } tu)
    {
        Console.WriteLine("\n=== TOKEN USAGE (raw JSON) ===");
        Console.WriteLine(tu.GetRawText());
    }
}
catch (Exception ex)
{
    var msg = $"讀取 Token 統計失敗: {ex.Message}";
    errorMessage = errorMessage is null ? msg : $"{errorMessage} | {msg}";
    Console.Error.WriteLine(msg);
}

var dashboard = UsageParser.ParseDashboard(rateLimits, tokenUsage, DateTimeOffset.Now, errorMessage);

Console.WriteLine("\n=== DOCK BAND 預覽 ===");
Console.WriteLine($"Title   : {dashboard.FormatDockTitle()}");
Console.WriteLine($"Subtitle: {dashboard.FormatDockSubtitle()}");

Console.WriteLine("\n=== 展開面板 (IContentPage Markdown) 預覽 ===");
Console.WriteLine(dashboard.ToMarkdown());
