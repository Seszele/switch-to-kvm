using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SwitchToKVM.Core;
using SwitchToKVM.Core.Configuration;
using SwitchToKVM.Core.Monitors.Windows;
using SwitchToKVM.Core.Usb.Windows;

namespace SwitchToKVM.App.ViewModels;

public partial class TrayViewModel(Action openSettings, Action exit) : ViewModelBase, IDisposable
{
    private Switcher? _switcher;
    private int _generation;

    [ObservableProperty] public partial string Status { get; set; } = "Starting...";

    public Config? Config { get; private set; }

    public void Start()
    {
        Log.Write("Started");
        try
        {
            Config = ConfigFile.Load(AppPaths.Config);
        }
        catch (JsonException e)
        {
            Status = "Config file is invalid";
            Log.Write($"Invalid config: {e.Message}");
            return;
        }

        if (Config is null)
            Status = "Not set up yet";
        else
            _ = ConnectAsync(Config);
    }

    public void Apply(Config config)
    {
        Log.Write("Settings saved");
        Config = config;
        _ = ConnectAsync(config);
    }

    public void Pause()
    {
        _generation++;
        _switcher?.Dispose();
        _switcher = null;
        Status = "Paused during setup";
    }

    public void Resume()
    {
        if (Config is null)
            Status = "Not set up yet";
        else
            _ = ConnectAsync(Config);
    }

    public void Dispose()
    {
        _generation++;
        _switcher?.Dispose();
    }

    private async Task ConnectAsync(Config config)
    {
        var generation = ++_generation;
        _switcher?.Dispose();
        _switcher = null;
        Status = $"Looking for {config.MonitorModel}...";

        try
        {
            var switcher = await Task.Run(() => CreateSwitcher(config));
            if (generation != _generation)
            {
                switcher.Dispose();
                return;
            }

            _switcher = switcher;
            Status = switcher.HasMonitor ? $"Watching · {config.MonitorModel}" : $"{config.MonitorModel} not found";
            Log.Write(switcher.HasMonitor ? $"Found {config.MonitorModel}" : $"{config.MonitorModel} not found");
        }
        catch (Exception e)
        {
            if (generation == _generation) Status = "Failed to start, see log";
            Log.Write($"Failed to start: {e}");
        }
    }

    private Switcher CreateSwitcher(Config config)
    {
        var switcher = new Switcher(
            new WindowsDeviceWatcher(config.VendorId, config.ProductId),
            () => WindowsMonitor.Find(config.MonitorModel),
            config.HereInput,
            config.AwayInput);
        switcher.Switched += OnSwitched;
        return switcher;
    }

    private void OnSwitched(byte input, bool ok)
    {
        Log.Write($"Switch to 0x{input:X2}: {(ok ? "OK" : "FAILED")}");
        var status = ok ? $"Switched to 0x{input:X2} at {DateTime.Now:HH:mm}" : $"Switch to 0x{input:X2} failed";
        Dispatcher.UIThread.Post(() => Status = status);
    }

    [RelayCommand]
    private void OpenSettings() => openSettings();

    [RelayCommand]
    private void OpenLog()
    {
        if (File.Exists(AppPaths.Log))
            Process.Start(new ProcessStartInfo(AppPaths.Log) { UseShellExecute = true });
    }

    [RelayCommand]
    private void Exit() => exit();
}
