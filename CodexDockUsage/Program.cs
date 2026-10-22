using System;
using System.IO;
using System.Threading;
using Microsoft.CommandPalette.Extensions;
using Shmuelie.WinRTServer;
using Shmuelie.WinRTServer.CsWinRT;

namespace CodexDockUsage;

public static class Program
{
    [MTAThread]
    public static void Main(string[] args)
    {
        Directory.SetCurrentDirectory(AppContext.BaseDirectory);

        if (args.Length > 0 && args[0] == "-RegisterProcessAsComServer")
        {
            ComServer server = new();
            using ManualResetEvent extensionDisposedEvent = new(false);

            CodexDockUsageExtension extensionInstance = new(extensionDisposedEvent);
            server.RegisterClass<CodexDockUsageExtension, IExtension>(() => extensionInstance);
            server.Start();

            extensionDisposedEvent.WaitOne();
            server.Stop();
            server.UnsafeDispose();
        }
        else
        {
            Console.WriteLine("CodexDockUsage 是 PowerToys Command Palette 擴充功能（MSIX COM Server）。");
            Console.WriteLine("若要直接測試本機 Codex App Server 資料擷取與解析，請執行 Probe 專案：");
            Console.WriteLine("  dotnet run --project Probe");
        }
    }
}
