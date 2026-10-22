using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CodexDockUsage.Models;

namespace CodexDockUsage.Services;

public sealed partial class CodexUsageService : IDisposable
{
    public static CodexUsageService Shared { get; } = new();

    private readonly CodexAppServerClient _client;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly System.Timers.Timer _pollTimer;

    private JsonElement? _lastRateLimits;
    private JsonElement? _lastTokenUsage;
    private bool _disposed;

    public CodexUsageService(string? executableOverride = null, TimeSpan? pollInterval = null)
    {
        _client = new CodexAppServerClient(executableOverride);
        _client.RateLimitsUpdated += OnRateLimitsUpdatedNotification;

        var interval = pollInterval ?? TimeSpan.FromSeconds(60);
        _pollTimer = new System.Timers.Timer(interval.TotalMilliseconds)
        {
            AutoReset = true,
        };
        _pollTimer.Elapsed += (_, _) => _ = RefreshAsync();
    }

    public event Action<CodexUsageDashboard>? DashboardUpdated;

    public CodexUsageDashboard CurrentDashboard { get; private set; } = CodexUsageDashboard.CreateLoading();

    public void Start()
    {
        if (_disposed)
        {
            return;
        }

        _pollTimer.Start();
        _ = RefreshAsync();
    }

    public async Task<CodexUsageDashboard> RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            return CurrentDashboard;
        }

        if (!await _refreshLock.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            _refreshLock.Release();
            return CurrentDashboard;
        }

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(20));
            var ct = timeoutCts.Token;

            string? errorMessage = null;

            try
            {
                _lastRateLimits = await _client.ReadRateLimitsAsync(ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                errorMessage = $"讀取額度失敗: {ex.Message}";
            }

            try
            {
                _lastTokenUsage = await _client.ReadTokenUsageAsync(ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                errorMessage = errorMessage is null
                    ? $"讀取 Token 統計失敗: {ex.Message}"
                    : $"{errorMessage} | Token 統計: {ex.Message}";
            }

            var updated = UsageParser.ParseDashboard(
                _lastRateLimits,
                _lastTokenUsage,
                DateTimeOffset.Now,
                errorMessage);

            CurrentDashboard = updated;
            DashboardUpdated?.Invoke(updated);
            return updated;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _pollTimer.Stop();
        _pollTimer.Dispose();
        _client.RateLimitsUpdated -= OnRateLimitsUpdatedNotification;
        _client.Dispose();
        _refreshLock.Dispose();
    }

    private void OnRateLimitsUpdatedNotification(JsonElement notificationParams)
    {
        _ = RefreshAsync();
    }
}
