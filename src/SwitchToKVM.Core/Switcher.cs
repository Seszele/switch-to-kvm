using SwitchToKVM.Core.Monitors;
using SwitchToKVM.Core.Usb;

namespace SwitchToKVM.Core;

public sealed class Switcher : IDisposable
{
    private readonly IDeviceWatcher _watcher;
    private readonly Func<IMonitor?> _findMonitor;
    private readonly byte _here;
    private readonly byte _away;
    private IMonitor? _monitor;

    public Switcher(IDeviceWatcher watcher, Func<IMonitor?> findMonitor, byte here, byte away)
    {
        _watcher = watcher;
        _findMonitor = findMonitor;
        _here = here;
        _away = away;
        _monitor = findMonitor();
        _watcher.PresenceChanged += OnPresenceChanged;
    }

    public bool HasMonitor => _monitor is not null;

    public event Action<byte, bool>? Switched;

    public void Dispose()
    {
        _watcher.PresenceChanged -= OnPresenceChanged;
        _watcher.Dispose();
        _monitor?.Dispose();
    }

    private void OnPresenceChanged(bool present)
    {
        var input = present ? _here : _away;
        Switched?.Invoke(input, TrySetInput(input));
    }

    private bool TrySetInput(byte input)
    {
        if (_monitor?.SetInput(input) == true) return true;

        _monitor?.Dispose();
        _monitor = _findMonitor();
        return _monitor?.SetInput(input) == true;
    }
}
