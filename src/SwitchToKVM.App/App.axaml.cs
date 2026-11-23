using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using SwitchToKVM.App.ViewModels;
using SwitchToKVM.App.Views;
using SwitchToKVM.Core;
using SwitchToKVM.Core.Configuration;
using SwitchToKVM.Core.Ipc;
using SwitchToKVM.Core.Monitors;

namespace SwitchToKVM.App;

public partial class App : Application
{
    public const string PreviewArgument = "--preview";

    private TrayViewModel? _tray;
    private SettingsWindow? _settings;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
#if DEBUG
            if (desktop.Args?.Contains(PreviewArgument) == true)
            {
                StartPreview(desktop);
                base.OnFrameworkInitializationCompleted();
                return;
            }
#endif
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var tray = new TrayViewModel(OpenSettings, () => desktop.Shutdown());
            _tray = tray;
            DataContext = tray;
            tray.Start();

            var pipe = new PipeServer(AppPipe.Name, command => Dispatcher.UIThread.InvokeAsync(() => tray.HandleCommandAsync(command)));
            desktop.Exit += (_, _) =>
            {
                pipe.Dispose();
                tray.Dispose();
            };

            if (_tray.Config is null) OpenSettings();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void StartPreview(IClassicDesktopStyleApplicationLifetime desktop)
    {
        if (GetValue(TrayIcon.IconsProperty) is { } icons)
        {
            foreach (var icon in icons) icon.IsVisible = false;
        }
        desktop.MainWindow = new PreviewWindow { DataContext = new PreviewViewModel() };
    }

    private void OpenSettings()
    {
        if (_settings is not null)
        {
            _settings.Activate();
            return;
        }

        var tray = _tray!;
        var window = new SettingsWindow();
        var settings = new SettingsViewModel(tray.Config, new SettingsHost(window, tray));
        window.DataContext = settings;
        window.PositionChanged += (_, _) => settings.RefreshCurrentScreen();
        window.Closed += (_, _) =>
        {
            _settings = null;
            settings.Dispose();
            if (settings.SetupStarted && !settings.Saved) tray.Resume();
        };
        _settings = window;

        window.Show();
        settings.Start();
    }
}
