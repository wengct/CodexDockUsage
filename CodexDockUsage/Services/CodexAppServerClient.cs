using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CodexDockUsage.Services;

public sealed partial class CodexAppServerClient : IAsyncDisposable, IDisposable
{
    private readonly string? _executableOverride;
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonElement>> _pendingRequests = new();

    private Process? _process;
    private CancellationTokenSource? _readerCts;
    private Task? _readerTask;
    private int _nextRequestId;
    private bool _initialized;
    private bool _disposed;

    public CodexAppServerClient(string? executableOverride = null)
    {
        _executableOverride = executableOverride;
    }

    public event Action<JsonElement>? RateLimitsUpdated;

    public bool IsRunning => _process is { HasExited: false } && _initialized;

    public static ProcessStartInfo ResolveCodexStartInfo(string? executableOverride = null, string arguments = "app-server --listen stdio://")
    {
        var candidate = FindCodexExecutable(executableOverride);
        if (candidate.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) ||
            candidate.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
        {
            var comSpec = Environment.GetEnvironmentVariable("COMSPEC");
            if (string.IsNullOrWhiteSpace(comSpec))
            {
                comSpec = "cmd.exe";
            }

            return new ProcessStartInfo(comSpec, $"/d /s /c \"\"{candidate}\" {arguments}\"")
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
        }

        return new ProcessStartInfo(candidate, arguments)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
    }

    public static string FindCodexExecutable(string? executableOverride = null)
    {
        if (!string.IsNullOrWhiteSpace(executableOverride) && !string.Equals(executableOverride, "codex", StringComparison.OrdinalIgnoreCase))
        {
            if (File.Exists(executableOverride))
            {
                return TryResolveNativeBinaryFromShim(executableOverride) ?? executableOverride;
            }
        }

        var envOverride = Environment.GetEnvironmentVariable("CODEX_CLI_PATH");
        if (!string.IsNullOrWhiteSpace(envOverride) && File.Exists(envOverride))
        {
            return TryResolveNativeBinaryFromShim(envOverride) ?? envOverride;
        }

        var searchDirs = new List<string>();
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrWhiteSpace(pathEnv))
        {
            foreach (var part in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                searchDirs.Add(part);
            }
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            searchDirs.Add(Path.Combine(userProfile, "scoop", "apps", "nvm", "current", "nodejs", "nodejs"));
            searchDirs.Add(Path.Combine(userProfile, "scoop", "persist", "nvm", "nodejs", "nodejs"));
            searchDirs.Add(Path.Combine(userProfile, "AppData", "Roaming", "npm"));
            searchDirs.Add(Path.Combine(userProfile, "AppData", "Local", "pnpm"));
            searchDirs.Add(Path.Combine(userProfile, ".bun", "bin"));
        }

        foreach (var dir in searchDirs)
        {
            try
            {
                var exePath = Path.Combine(dir, "codex.exe");
                if (File.Exists(exePath))
                {
                    return exePath;
                }

                var cmdPath = Path.Combine(dir, "codex.cmd");
                if (File.Exists(cmdPath))
                {
                    return TryResolveNativeBinaryFromShim(cmdPath) ?? cmdPath;
                }
            }
            catch
            {
                // Ignore invalid directory entries in PATH
            }
        }

