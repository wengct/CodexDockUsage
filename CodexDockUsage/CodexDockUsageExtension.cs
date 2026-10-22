using System;
using System.Runtime.InteropServices;
using System.Threading;
using CodexDockUsage.Services;
using Microsoft.CommandPalette.Extensions;

namespace CodexDockUsage;

[Guid("8A4D9C7E-3B21-4F65-9E12-7C8B5D4A3F10")]
public sealed partial class CodexDockUsageExtension : IExtension, IDisposable
{
    private readonly ManualResetEvent _extensionDisposedEvent;
    private readonly CodexDockUsageCommandsProvider _provider = new();

    public CodexDockUsageExtension(ManualResetEvent extensionDisposedEvent)
    {
        _extensionDisposedEvent = extensionDisposedEvent;
    }

    public object? GetProvider(ProviderType providerType)
    {
        return providerType switch
        {
            ProviderType.Commands => _provider,
            _ => null,
        };
    }

    public void Dispose()
    {
        _provider.Dispose();
        CodexUsageService.Shared.Dispose();
        _extensionDisposedEvent.Set();
    }
}
