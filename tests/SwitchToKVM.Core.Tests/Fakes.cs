using SwitchToKVM.Core.Monitors;
using SwitchToKVM.Core.Usb;

namespace SwitchToKVM.Core.Tests;

internal sealed class FakeWatcher : IDeviceWatcher
{
    public bool IsPresent { get; private set; } = true;
    public bool Disposed { get; private set; }
    public event Action<bool>? PresenceChanged;

    public void Set(bool present)
    {
        IsPresent = present;
        PresenceChanged?.Invoke(present);
    }

    public void Dispose() => Disposed = true;
}

internal sealed class FakeMonitor(string model = "TEST") : IMonitor
{
    public string? Model => model;
    public bool Works { get; set; } = true;
    public bool Disposed { get; private set; }
    public List<byte> Inputs { get; } = [];

    public bool SetInput(byte value)
    {
        if (!Works) return false;
        Inputs.Add(value);
        return true;
    }

    public void Dispose() => Disposed = true;
}
