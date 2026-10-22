using System;
using System.IO;
using CodexDockUsage.Models;
using CodexDockUsage.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CodexDockUsage.Pages;

internal static class CodexIcon
{
    public static IconInfo Create()
    {
        var baseDir = AppContext.BaseDirectory;
        var lightPath = Path.Combine(baseDir, "Assets", "Codex.light.png");
        var darkPath = Path.Combine(baseDir, "Assets", "Codex.dark.png");
        return new IconInfo(new IconData(lightPath), new IconData(darkPath));
    }
}

internal sealed partial class RefreshUsageCommand : InvokableCommand
{
    private readonly CodexUsageService _service;

    public RefreshUsageCommand(CodexUsageService service)
    {
        _service = service;
        Id = "local.codexdockusage.refresh";
        Name = "立即重新整理額度";
        Icon = new IconInfo("\uE72C");
    }

    public override CommandResult Invoke()
    {
        _ = _service.RefreshAsync();
        return CommandResult.KeepOpen();
    }
}

public sealed partial class UsagePage : ContentPage, IDisposable
{
    private readonly CodexUsageService _service;
    private readonly RefreshUsageCommand _refreshCommand;
    private bool _disposed;

    public UsagePage()
        : this(CodexUsageService.Shared)
    {
    }

    public UsagePage(CodexUsageService service)
    {
        _service = service;
        _refreshCommand = new RefreshUsageCommand(_service);

        Id = "local.codexdockusage.page";
        Title = "Codex Usage";
        Name = "開啟 Codex 額度面板";
        Icon = CodexIcon.Create();

        UpdateCommands(_service.CurrentDashboard);
        _service.DashboardUpdated += OnDashboardUpdated;
    }

    public override IContent[] GetContent()
    {
        var dashboard = _service.CurrentDashboard;
        return [new MarkdownContent(dashboard.ToMarkdown())];
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _service.DashboardUpdated -= OnDashboardUpdated;
    }

    private void OnDashboardUpdated(CodexUsageDashboard dashboard)
    {
        UpdateCommands(dashboard);
        RaiseItemsChanged();
    }

    private void UpdateCommands(CodexUsageDashboard dashboard)
    {
        var copySummaryCommand = new CopyTextCommand(dashboard.ToMarkdown())
        {
            Id = "local.codexdockusage.copy",
            Name = "複製 Markdown 報告",
        };

        Commands =
        [
            new CommandContextItem(_refreshCommand),
            new CommandContextItem(copySummaryCommand),
        ];
    }
}
