using SwitchToKVM.Core.Usb;

namespace SwitchToKVM.Core.Tests;

public class DeviceLearnerTests
{
    private static readonly UsbId Mouse = new(0x046D, 0xC52B);
    private static readonly UsbId Keyboard = new(0x3434, 0xD030);
    private static readonly UsbId Hub = new(0x05E3, 0x0608);

    private static DeviceLearner LearnerFor(params IReadOnlySet<UsbId>[] snapshots)
    {
        var queue = new Queue<IReadOnlySet<UsbId>>(snapshots);
        return new DeviceLearner(() => queue.Count > 1 ? queue.Dequeue() : queue.Peek(), TimeSpan.Zero);
    }

    [Fact]
    public async Task LearnsDeviceThatLeavesAndComesBack()
    {
        var learner = LearnerFor(
            new HashSet<UsbId> { Mouse, Keyboard },
            new HashSet<UsbId> { Mouse },
            new HashSet<UsbId> { Mouse, Keyboard });

        Assert.Equal(Keyboard, await learner.LearnAsync(() => { }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WaitsUntilSomethingLeaves()
    {
        var learner = LearnerFor(
            new HashSet<UsbId> { Mouse, Keyboard },
            new HashSet<UsbId> { Mouse, Keyboard },
            new HashSet<UsbId> { Mouse, Keyboard },
            new HashSet<UsbId> { Mouse },
            new HashSet<UsbId> { Mouse, Keyboard });

        Assert.Equal(Keyboard, await learner.LearnAsync(() => { }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReportsWhenDeviceLeaves()
    {
        var events = new List<string>();
        var learner = new DeviceLearner(() =>
        {
            events.Add("poll");
            return events.Count switch
            {
                1 => new HashSet<UsbId> { Keyboard },
                2 => new HashSet<UsbId>(),
                _ => new HashSet<UsbId> { Keyboard },
            };
        }, TimeSpan.Zero);

        await learner.LearnAsync(() => events.Add("gone"), TestContext.Current.CancellationToken);

        Assert.Equal(["poll", "poll", "gone", "poll"], events);
    }

    [Fact]
    public async Task IgnoresDevicesThatOnlyArrive()
    {
        var learner = LearnerFor(
            new HashSet<UsbId> { Keyboard, Hub },
            new HashSet<UsbId> { Mouse },
            new HashSet<UsbId> { Mouse, Hub });

        Assert.Equal(Hub, await learner.LearnAsync(() => { }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CanBeCancelled()
    {
        using var cancel = new CancellationTokenSource();
        var learner = new DeviceLearner(() => new HashSet<UsbId> { Keyboard }, TimeSpan.FromMilliseconds(10));

        var learning = learner.LearnAsync(() => { }, cancel.Token);
        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => learning);
    }
}
