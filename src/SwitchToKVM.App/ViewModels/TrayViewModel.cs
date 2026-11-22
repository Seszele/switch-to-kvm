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

public partial class TrayViewModel(Action exit) : ViewModelBase, IDisposable
{
    private Switcher? _switcher;

    [ObservableProperty] public partial string Status { get; set; } = "Starting...";

    public void Start()
    {
        Log.Write("Started");
        try
        {
            var config = ConfigFile.Load(AppPaths.Config);
            if (config is null)
            {
                Status = "Not set up yet";
                return;
            }
            Status = $"Looking for {config.MonitorModel}...";
            Task.Run(() => Connect(config));
        }
        catch (JsonException e)
        {
            Status = "Config file is invalid";
            Log.Write($"Invalid config: {e.Message}");
        }
    }

    public void Dispose() => _switcher?.Dispose();

    private void Connect(Config config)
    {
        try
        {
            var switcher = new Switcher(
                new WindowsDeviceWatcher(config.VendorId, config.ProductId),
                () => WindowsMonitor.Find(config.MonitorModel),
                config.HereInput,
                config.AwayInput);
            switcher.Switched += OnSwitched;
            _switcher = switcher;

            SetStatus(switcher.HasMonitor ? $"Watching · {config.MonitorModel}" : $"{config.MonitorModel} not found");
            Log.Write(switcher.HasMonitor ? $"Found {config.MonitorModel}" : $"{config.MonitorModel} not found");
        }
        catch (Exception e)
        {
            SetStatus("Failed to start, see log");
            Log.Write($"Failed to start: {e}");
        }
    }

    private void OnSwitched(byte input, bool ok)
    {
        Log.Write($"Switch to 0x{input:X2}: {(ok ? "OK" : "FAILED")}");
        SetStatus(ok ? $"Switched to 0x{input:X2} at {DateTime.Now:HH:mm}" : $"Switch to 0x{input:X2} failed");
    }

    private void SetStatus(string status) => Dispatcher.UIThread.Post(() => Status = status);

    [RelayCommand]
    private void OpenLog()
    {
        if (File.Exists(AppPaths.Log))
            Process.Start(new ProcessStartInfo(AppPaths.Log) { UseShellExecute = true });
    }

    [RelayCommand]
    private void Exit() => exit();
}
