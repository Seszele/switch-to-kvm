namespace SwitchToKVM.Core.Tests;

public class SwitcherTests
{
    private const byte Here = 0x0F;
    private const byte Away = 0x11;

    private readonly FakeWatcher _watcher = new();
    private readonly FakeMonitor _monitor = new();

    [Fact]
    public void DeviceGoneSwitchesAway()
    {
        using var switcher = new Switcher(_watcher, () => _monitor, Here, Away, TimeSpan.Zero);
        _watcher.Set(false);
        Assert.Equal([Away], _monitor.Inputs);
    }

    [Fact]
    public void DeviceBackSwitchesHere()
    {
        using var switcher = new Switcher(_watcher, () => _monitor, Here, Away, TimeSpan.Zero);
        _watcher.Set(false);
        _watcher.Set(true);
        Assert.Equal([Away, Here], _monitor.Inputs);
    }

    [Fact]
    public void DoesNothingAtStartup()
    {
        using var switcher = new Switcher(_watcher, () => _monitor, Here, Away, TimeSpan.Zero);
        Assert.Empty(_monitor.Inputs);
    }

    [Fact]
    public void ReportsEachSwitch()
    {
        using var switcher = new Switcher(_watcher, () => _monitor, Here, Away, TimeSpan.Zero);
        var reports = new List<(byte, bool)>();
        switcher.Switched += (input, ok) => reports.Add((input, ok));

        _watcher.Set(false);

        Assert.Equal([(Away, true)], reports);
    }

    [Fact]
    public void ReconnectsWhenMonitorStopsResponding()
    {
        var stale = new FakeMonitor { Works = false };
        var fresh = new FakeMonitor();
        var monitors = new Queue<FakeMonitor>([stale, fresh]);
        using var switcher = new Switcher(_watcher, monitors.Dequeue, Here, Away, TimeSpan.Zero);

        _watcher.Set(false);

        Assert.True(stale.Disposed);
        Assert.Equal([Away], fresh.Inputs);
    }

    [Fact]
    public void FindsMonitorLaterWhenMissingAtStartup()
    {
        var monitors = new Queue<FakeMonitor?>([null, _monitor]);
        using var switcher = new Switcher(_watcher, monitors.Dequeue, Here, Away, TimeSpan.Zero);
        Assert.False(switcher.HasMonitor);

        _watcher.Set(false);

        Assert.Equal([Away], _monitor.Inputs);
    }

    [Fact]
    public void KeepsTryingToSwitchBack()
    {
        var failures = 3;
        var monitor = new FakeMonitor { Accepts = input => input == Away || failures-- <= 0 };
        using var switcher = new Switcher(_watcher, () => monitor, Here, Away, TimeSpan.Zero);

        _watcher.Set(false);
        _watcher.Set(true);

        Assert.Equal([Away, Here], monitor.Inputs);
    }

    [Fact]
    public void SwitchesOnRequest()
    {
        using var switcher = new Switcher(_watcher, () => _monitor, Here, Away, TimeSpan.Zero);
        var reports = new List<(byte, bool)>();
        switcher.Switched += (input, ok) => reports.Add((input, ok));

        Assert.True(switcher.Switch(toHere: false));
        Assert.True(switcher.Switch(toHere: true));

        Assert.Equal([Away, Here], _monitor.Inputs);
        Assert.Equal([(Away, true), (Here, true)], reports);
    }

    [Fact]
    public void ReportsFailureWhenNoMonitor()
    {
        using var switcher = new Switcher(_watcher, () => null, Here, Away, TimeSpan.Zero);
        var reports = new List<(byte, bool)>();
        switcher.Switched += (input, ok) => reports.Add((input, ok));

        _watcher.Set(false);

        Assert.Equal([(Away, false)], reports);
    }

    [Fact]
    public void DisposeStopsWatchingAndReleasesMonitor()
    {
        var switcher = new Switcher(_watcher, () => _monitor, Here, Away, TimeSpan.Zero);
        switcher.Dispose();

        _watcher.Set(false);

        Assert.True(_watcher.Disposed);
        Assert.True(_monitor.Disposed);
        Assert.Empty(_monitor.Inputs);
    }
}
