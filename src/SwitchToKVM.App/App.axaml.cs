using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using SwitchToKVM.App.ViewModels;
using SwitchToKVM.App.Views;
using SwitchToKVM.Core;
using SwitchToKVM.Core.Configuration;
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

            _tray = new TrayViewModel(OpenSettings, () => desktop.Shutdown());
            DataContext = _tray;
            desktop.Exit += (_, _) => _tray.Dispose();
            _tray.Start();

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
        var settings = new SettingsViewModel(
            tray.Config,
            () => ScreenOf(window),
            tray.Pause,
            config =>
            {
                ConfigFile.Save(config, AppPaths.Config);
                tray.Apply(config);
            },
            window.Close);
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

    private static ScreenArea? ScreenOf(Window window) =>
        window.Screens.ScreenFromWindow(window) is { } screen
            ? new ScreenArea(screen.Bounds.X, screen.Bounds.Y, screen.Bounds.Width, screen.Bounds.Height)
            : null;
}
