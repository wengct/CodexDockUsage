using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using CodexDockUsage.Pages;
namespace CodexDockUsage;
public sealed partial class CodexDockUsageCommandsProvider : CommandProvider
{
    private readonly ICommandItem[] _items;
    public CodexDockUsageCommandsProvider()
    {
        DisplayName = "Codex Usage";
        Id = "local.codexdockusage";
        var page = new UsagePage();
        _items = [new CommandItem(page) { Title = "Codex Usage" }];
    }
    public override ICommandItem[] TopLevelCommands() => _items;
    public override ICommandItem[]? GetDockBands() => _items;
}
