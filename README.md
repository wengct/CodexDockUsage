# Codex Dock Usage — Windows starter kit

## Status

This package includes a **standalone .NET 8 App Server protocol probe**, a **rate-limit parser**, and a **PowerToys Command Palette extension overlay**. It is **not yet a complete live-updating packaged Dock application**. The host, MSIX manifest, signing and project configuration must be generated with the official Command Palette template. The extension's ContentPage currently contains a placeholder; live polling and binding are not implemented.

## Verify native Codex CLI

```powershell
where.exe codex
codex login status
codex app-server --help
cd Probe
dotnet run
```

The probe starts `codex app-server --listen stdio://`, sends initialize/initialized, requests account/rateLimits/read and account/usage/read, prints JSON, and stops the child process. Do not share the returned account data publicly.

## Build the extension scaffold

1. In Command Palette run **Create a new extension**.
2. Set ExtensionName to **CodexDockUsage** and display name to **Codex Usage**.
3. Open the generated solution in VS Code on Windows.
4. Copy the files from `ExtensionOverlay` into the generated project folder, replacing `CodexDockUsageCommandsProvider.cs`.
5. Confirm Microsoft.CommandPalette.Extensions SDK >= 0.9.260303001.
6. Build with `dotnet build` in Windows PowerShell. Deploy the generated MSIX project using Windows packaging tooling, then Reload Command Palette and enable the Dock Band.

**Important:** The included overlay only displays a static expandable flyout. The standalone Probe verifies data acquisition, and UsageParser demonstrates handling of primary/secondary windows. Combining the two with a long-lived process, cache, event notifications, error handling, and live Dock updates remains necessary before it meets the full requested feature set. Compilation of the overlay has not been validated in a Windows SDK environment.

## Debugging

Run the Probe with `dotnet run` first. For the packaged extension, attach VS Code's C# debugger to the registered extension process after launching it through Command Palette. A plain `dotnet run` of the extension project does not register an MSIX COM server.

## References

- https://learn.chatgpt.com/docs/app-server
- https://learn.microsoft.com/en-us/windows/powertoys/command-palette/adding-dock-support
- https://learn.microsoft.com/en-us/windows/powertoys/command-palette/creating-an-extension
