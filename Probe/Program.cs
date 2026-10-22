using System.Diagnostics;
using System.Text.Json;

var exe = args.Length > 0 ? args[0] : "codex";
var start = new ProcessStartInfo(exe, "app-server --listen stdio://")
{
    RedirectStandardInput = true, RedirectStandardOutput = true,
    RedirectStandardError = true, UseShellExecute = false,
    CreateNoWindow = true
};
using var process = new Process { StartInfo = start };
if (!process.Start()) throw new Exception("Cannot start Codex app-server");
using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
var ct = timeout.Token;
async Task<JsonElement> Request(int id, string method, object? parameters = null)
{
    await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new { id, method, @params = parameters }));
    await process.StandardInput.FlushAsync();
    while (true)
    {
        var line = await process.StandardOutput.ReadLineAsync(ct) ?? throw new EndOfStreamException("App server stopped");
        using var doc = JsonDocument.Parse(line);
        var root = doc.RootElement;
        if (!root.TryGetProperty("id", out var responseId) || responseId.ValueKind != JsonValueKind.Number || responseId.GetInt32() != id) continue;
        if (root.TryGetProperty("error", out var error)) throw new InvalidOperationException(error.ToString());
        return root.GetProperty("result").Clone();
    }
}
try
{
    await Request(1, "initialize", new { clientInfo = new { name = "codex_dock_usage", title = "Codex Dock Usage", version = "0.1.0" } });
    await process.StandardInput.WriteLineAsync("{\"method\":\"initialized\",\"params\":{}}");
    await process.StandardInput.FlushAsync();
    var limits = await Request(2, "account/rateLimits/read");
    Console.WriteLine("RATE LIMITS:\n" + JsonSerializer.Serialize(limits, new JsonSerializerOptions { WriteIndented = true }));
    try
    {
        var usage = await Request(3, "account/usage/read");
        Console.WriteLine("TOKEN USAGE:\n" + JsonSerializer.Serialize(usage, new JsonSerializerOptions { WriteIndented = true }));
    }
    catch (Exception ex) { Console.Error.WriteLine("Token usage unavailable: " + ex.Message); }
}
finally
{
    if (!process.HasExited) process.Kill(entireProcessTree: true);
}
