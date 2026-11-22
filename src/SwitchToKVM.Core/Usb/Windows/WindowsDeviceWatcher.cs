using System.Runtime.InteropServices;

namespace SwitchToKVM.Core.Usb.Windows;

public sealed unsafe class WindowsDeviceWatcher : IDeviceWatcher
{
    private static readonly Guid UsbDeviceInterface = new("A5DCBF10-6530-11D2-901F-00C04FB951ED");

    private readonly string _id;
    private readonly HashSet<string> _present = [with(StringComparer.OrdinalIgnoreCase)];
    private readonly Lock _lock = new();
    private GCHandle _self;
    private nint _notification;

    public WindowsDeviceWatcher(ushort vendorId, ushort productId)
    {
        _id = $"VID_{vendorId:X4}&PID_{productId:X4}";
        _self = GCHandle.Alloc(this);

        var filter = new Native.CmNotifyFilter
        {
            Size = (uint)sizeof(Native.CmNotifyFilter),
            ClassGuid = UsbDeviceInterface,
        };
        var result = Native.CM_Register_Notification(&filter, GCHandle.ToIntPtr(_self), &OnNotification, out _notification);
        if (result != Native.Success)
        {
            _self.Free();
            throw new InvalidOperationException($"CM_Register_Notification failed with {result}");
        }

        lock (_lock)
        {
            foreach (var link in GetPresentInterfaces().Where(Matches))
                _present.Add(link);
        }
    }

    public event Action<bool>? PresenceChanged;

    public bool IsPresent
    {
        get { lock (_lock) return _present.Count > 0; }
    }

    public void Dispose()
    {
        if (_notification == 0) return;
        Native.CM_Unregister_Notification(_notification);
        _notification = 0;
        _self.Free();
    }

    private bool Matches(string link) =>
        link.Contains(_id + "#", StringComparison.OrdinalIgnoreCase) ||
        link.Contains(_id + "&", StringComparison.OrdinalIgnoreCase);

    private void OnInterfaceChanged(uint action, string link)
    {
        if (!Matches(link)) return;
        lock (_lock)
        {
            var wasPresent = _present.Count > 0;
            if (action == Native.InterfaceArrival) _present.Add(link);
            else if (action == Native.InterfaceRemoval) _present.Remove(link);
            else return;

            var isPresent = _present.Count > 0;
            if (isPresent != wasPresent) PresenceChanged?.Invoke(isPresent);
        }
    }

    [UnmanagedCallersOnly]
    private static uint OnNotification(nint notification, nint context, uint action, Native.CmNotifyEventData* data, uint size)
    {
        try
        {
            var watcher = (WindowsDeviceWatcher)GCHandle.FromIntPtr(context).Target!;
            watcher.OnInterfaceChanged(action, ReadSymbolicLink(data, size));
        }
        catch (Exception e)
        {
            Log.Write($"Device notification failed: {e}");
        }
        return Native.Success;
    }

    private static string ReadSymbolicLink(Native.CmNotifyEventData* data, uint size)
    {
        var offset = sizeof(Native.CmNotifyEventData);
        if (size <= offset) return "";
        var chars = new ReadOnlySpan<char>((byte*)data + offset, (int)(size - offset) / sizeof(char));
        var end = chars.IndexOf('\0');
        return new string(end >= 0 ? chars[..end] : chars);
    }

    private static List<string> GetPresentInterfaces()
    {
        var guid = UsbDeviceInterface;
        while (true)
        {
            if (Native.CM_Get_Device_Interface_List_SizeW(out var length, &guid, null, 0) != Native.Success) return [];
            var buffer = new char[length];
            fixed (char* pointer = buffer)
            {
                var result = Native.CM_Get_Device_Interface_ListW(&guid, null, pointer, length, 0);
                if (result == Native.BufferSmall) continue;
                if (result != Native.Success) return [];
            }
            return new string(buffer).Split('\0', StringSplitOptions.RemoveEmptyEntries).ToList();
        }
    }
}
