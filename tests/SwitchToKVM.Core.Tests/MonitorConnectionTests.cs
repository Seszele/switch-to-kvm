using SwitchToKVM.Core.Monitors;

namespace SwitchToKVM.Core.Tests;

public class MonitorConnectionTests
{
    private const byte Input = 0x11;

    [Fact]
    public void UsesGivenMonitorWithoutSearching()
    {
        var monitor = new FakeMonitor();
        using var connection = new MonitorConnection(() => throw new InvalidOperationException(), TimeSpan.Zero, monitor);

        Assert.True(connection.SetInput(Input));
        Assert.Equal([Input], monitor.Inputs);
    }

    [Fact]
    public void KeepsTryingWhileMonitorIsBusy()
    {
        var failures = 5;
        var monitor = new FakeMonitor { Accepts = _ => failures-- <= 0 };
        using var connection = new MonitorConnection(() => monitor, TimeSpan.Zero, monitor);

        Assert.True(connection.SetInput(Input));
        Assert.Equal([Input], monitor.Inputs);
    }

    [Fact]
    public void FindsMonitorThatCameBack()
    {
        var gone = new FakeMonitor { Works = false };
        var back = new FakeMonitor();
        var found = new Queue<FakeMonitor?>([null, null, back]);
        using var connection = new MonitorConnection(found.Dequeue, TimeSpan.Zero, gone);

        Assert.True(connection.SetInput(Input));
        Assert.True(gone.Disposed);
        Assert.Equal([Input], back.Inputs);
    }

    [Fact]
    public void GivesUpAfterAllAttempts()
    {
        var searches = 0;
        using var connection = new MonitorConnection(() => { searches++; return null; }, TimeSpan.Zero);

        Assert.False(connection.SetInput(Input));
        Assert.Equal(MonitorConnection.Attempts, searches);
    }

    [Fact]
    public void DisposeReleasesMonitor()
    {
        var monitor = new FakeMonitor();
        var connection = new MonitorConnection(() => null, TimeSpan.Zero, monitor);
        connection.Dispose();
        Assert.True(monitor.Disposed);
    }
}
