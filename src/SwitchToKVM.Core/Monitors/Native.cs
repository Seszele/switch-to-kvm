using System.Runtime.InteropServices;

namespace SwitchToKVM.Core.Monitors;

internal static unsafe partial class Native
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PhysicalMonitor
    {
        public nint Handle;
        public fixed char Description[128];
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool EnumDisplayMonitors(nint hdc, nint clip, delegate* unmanaged<nint, nint, nint, nint, int> callback, nint data);

    [LibraryImport("dxva2.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetNumberOfPhysicalMonitorsFromHMONITOR(nint hMonitor, out uint count);

    [LibraryImport("dxva2.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetPhysicalMonitorsFromHMONITOR(nint hMonitor, uint count, PhysicalMonitor* monitors);

    [LibraryImport("dxva2.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DestroyPhysicalMonitor(nint handle);

    [LibraryImport("dxva2.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetCapabilitiesStringLength(nint handle, out uint length);

    [LibraryImport("dxva2.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CapabilitiesRequestAndCapabilitiesReply(nint handle, byte* buffer, uint length);

    [LibraryImport("dxva2.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetVCPFeatureAndVCPFeatureReply(nint handle, byte code, nint codeType, out uint current, out uint maximum);

    [LibraryImport("dxva2.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetVCPFeature(nint handle, byte code, uint value);
}
