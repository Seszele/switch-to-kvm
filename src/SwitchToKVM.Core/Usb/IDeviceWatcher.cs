namespace SwitchToKVM.Core.Usb;

public interface IDeviceWatcher : IDisposable
{
    bool IsPresent { get; }
    event Action<bool>? PresenceChanged;
}
