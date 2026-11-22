using System.Runtime.InteropServices;
using System.Text;

namespace SwitchToKVM.Core.Monitors.Windows;

public sealed unsafe class WindowsMonitor : IMonitor
{
    private const byte InputSource = 0x60;

    private nint _handle;

    private WindowsMonitor(nint handle) => _handle = handle;

    public string CapabilitiesString => field ??= ReadCapabilities();
    public string? Model => Capabilities.Model(CapabilitiesString);
    public IReadOnlyList<byte> Inputs => Capabilities.Inputs(CapabilitiesString);

    public uint? GetInput() =>
        Native.GetVCPFeatureAndVCPFeatureReply(_handle, InputSource, 0, out var current, out _) ? current : null;

    public bool SetInput(byte value) => Native.SetVCPFeature(_handle, InputSource, value);

    public void Dispose()
    {
        if (_handle == 0) return;
        Native.DestroyPhysicalMonitor(_handle);
        _handle = 0;
    }

    public static WindowsMonitor? Find(string model)
    {
        var monitors = GetAll();
        var match = monitors.FirstOrDefault(m => m.Model == model);
        foreach (var monitor in monitors.Where(m => m != match)) monitor.Dispose();
        return match;
    }

    public static List<WindowsMonitor> GetAll()
    {
        var hMonitors = new List<nint>();
        var gcHandle = GCHandle.Alloc(hMonitors);
        try
        {
            Native.EnumDisplayMonitors(0, 0, &CollectMonitor, GCHandle.ToIntPtr(gcHandle));
        }
        finally
        {
            gcHandle.Free();
        }

        var result = new List<WindowsMonitor>();
        foreach (var hMonitor in hMonitors)
        {
            if (!Native.GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, out var count) || count == 0) continue;
            var physical = new Native.PhysicalMonitor[count];
            fixed (Native.PhysicalMonitor* pointer = physical)
            {
                if (!Native.GetPhysicalMonitorsFromHMONITOR(hMonitor, count, pointer)) continue;
            }
            result.AddRange(physical.Select(m => new WindowsMonitor(m.Handle)));
        }
        return result;
    }

    [UnmanagedCallersOnly]
    private static int CollectMonitor(nint hMonitor, nint hdc, nint rect, nint data)
    {
        ((List<nint>)GCHandle.FromIntPtr(data).Target!).Add(hMonitor);
        return 1;
    }

    private string ReadCapabilities()
    {
        if (!Native.GetCapabilitiesStringLength(_handle, out var length) || length == 0) return "";
        var buffer = new byte[length];
        fixed (byte* pointer = buffer)
        {
            if (!Native.CapabilitiesRequestAndCapabilitiesReply(_handle, pointer, length)) return "";
        }
        return Encoding.ASCII.GetString(buffer).TrimEnd('\0');
    }
}
