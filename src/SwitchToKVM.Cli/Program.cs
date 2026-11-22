using System.Globalization;
using SwitchToKVM.Core.Monitors;
using SwitchToKVM.Core.Usb;

return args switch
{
    ["monitors"] => ListMonitors(),
    ["set", var model, var value] => SetInput(model, value),
    ["watch", var vendorId, var productId] => Watch(vendorId, productId),
    _ => Usage(),
};

static int ListMonitors()
{
    var monitors = DdcMonitor.GetAll();
    foreach (var monitor in monitors)
    {
        var inputs = string.Join(" ", monitor.Inputs.Select(i => $"0x{i:X2}"));
        Console.WriteLine($"{monitor.Model ?? "(unknown)"}  current: 0x{monitor.GetInput():X2}  inputs: {inputs}");
        monitor.Dispose();
    }
    return 0;
}

static int SetInput(string model, string value)
{
    if (!byte.TryParse(value, NumberStyles.HexNumber, null, out var input))
    {
        Console.Error.WriteLine($"Not a hex byte: {value}");
        return 1;
    }

    var monitors = DdcMonitor.GetAll();
    try
    {
        var target = monitors.FirstOrDefault(m => m.Model == model);
        if (target is null)
        {
            Console.Error.WriteLine($"No monitor with model {model}");
            return 1;
        }
        var ok = target.SetInput(input);
        Console.WriteLine(ok ? "OK" : "Failed");
        return ok ? 0 : 1;
    }
    finally
    {
        foreach (var monitor in monitors) monitor.Dispose();
    }
}

static int Watch(string vendorId, string productId)
{
    if (!ushort.TryParse(vendorId, NumberStyles.HexNumber, null, out var vid) ||
        !ushort.TryParse(productId, NumberStyles.HexNumber, null, out var pid))
    {
        Console.Error.WriteLine("Vendor and product IDs must be hex, e.g. watch 3434 D030");
        return 1;
    }

    using var watcher = new UsbDeviceWatcher(vid, pid);
    watcher.PresenceChanged += present =>
        Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff}  {(present ? "back" : "gone")}");

    Console.WriteLine($"Present: {watcher.IsPresent}. Press Enter to stop.");
    Console.ReadLine();
    return 0;
}

static int Usage()
{
    Console.WriteLine("Usage: monitors | set <model> <hex input> | watch <vendor id> <product id>");
    return 1;
}
