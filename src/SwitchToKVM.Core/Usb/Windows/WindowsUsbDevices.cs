namespace SwitchToKVM.Core.Usb.Windows;

public static unsafe class WindowsUsbDevices
{
    internal static readonly Guid UsbDeviceInterface = new("A5DCBF10-6530-11D2-901F-00C04FB951ED");

    public static IReadOnlySet<UsbId> GetPresent() =>
        GetInterfacePaths().Select(UsbId.FromPath).OfType<UsbId>().ToHashSet();

    internal static List<string> GetInterfacePaths()
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
