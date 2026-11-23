using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using SwitchToKVM.Core.Configuration;
using SwitchToKVM.Core.Monitors;
using SwitchToKVM.Core.Updates;

namespace SwitchToKVM.App.ViewModels;

public sealed record PreviewScenario(string Name, SettingsViewModel ViewModel);

public partial class PreviewViewModel : ViewModelBase
{
    private static readonly Config Configured = new(0x3434, 0xD030, "PL2770QS", 0x0F, 0x11);
    private static readonly ScreenArea Left = new(0, 0, 2560, 1440);
    private static readonly ScreenArea Middle = new(2560, 0, 1920, 1080);
    private static readonly ScreenArea Right = new(4480, 0, 1920, 1080);

    public PreviewViewModel()
    {
        Scenarios =
        [
            Scenario("Overview", Configured, vm => vm.Step = SetupStep.Overview),
            Scenario("Overview, update available", Configured, vm =>
            {
                vm.Updates.Available = new AvailableUpdate(new Version(0, 2, 0), "https://github.com", null, null);
                vm.Step = SetupStep.Overview;
            }),
            Scenario("Overview, update failed", Configured, vm =>
            {
                vm.Updates.Available = new AvailableUpdate(new Version(0, 2, 0), "https://github.com", null, null);
                vm.Updates.Error = "The update didn't install. Try again later, or download it from GitHub.";
                vm.Step = SetupStep.Overview;
            }),
            Scenario("Overview, tray icon hidden", Configured with { ShowTrayIcon = false }, vm => vm.Step = SetupStep.Overview),
            Scenario("Overview, confirming quit", Configured, vm =>
            {
                vm.Step = SetupStep.Overview;
                vm.IsConfirmingQuit = true;
            }),
            Scenario("Overview, long names", new Config(0x3434, 0xD030, "U2723QE-EXTRA-LONG-MODEL", 0x1B, 0x12),
                vm => vm.Step = SetupStep.Overview),
            Scenario("Loading", null, vm => vm.Step = SetupStep.Loading),
            Scenario("Monitor: two", null, vm =>
            {
                vm.Monitors = Monitors(Left, ("PL2770QS", Left, true), ("PL2560H", Middle, false));
                vm.Step = SetupStep.Monitor;
            }),
            Scenario("Monitor: three, one picked", null, vm =>
            {
                vm.Monitors = Monitors(Right, ("PL2770QS", Left, true), ("PL2560H", Middle, false), ("U2723QE", Right, false));
                vm.SelectedMonitor = vm.Monitors[1];
                vm.Step = SetupStep.Monitor;
            }),
            Scenario("Input: three inputs", null, vm =>
            {
                Inputs(vm, 0x0F, 0x03, 0x11, 0x12);
                vm.Step = SetupStep.Input;
            }),
            Scenario("Device: press the switch", null, vm => Device(vm, SetupText.PressSwitch, SetupText.PressSwitchHint)),
            Scenario("Device: switching away", null, vm => Device(vm, SetupText.SwitchingAway, "")),
            Scenario("Device: screen didn't switch", null, vm => Device(vm, SetupText.ScreenDidntSwitch, SetupText.ScreenDidntSwitchHint)),
            Scenario("Device: switching back", null, vm => Device(vm, SetupText.SwitchingBack, "")),
            Scenario("Confirm", Configured, vm => vm.Step = SetupStep.Confirm),
            Scenario("Both computers", Configured, vm => vm.Step = SetupStep.BothComputers),
            Scenario("Troubleshoot: one input", null, vm =>
            {
                Inputs(vm, 0x0F, 0x11);
                vm.SelectedAway = vm.AwayOptions[0];
                vm.Step = SetupStep.Troubleshoot;
            }),
            Scenario("Troubleshoot: trying", null, vm =>
            {
                Inputs(vm, 0x0F, 0x03, 0x11);
                vm.SelectedAway = vm.AwayOptions[1];
                vm.IsTrying = true;
                vm.TryMessage = SetupText.Trying("HDMI 1");
                vm.Step = SetupStep.Troubleshoot;
            }),
            Scenario("Troubleshoot: tried", null, vm =>
            {
                Inputs(vm, 0x0F, 0x03, 0x11);
                vm.SelectedAway = vm.AwayOptions[1];
                vm.TryMessage = SetupText.Tried("HDMI 1");
                vm.Step = SetupStep.Troubleshoot;
            }),
            Scenario("Problem: no monitor", null, vm => vm.ShowProblem(SetupText.NoMonitor)),
            Scenario("Problem: can't read monitor", null, vm => vm.ShowProblem(SetupText.CantReadMonitor("PL2770QS"))),
            Scenario("Problem: didn't switch", null, vm => vm.ShowProblem(SetupText.DidntSwitch("PL2770QS", "HDMI 1"))),
            Scenario("Problem: didn't come back", null, vm => vm.ShowProblem(SetupText.DidntComeBack)),
            Scenario("Problem: can't save", null, vm =>
                vm.ShowProblem(SetupText.CantSave(@"Access to the path 'C:\Users\you\AppData\Roaming\SwitchToKVM\config.json' is denied."))),
        ];
        Selected = Scenarios[0];
    }

    public IReadOnlyList<PreviewScenario> Scenarios { get; }

    [ObservableProperty] public partial PreviewScenario? Selected { get; set; }

    private static PreviewScenario Scenario(string name, Config? config, Action<SettingsViewModel> arrange)
    {
        var vm = new SettingsViewModel(config, new PreviewHost());
        arrange(vm);
        return new PreviewScenario(name, vm);
    }

    private static List<MonitorOption> Monitors(ScreenArea current, params (string Model, ScreenArea Area, bool Primary)[] monitors)
    {
        var options = monitors.Select(m => new MonitorOption
        {
            Monitor = new PreviewMonitor(m.Model),
            Model = m.Model,
            Inputs = [0x0F, 0x11],
            Area = m.Area,
            IsPrimary = m.Primary,
        }).ToList();
        MonitorOption.Annotate(options, current);
        return options;
    }

    private static void Inputs(SettingsViewModel vm, byte here, params byte[] others)
    {
        vm.HereName = InputNames.Get(here);
        vm.AwayOptions = others.Select(i => new InputOption(i)).ToList();
    }

    private static void Device(SettingsViewModel vm, string message, string hint)
    {
        vm.DeviceMessage = message;
        vm.DeviceHint = hint;
        vm.Step = SetupStep.Device;
    }

    private sealed class PreviewHost : ISettingsHost
    {
        public bool StartsWithWindows { get; set; } = true;
        public Updater Updates { get; } = new();
        public ScreenArea? CurrentScreen() => null;
        public void PauseSwitching() { }
        public void Save(Config config) { }
        public void Close() { }
        public void Quit() { }
    }

    private sealed class PreviewMonitor(string model) : IMonitor
    {
        public string? Model => model;
        public bool SetInput(byte value) => true;
        public void Dispose() { }
    }
}
