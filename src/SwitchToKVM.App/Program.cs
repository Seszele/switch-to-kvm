using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using SwitchToKVM.Core;
using SwitchToKVM.Core.Ipc;

namespace SwitchToKVM.App;

sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
#if DEBUG
        if (args.Contains(App.PreviewArgument))
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            return;
        }
#endif

        using var instance = new Mutex(true, @"Local\SwitchToKVM", out var isFirstInstance);
        if (!isFirstInstance)
        {
            PipeClient.SendAsync(AppPipe.Name, AppPipe.Settings, TimeSpan.FromSeconds(2)).GetAwaiter().GetResult();
            return;
        }

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