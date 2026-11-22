using SwitchToKVM.Core.Monitors;
using SwitchToKVM.Core.Usb;

namespace SwitchToKVM.Core;

public sealed class Switcher : IDisposable
{
    private readonly IDeviceWatcher _watcher;
    private readonly MonitorConnection _monitor;
    private readonly byte _here;
    private readonly byte _away;

    public Switcher(IDeviceWatcher watcher, Func<IMonitor?> findMonitor, byte here, byte away, TimeSpan? retryDelay = null)
    {
        _watcher = watcher;
        _here = here;
        _away = away;
        _monitor = new MonitorConnection(findMonitor, retryDelay ?? TimeSpan.FromSeconds(1));
        _monitor.Connect();
        _watcher.PresenceChanged += OnPresenceChanged;
    }

    public bool HasMonitor => _monitor.HasMonitor;

    public event Action<byte, bool>? Switched;

    public void Dispose()
    {
        _watcher.PresenceChanged -= OnPresenceChanged;
        _watcher.Dispose();
        _monitor.Dispose();
    }

    private void OnPresenceChanged(bool present)
    {
        var input = present ? _here : _away;
        var ok = _monitor.SetInput(input);
        Switched?.Invoke(input, ok);
    }
}
