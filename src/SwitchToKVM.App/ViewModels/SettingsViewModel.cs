using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SwitchToKVM.Core;
using SwitchToKVM.Core.Configuration;
using SwitchToKVM.Core.Monitors;
using SwitchToKVM.Core.Monitors.Windows;
using SwitchToKVM.Core.Usb;
using SwitchToKVM.Core.Usb.Windows;

namespace SwitchToKVM.App.ViewModels;

public enum SetupStep
{
    Overview,
    Loading,
    Problem,
    Monitor,
    Input,
    Device,
    Confirm,
    Troubleshoot,
    BothComputers,
}

public sealed partial class MonitorOption : ObservableObject
{
    public required IMonitor Monitor { get; init; }
    public required string Model { get; init; }
    public required IReadOnlyList<byte> Inputs { get; init; }
    public byte? CurrentInput { get; init; }
    public ScreenArea? Area { get; init; }
    public bool IsPrimary { get; init; }
    public string? Position { get; set; }
    [ObservableProperty] public partial bool IsCurrent { get; set; }

    public string Details => string.Join(" · ", new[] { Position, IsPrimary ? "Main display" : null }.OfType<string>());

    public static void Annotate(IReadOnlyList<MonitorOption> monitors, ScreenArea? current)
    {
        var ordered = monitors.Where(m => m.Area is not null).OrderBy(m => m.Area!.Value.CenterX).ToList();
        if (ordered.Count >= 2)
        {
            for (var i = 0; i < ordered.Count; i++)
                ordered[i].Position = i == 0 ? "Left" : i == ordered.Count - 1 ? "Right" : "Middle";
        }
        MarkCurrent(monitors, current);
    }

    public static void MarkCurrent(IReadOnlyList<MonitorOption> monitors, ScreenArea? current)
    {
        foreach (var monitor in monitors) monitor.IsCurrent = current is not null && monitor.Area == current;
    }
}

public sealed record InputOption(byte Value)
{
    public string Name => InputNames.Get(Value);
}

public partial class SettingsViewModel : ViewModelBase, IDisposable
{
    private static readonly TimeSpan TryDuration = TimeSpan.FromSeconds(10);

    private readonly ISettingsHost _host;
    private readonly CancellationTokenSource _cancel = new();
    private List<MonitorOption> _monitors = [];
    private MonitorOption? _monitor;
    private MonitorConnection? _connection;
    private byte _here;
    private Config? _config;

    public SettingsViewModel(Config? current, ISettingsHost host)
    {
        _config = current;
        _host = host;
        ShowTrayIcon = current?.ShowTrayIcon ?? true;
        StartWithWindows = host.StartsWithWindows;
        if (current is not null) ShowSummary(current);
    }

