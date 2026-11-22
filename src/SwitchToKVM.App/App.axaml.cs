using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using SwitchToKVM.App.ViewModels;

namespace SwitchToKVM.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var tray = new TrayViewModel(() => desktop.Shutdown());
            DataContext = tray;
            desktop.Exit += (_, _) => tray.Dispose();
            tray.Start();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
