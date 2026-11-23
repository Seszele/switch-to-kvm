using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SwitchToKVM.Core;
using SwitchToKVM.Core.Configuration;
using SwitchToKVM.Core.Ipc;
using SwitchToKVM.Core.Monitors;
using SwitchToKVM.Core.Monitors.Windows;
using SwitchToKVM.Core.Usb;
using SwitchToKVM.Core.Usb.Windows;

namespace SwitchToKVM.App.ViewModels;

public partial class TrayViewModel(Action openSettings, Action exit) : ViewModelBase, IDisposable
{
    private Switcher? _switcher;
    private int _generation;

    [ObservableProperty] public partial string Status { get; set; } = "Starting...";
    [ObservableProperty] public partial bool IsTrayIconVisible { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PauseHeader))]
    public partial bool IsPaused { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SwitchHeader))]
    public partial bool ShowsOtherComputer { get; set; }

    public string PauseHeader => IsPaused ? "Resume automatic switching" : "Pause automatic switching";
    public string SwitchHeader => ShowsOtherComputer ? "Switch back to this computer" : "Switch to the other computer";

    public Config? Config { get; private set; }

    public Updater Updates { get; } = new();

    public void Start()
    {
        Log.Write("Started");
        if (!TryLoadConfig(out var config, out _))
        {
            Status = "Config file is invalid";
            Updates.Start(true);
            return;
        }

        Config = config;
        IsTrayIconVisible = config?.ShowTrayIcon ?? true;
        Updates.Start(config?.CheckForUpdates ?? true);
        if (config is null)
            Status = "Not set up yet";
        else
            _ = ConnectAsync(config);
    }

    public async Task<PipeResponse> HandleCommandAsync(string command)
    {
        switch (command.ToLowerInvariant())
        {
            case AppPipe.Status:
                return PipeResponse.Success(DescribeStatus());
            case AppPipe.Settings:
                openSettings();
                return PipeResponse.Success("Opened the settings window.");
            case AppPipe.Reload:
                return Reload();
            case AppPipe.Here:
                return await SwitchAsync(toHere: true);
            case AppPipe.Away:
                return await SwitchAsync(toHere: false);
            case AppPipe.Quit:
                _ = Task.Delay(TimeSpan.FromMilliseconds(300)).ContinueWith(_ => Dispatcher.UIThread.Post(exit));
                return PipeResponse.Success("SwitchToKVM is quitting.");
            default:
                return PipeResponse.Error($"Unknown command '{command}'. The app understands: status, here, away, reload, settings, quit.");
        }
    }

    public void Apply(Config config)
    {
        Log.Write("Settings saved");
        var switchingChanged = Config is null
                               || Config with { ShowTrayIcon = config.ShowTrayIcon, CheckForUpdates = config.CheckForUpdates } != config;
        Config = config;
        IsTrayIconVisible = config.ShowTrayIcon;
        Updates.SetEnabled(config.CheckForUpdates);
        if (!switchingChanged) return;
        IsPaused = false;
        _ = ConnectAsync(config);
    }

    public void SuspendForSetup()
    {
        _generation++;
        SetSwitcher(null);
        Status = "Paused during setup";
    }

    public void ResumeAfterSetup()
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
        Updates.Dispose();
    }

    private async Task ConnectAsync(Config config)
    {
        var generation = ++_generation;
        SetSwitcher(null);
        Status = $"Looking for {config.MonitorModel}...";

        try
        {
            var switcher = await Task.Run(() => CreateSwitcher(config));
            for (var attempt = 0; attempt < 3 && !switcher.HasMonitor && generation == _generation; attempt++)
            {
                await Task.Delay(TimeSpan.FromSeconds(2));
                await Task.Run(switcher.Connect);
            }

            if (generation != _generation)
            {
                switcher.Dispose();
                return;
            }

            switcher.Paused = IsPaused;
            SetSwitcher(switcher);
            Status = switcher.HasMonitor ? WatchingStatus(config) : $"{config.MonitorModel} not found";
            Log.Write(switcher.HasMonitor ? $"Found {config.MonitorModel}" : $"{config.MonitorModel} not found");
        }
        catch (Exception e)
        {
            if (generation == _generation) Status = "Failed to start, see log";
            Log.Write($"Failed to start: {e}");
        }
    }

    private PipeResponse Reload()
    {
        if (!TryLoadConfig(out var config, out var error))
            return PipeResponse.Error($"The config file is invalid, keeping the current settings. {error}");
        if (config is null)
            return PipeResponse.Error($"There's no config file at {AppPaths.Config}.");

        Log.Write("Config reloaded");
        Config = config;
        IsTrayIconVisible = config.ShowTrayIcon;
        Updates.SetEnabled(config.CheckForUpdates);
        _ = ConnectAsync(config);
        return PipeResponse.Success("Reloaded the config.");
    }

    private void SetSwitcher(Switcher? switcher)
    {
        _switcher?.Dispose();
        _switcher = switcher;
        SwitchInputCommand.NotifyCanExecuteChanged();
    }

    private string WatchingStatus(Config config) =>
        IsPaused ? $"Paused · {config.MonitorModel}" : $"Watching · {config.MonitorModel}";

    partial void OnIsPausedChanged(bool value)
    {
        Log.Write(value ? "Paused" : "Resumed");
        if (_switcher is not { } switcher) return;
        switcher.Paused = value;
        if (switcher.HasMonitor && Config is { } config) Status = WatchingStatus(config);
    }

    private async Task<PipeResponse> SwitchAsync(bool toHere)
    {
        if (_switcher is not { } switcher || Config is not { } config)
            return PipeResponse.Error($"Can't switch right now: {Status}.");

        var name = InputNames.Get(toHere ? config.HereInput : config.AwayInput);
        var ok = await Task.Run(() => switcher.Switch(toHere));
        return ok ? PipeResponse.Success($"Switched to {name}.") : PipeResponse.Error($"The monitor didn't switch to {name}.");
    }

    private string DescribeStatus()
    {
        var lines = new List<string> { $"Status: {Status}" };
        if (Config is { } config)
        {
            lines.Add($"Monitor: {config.MonitorModel}");
            lines.Add($"This computer: {InputNames.Get(config.HereInput)} (0x{config.HereInput:X2})");
            lines.Add($"Other computer: {InputNames.Get(config.AwayInput)} (0x{config.AwayInput:X2})");
            lines.Add($"USB device: {new UsbId(config.VendorId, config.ProductId)}");
        }
        lines.Add($"Config: {AppPaths.Config}");
        lines.Add($"Log: {AppPaths.Log}");
        return string.Join(Environment.NewLine, lines);
    }

    private static bool TryLoadConfig(out Config? config, out string? error)
    {
        config = null;
        error = null;
        try
        {
            config = ConfigFile.Load(AppPaths.Config);
            return true;
        }
        catch (JsonException e)
        {
            error = e.Message;
            Log.Write($"Invalid config: {e.Message}");
            return false;
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
        var name = InputNames.Get(input);
        var status = ok ? $"Switched to {name} at {DateTime.Now:HH:mm}" : $"Switch to {name} failed";
        Dispatcher.UIThread.Post(() =>
        {
            Status = status;
            if (ok) ShowsOtherComputer = input == Config?.AwayInput;
        });
    }

    private bool CanSwitch() => _switcher is not null;

    [RelayCommand(CanExecute = nameof(CanSwitch))]
    private async Task SwitchInputAsync() => await SwitchAsync(toHere: ShowsOtherComputer);

    [RelayCommand]
    private void TogglePause() => IsPaused = !IsPaused;

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
