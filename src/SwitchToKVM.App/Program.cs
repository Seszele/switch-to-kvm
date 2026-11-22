using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using SwitchToKVM.Core;

namespace SwitchToKVM.App;

sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Contains(App.PreviewArgument))
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            return;
        }

        using var instance = new Mutex(true, @"Local\SwitchToKVM", out var isFirstInstance);
        if (!isFirstInstance) return;

        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Write($"Crashed: {e.ExceptionObject}");
        TaskScheduler.UnobservedTaskException += (_, e) => Log.Write($"Background task failed: {e.Exception}");

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}