        return executableOverride ?? "codex.cmd";
    }

    public async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _lifecycleLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_process is { HasExited: false } && _initialized)
            {
                return;
            }

            StopProcessInternal();

            var startInfo = ResolveCodexStartInfo(_executableOverride);
            var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            if (!process.Start())
            {
                throw new InvalidOperationException("無法啟動 Codex app-server 行程。");
            }

            _process = process;
            _readerCts = new CancellationTokenSource();
            _readerTask = Task.Run(() => ReadOutputLoopAsync(process, _readerCts.Token), CancellationToken.None);

            using var initTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            initTimeout.CancelAfter(TimeSpan.FromSeconds(15));

            var initId = Interlocked.Increment(ref _nextRequestId);
            var initPayload = BuildInitializeRequestJson(initId);
            await SendRequestRawAsync(initId, initPayload, initTimeout.Token).ConfigureAwait(false);

            await SendNotificationRawAsync("{\"method\":\"initialized\",\"params\":{}}", initTimeout.Token).ConfigureAwait(false);
            _initialized = true;
        }
        catch
        {
            StopProcessInternal();
            throw;
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async Task<JsonElement> ReadRateLimitsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var id = Interlocked.Increment(ref _nextRequestId);
        var payload = $"{{\"id\":{id},\"method\":\"account/rateLimits/read\",\"params\":null}}";
        return await SendRequestRawAsync(id, payload, cancellationToken).ConfigureAwait(false);
    }

    public async Task<JsonElement> ReadTokenUsageAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var id = Interlocked.Increment(ref _nextRequestId);
        var payload = $"{{\"id\":{id},\"method\":\"account/usage/read\",\"params\":null}}";
        return await SendRequestRawAsync(id, payload, cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StopProcessInternal();
        _lifecycleLock.Dispose();
        _writeLock.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    private static string? TryResolveNativeBinaryFromShim(string shimPath)
    {
        try
        {
            var dir = Path.GetDirectoryName(shimPath);
            if (string.IsNullOrWhiteSpace(dir))
            {
                return null;
            }

            var isArm64 = RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
            var pkgName = isArm64 ? "codex-win32-arm64" : "codex-win32-x64";
            var triple = isArm64 ? "aarch64-pc-windows-msvc" : "x86_64-pc-windows-msvc";

            string[] candidates =
            [
                Path.Combine(dir, "node_modules", "@openai", "codex", "node_modules", "@openai", pkgName, "vendor", triple, "bin", "codex.exe"),
                Path.Combine(dir, "node_modules", "@openai", pkgName, "vendor", triple, "bin", "codex.exe"),
                Path.Combine(dir, "node_modules", "@openai", "codex", "vendor", triple, "bin", "codex.exe"),
            ];

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }
        catch
        {
            // Fallback to shim
        }

        return null;
    }

    private static string BuildInitializeRequestJson(int id)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("id", id);
            writer.WriteString("method", "initialize");
            writer.WritePropertyName("params");
            writer.WriteStartObject();
            writer.WritePropertyName("clientInfo");
            writer.WriteStartObject();
            writer.WriteString("name", "codex_dock_usage");
            writer.WriteString("title", "Codex Dock Usage");
            writer.WriteString("version", "0.2.0");
            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private async Task<JsonElement> SendRequestRawAsync(int id, string jsonLine, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingRequests[id] = tcs;

        using var reg = cancellationToken.Register(() =>
        {
            if (_pendingRequests.TryRemove(id, out var pending))
            {
                pending.TrySetCanceled(cancellationToken);
            }
        });

        try
        {
            await SendNotificationRawAsync(jsonLine, cancellationToken).ConfigureAwait(false);
            return await tcs.Task.ConfigureAwait(false);
        }
        finally
        {
            _pendingRequests.TryRemove(id, out _);
        }
    }

    private async Task SendNotificationRawAsync(string jsonLine, CancellationToken cancellationToken)
    {
        var process = _process ?? throw new InvalidOperationException("Codex app-server 尚未啟動。");
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await process.StandardInput.WriteLineAsync(jsonLine.AsMemory(), cancellationToken).ConfigureAwait(false);
            await process.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task ReadOutputLoopAsync(Process process, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await process.StandardOutput.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (line is null)
                {
                    break;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.Number && idProp.TryGetInt32(out var id))
                    {
                        if (_pendingRequests.TryRemove(id, out var tcs))
                        {
                            if (root.TryGetProperty("error", out var errorProp) && errorProp.ValueKind != JsonValueKind.Null)
                            {
                                tcs.TrySetException(new InvalidOperationException(errorProp.ToString()));
                            }
                            else if (root.TryGetProperty("result", out var resultProp))
                            {
                                tcs.TrySetResult(resultProp.Clone());
                            }
                            else
                            {
                                tcs.TrySetException(new InvalidOperationException("JSON-RPC 回應缺少 result 欄位。"));
                            }
                        }

                        continue;
                    }

                    if (root.TryGetProperty("method", out var methodProp) && methodProp.ValueKind == JsonValueKind.String)
                    {
                        var method = methodProp.GetString();
                        if (string.Equals(method, "account/rateLimits/updated", StringComparison.Ordinal) &&
                            root.TryGetProperty("params", out var paramsProp))
                        {
                            var clonedParams = paramsProp.Clone();
                            _ = Task.Run(() => RateLimitsUpdated?.Invoke(clonedParams), CancellationToken.None);
                        }
                    }
                }
                catch
                {
                    // Ignore non-JSON diagnostic lines
                }
            }
        }
        catch
        {
            // Stream closed or cancelled
        }
        finally
        {
            foreach (var kvp in _pendingRequests)
            {
                if (_pendingRequests.TryRemove(kvp.Key, out var pending))
                {
                    pending.TrySetException(new EndOfStreamException("Codex app-server 已停止。"));
                }
            }
        }
    }

    private void StopProcessInternal()
    {
        _initialized = false;

        try
        {
            _readerCts?.Cancel();
            _readerCts?.Dispose();
        }
        catch
        {
        }

        _readerCts = null;
        _readerTask = null;

        if (_process is not null)
        {
            try
            {
                if (!_process.HasExited)
                {
                    _process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
            }
            finally
            {
                _process.Dispose();
                _process = null;
            }
        }

        foreach (var kvp in _pendingRequests)
        {
            if (_pendingRequests.TryRemove(kvp.Key, out var pending))
            {
                pending.TrySetCanceled();
            }
        }
    }
}
