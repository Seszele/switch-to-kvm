using System;
using Avalonia.Controls;
using SwitchToKVM.App.ViewModels;
using SwitchToKVM.Core;
using SwitchToKVM.Core.Configuration;
using SwitchToKVM.Core.Monitors;
using SwitchToKVM.Core.Startup;

namespace SwitchToKVM.App;

public sealed class SettingsHost(Window window, TrayViewModel tray) : ISettingsHost
{
    public bool StartsWithWindows
    {
        get => OperatingSystem.IsWindows() && WindowsStartup.IsEnabled();
        set
        {
            if (OperatingSystem.IsWindows() && Environment.ProcessPath is { } path)
                WindowsStartup.SetEnabled(value, path);
        }
    }

    public ScreenArea? CurrentScreen() =>
        window.Screens.ScreenFromWindow(window) is { } screen
            ? new ScreenArea(screen.Bounds.X, screen.Bounds.Y, screen.Bounds.Width, screen.Bounds.Height)
            : null;

    public void PauseSwitching() => tray.Pause();

    public void Save(Config config)
    {
        ConfigFile.Save(config, AppPaths.Config);
        tray.Apply(config);
    }

    public void Close() => window.Close();
}