    public bool SetupStarted { get; private set; }
    public bool Saved { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOverview), nameof(IsLoading), nameof(IsProblem), nameof(IsMonitor),
        nameof(IsInput), nameof(IsDevice), nameof(IsConfirm), nameof(IsTroubleshoot), nameof(IsBothComputers))]
    public partial SetupStep Step { get; set; } = SetupStep.Loading;

    public bool IsOverview => Step == SetupStep.Overview;
    public bool IsLoading => Step == SetupStep.Loading;
    public bool IsProblem => Step == SetupStep.Problem;
    public bool IsMonitor => Step == SetupStep.Monitor;
    public bool IsInput => Step == SetupStep.Input;
    public bool IsDevice => Step == SetupStep.Device;
    public bool IsConfirm => Step == SetupStep.Confirm;
    public bool IsTroubleshoot => Step == SetupStep.Troubleshoot;
    public bool IsBothComputers => Step == SetupStep.BothComputers;

    [ObservableProperty] public partial SetupProblem? Problem { get; set; }

    [ObservableProperty] public partial IReadOnlyList<MonitorOption> Monitors { get; set; } = [];
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UseSelectedMonitorCommand))]
    public partial MonitorOption? SelectedMonitor { get; set; }

    [ObservableProperty] public partial string HereName { get; set; } = "";
    [ObservableProperty] public partial IReadOnlyList<InputOption> AwayOptions { get; set; } = [];
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UseSelectedInputCommand), nameof(TryInputCommand), nameof(UseTriedInputCommand))]
    public partial InputOption? SelectedAway { get; set; }

    [ObservableProperty] public partial string DeviceMessage { get; set; } = "";
    [ObservableProperty] public partial string DeviceHint { get; set; } = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UseTriedInputCommand))]
    public partial bool IsTrying { get; set; }
    [ObservableProperty] public partial string? TryMessage { get; set; }

    [ObservableProperty] public partial string SummaryMonitor { get; set; } = "";
    [ObservableProperty] public partial string SummaryHere { get; set; } = "";
    [ObservableProperty] public partial string SummaryAway { get; set; } = "";

    [ObservableProperty] public partial bool ShowTrayIcon { get; set; }
    [ObservableProperty] public partial bool StartWithWindows { get; set; }
    [ObservableProperty] public partial bool IsConfirmingQuit { get; set; }

    public void Start()
    {
        if (_config is null)
            _ = StartSetupAsync();
        else
            Step = SetupStep.Overview;
    }

    public void RefreshCurrentScreen() => MonitorOption.MarkCurrent(Monitors, _host.CurrentScreen());

    public void ShowProblem(SetupProblem problem)
    {
        Problem = problem;
        Step = SetupStep.Problem;
    }

    public void Dispose()
    {
        _cancel.Cancel();
        _cancel.Dispose();
        DisposeMonitors();
    }

    [RelayCommand]
    private async Task StartSetupAsync()
    {
        if (!SetupStarted)
        {
            SetupStarted = true;
            _host.PauseSwitching();
        }

        Step = SetupStep.Loading;
        DisposeMonitors();
        _monitors = await Task.Run(FindMonitors);

        if (_monitors.Count == 0)
        {
            ShowProblem(SetupText.NoMonitor);
            return;
        }

        if (_monitors.Count == 1)
        {
            ChooseMonitor(_monitors[0]);
            return;
        }

        MonitorOption.Annotate(_monitors, _host.CurrentScreen());
        Monitors = _monitors;
        SelectedMonitor = null;
        Step = SetupStep.Monitor;
    }

    private bool HasSelectedMonitor() => SelectedMonitor is not null;

    [RelayCommand(CanExecute = nameof(HasSelectedMonitor))]
    private void UseSelectedMonitor()
    {
        if (SelectedMonitor is not null) ChooseMonitor(SelectedMonitor);
    }

    private void ChooseMonitor(MonitorOption option)
    {
        if (option.CurrentInput is not { } here || !option.Inputs.Contains(here))
        {
            ShowProblem(SetupText.CantReadMonitor(option.Model));
            return;
        }

        _monitor = option;
        _here = here;
        _connection?.Dispose();
        _connection = null;
        HereName = InputNames.Get(here);
        AwayOptions = option.Inputs.Where(i => i != here).Select(i => new InputOption(i)).ToList();

        if (AwayOptions.Count == 1)
        {
            SelectedAway = AwayOptions[0];
            _ = LearnDeviceAsync();
            return;
        }

        SelectedAway = null;
        Step = SetupStep.Input;
    }

    private bool HasSelectedAway() => SelectedAway is not null;

    private bool CanUseTriedInput() => SelectedAway is not null && !IsTrying;

    [RelayCommand(CanExecute = nameof(HasSelectedAway))]
    private void UseSelectedInput() => _ = LearnDeviceAsync();

    private async Task LearnDeviceAsync()
    {
        if (_monitor is null || SelectedAway is null) return;
        var monitor = _monitor;
        var here = _here;
        var away = SelectedAway;
        var connection = Connection();

        DeviceMessage = SetupText.PressSwitch;
        DeviceHint = SetupText.PressSwitchHint;
        Step = SetupStep.Device;

        var learner = new DeviceLearner(WindowsUsbDevices.GetPresent, TimeSpan.FromMilliseconds(250));
        var switchedAway = Task.FromResult(false);
        try
        {
            var device = await learner.LearnAsync(() => switchedAway = SwitchAwayAsync(connection, away), _cancel.Token);

            DeviceMessage = SetupText.SwitchingBack;
            DeviceHint = "";
            var awayOk = await switchedAway;
            var hereOk = await Task.Run(() => connection.SetInput(here));
            Log.Write($"Setup learned device {device}, switch away {(awayOk ? "OK" : "FAILED")}, back {(hereOk ? "OK" : "FAILED")}");

            if (!awayOk)
            {
                ShowProblem(SetupText.DidntSwitch(monitor.Model, away.Name));
                return;
            }

            _config = new Config(device.VendorId, device.ProductId, monitor.Model, here, away.Value, ShowTrayIcon);
            ShowSummary(_config);
            Step = hereOk ? SetupStep.Confirm : SetupStep.BothComputers;
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task<bool> SwitchAwayAsync(MonitorConnection connection, InputOption away)
    {
        DeviceMessage = SetupText.SwitchingAway;
        DeviceHint = "";
        var ok = await Task.Run(() => connection.SetInput(away.Value));
        if (Step != SetupStep.Device) return ok;

        DeviceMessage = ok ? SetupText.PressAgain : SetupText.ScreenDidntSwitch;
        DeviceHint = ok ? "" : SetupText.ScreenDidntSwitchHint;
        return ok;
    }

    [RelayCommand]
    private void ConfirmDidntComeBack() => Step = SetupStep.BothComputers;

    [RelayCommand]
    private void ConfirmNo()
    {
        TryMessage = null;
        Step = SetupStep.Troubleshoot;
    }

    [RelayCommand(CanExecute = nameof(HasSelectedAway))]
    private async Task TryInputAsync()
    {
        if (SelectedAway is not { } input || _monitor is null) return;

        var connection = Connection();
        IsTrying = true;
        TryMessage = SetupText.Trying(input.Name);

        var switched = await Task.Run(() => connection.SetInput(input.Value));
        if (switched)
        {
            await Task.Delay(TryDuration);
            if (!await Task.Run(() => connection.SetInput(_here)))
            {
                IsTrying = false;
                ShowProblem(SetupText.DidntComeBack);
                return;
            }
        }

        IsTrying = false;
        TryMessage = switched ? SetupText.Tried(input.Name) : SetupText.TryDidntSwitch(input.Name);
    }

    [RelayCommand(CanExecute = nameof(CanUseTriedInput))]
    private void UseTriedInput() => _ = LearnDeviceAsync();

    [RelayCommand]
    private void Finish()
    {
        if (_config is null) return;
        try
        {
            _host.Save(_config);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.Write($"Could not save config: {e.Message}");
            ShowProblem(SetupText.CantSave(e.Message));
            return;
        }

        Saved = true;
        _host.Close();
    }

    [RelayCommand]
    private void Close() => _host.Close();

    [RelayCommand]
    private void AskQuit() => IsConfirmingQuit = true;

    [RelayCommand]
    private void CancelQuit() => IsConfirmingQuit = false;

    [RelayCommand]
    private void Quit() => _host.Quit();

    partial void OnShowTrayIconChanged(bool value)
    {
        if (_config is null || _config.ShowTrayIcon == value || Step != SetupStep.Overview) return;
        try
        {
            _config = _config with { ShowTrayIcon = value };
            _host.Save(_config);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.Write($"Could not save the tray icon setting: {e.Message}");
        }
    }

    partial void OnStartWithWindowsChanged(bool value)
    {
        if (value == _host.StartsWithWindows) return;
        try
        {
            _host.StartsWithWindows = value;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            Log.Write($"Could not change start with Windows: {e.Message}");
        }
    }

    private MonitorConnection Connection()
    {
        var monitor = _monitor!;
        return _connection ??= new MonitorConnection(
            () => WindowsMonitor.Find(monitor.Model), TimeSpan.FromSeconds(1), monitor.Monitor);
    }

    private void ShowSummary(Config config)
    {
        SummaryMonitor = config.MonitorModel;
        SummaryHere = InputNames.Get(config.HereInput);
        SummaryAway = InputNames.Get(config.AwayInput);
    }

    private void DisposeMonitors()
    {
        _connection?.Dispose();
        _connection = null;
        foreach (var option in _monitors) option.Monitor.Dispose();
        _monitors = [];
        _monitor = null;
    }

    private static List<MonitorOption> FindMonitors()
    {
        var result = new List<MonitorOption>();
        foreach (var monitor in WindowsMonitor.GetAll())
        {
            if (monitor.Model is { } model && monitor.Inputs.Count >= 2)
            {
                result.Add(new MonitorOption
                {
                    Monitor = monitor,
                    Model = model,
                    Inputs = monitor.Inputs,
                    CurrentInput = (byte?)(monitor.GetInput() & 0xFF),
                    Area = monitor.Area,
                    IsPrimary = monitor.IsPrimary,
                });
            }
            else
            {
                monitor.Dispose();
            }
        }
        return result;
    }
}
