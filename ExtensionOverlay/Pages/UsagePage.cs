using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
namespace CodexDockUsage.Pages;
public sealed partial class UsagePage : ContentPage
{
    public UsagePage()
    {
        Id = "local.codexdockusage.page";
        Title = "Codex Usage";
        Name = "Codex Usage";
    }
    public override IContent[] GetContent()
    {
        return [new MarkdownContent("# Codex Usage\n\nRun the included Probe to verify App Server access.\n\nThis overlay is a minimal Dock integration scaffold; wire the shared polling service after confirming your returned schema.")];
    }
}
