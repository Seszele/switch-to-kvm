using System.Runtime.InteropServices;

namespace SwitchToKVM.Core.Usb;

internal static unsafe partial class Native
{
    public const uint Success = 0;
    public const uint BufferSmall = 0x1A;
    public const uint InterfaceArrival = 0;
    public const uint InterfaceRemoval = 1;

    [StructLayout(LayoutKind.Explicit, Size = 416)]
    public struct CmNotifyFilter
    {
        [FieldOffset(0)] public uint Size;
        [FieldOffset(4)] public uint Flags;
        [FieldOffset(8)] public uint FilterType;
        [FieldOffset(12)] public uint Reserved;
        [FieldOffset(16)] public Guid ClassGuid;
    }

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    public struct CmNotifyEventData
    {
        [FieldOffset(0)] public uint FilterType;
        [FieldOffset(4)] public uint Reserved;
        [FieldOffset(8)] public Guid ClassGuid;
    }

    [LibraryImport("cfgmgr32.dll")]
    public static partial uint CM_Register_Notification(
        CmNotifyFilter* filter,
        nint context,
        delegate* unmanaged<nint, nint, uint, CmNotifyEventData*, uint, uint> callback,
        out nint notification);

    [LibraryImport("cfgmgr32.dll")]
    public static partial uint CM_Unregister_Notification(nint notification);

    [LibraryImport("cfgmgr32.dll")]
    public static partial uint CM_Get_Device_Interface_List_SizeW(out uint length, Guid* interfaceClass, char* deviceId, uint flags);

    [LibraryImport("cfgmgr32.dll")]
    public static partial uint CM_Get_Device_Interface_ListW(Guid* interfaceClass, char* deviceId, char* buffer, uint length, uint flags);
}
