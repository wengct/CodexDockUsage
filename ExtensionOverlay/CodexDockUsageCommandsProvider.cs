using CodexDockUsage.Models;
using CodexDockUsage.Pages;
using CodexDockUsage.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CodexDockUsage;

public sealed partial class CodexDockUsageCommandsProvider : CommandProvider
{
    private readonly CodexUsageService _service;
    private readonly UsagePage _usagePage;
    private readonly RefreshUsageCommand _refreshCommand;
    private readonly CommandItem _mainCommandItem;
    private readonly CommandItem _refreshCommandItem;
    private readonly CommandItem _dockBandItem;
    private readonly ICommandItem[] _topLevelCommands;
    private readonly ICommandItem[] _dockBands;

    public CodexDockUsageCommandsProvider()
        : this(CodexUsageService.Shared)
    {
    }

    public CodexDockUsageCommandsProvider(CodexUsageService service)
    {
        _service = service;

        DisplayName = "Codex Usage";
        Id = "local.codexdockusage";
        var codexIcon = CodexIcon.Create();
        Icon = codexIcon;

        _usagePage = new UsagePage(_service);
        _refreshCommand = new RefreshUsageCommand(_service);

        var initial = _service.CurrentDashboard;

        _mainCommandItem = new CommandItem(_usagePage)
        {
            Title = $"Codex Usage ({initial.FormatDockTitle()})",
            Subtitle = initial.FormatDockSubtitle(),
            Icon = codexIcon,
            MoreCommands = [new CommandContextItem(_refreshCommand)],
        };

        _refreshCommandItem = new CommandItem(_refreshCommand)
        {
            Title = "重新整理 Codex 使用額度",
            Subtitle = "立即向本機 codex app-server 查詢最新額度與 Token 統計",
            Icon = new IconInfo("\uE72C"),
        };

        _dockBandItem = new CommandItem(_usagePage)
        {
            Title = initial.FormatDockTitle(),
            Subtitle = initial.FormatDockSubtitle(),
            Icon = codexIcon,
            MoreCommands = [new CommandContextItem(_refreshCommand)],
        };

        _topLevelCommands = [_mainCommandItem, _refreshCommandItem];
        _dockBands = [_dockBandItem];

        _service.DashboardUpdated += OnDashboardUpdated;
        _service.Start();
    }

    public override ICommandItem[] TopLevelCommands() => _topLevelCommands;

    public override ICommandItem[]? GetDockBands() => _dockBands;

    public override ICommandItem? GetCommandItem(string id)
    {
        foreach (var item in _topLevelCommands)
        {
            if (item.Command is ICommand cmd && cmd.Id == id)
            {
                return item;
            }
        }

        return null;
    }

    public override void Dispose()
    {
        _service.DashboardUpdated -= OnDashboardUpdated;
        _usagePage.Dispose();
        base.Dispose();
    }

    private void OnDashboardUpdated(CodexUsageDashboard dashboard)
    {
        var dockTitle = dashboard.FormatDockTitle();
        var dockSubtitle = dashboard.FormatDockSubtitle();

        _dockBandItem.Title = dockTitle;
        _dockBandItem.Subtitle = dockSubtitle;

        _mainCommandItem.Title = $"Codex Usage ({dockTitle})";
        _mainCommandItem.Subtitle = dockSubtitle;
    }
}
