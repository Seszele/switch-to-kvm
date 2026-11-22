using System.Globalization;
using System.Text.Json;
using SwitchToKVM.Core;
using SwitchToKVM.Core.Configuration;
using SwitchToKVM.Core.Monitors.Windows;
using SwitchToKVM.Core.Usb.Windows;

return args switch
{
    ["monitors"] => ListMonitors(),
    ["set", var model, var value] => SetInput(model, value),
    ["watch", var vendorId, var productId] => Watch(vendorId, productId),
    ["config"] => ShowConfig(),
    ["config", "set", var vendorId, var productId, var model, var here, var away] => SetConfig(vendorId, productId, model, here, away),
    ["run"] => Run(),
    _ => Usage(),
};

static int ListMonitors()
{
    var monitors = WindowsMonitor.GetAll();
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
    if (!TryParseHex(value, out var input)) return 1;

    using var monitor = WindowsMonitor.Find(model);
    if (monitor is null)
    {
        Console.Error.WriteLine($"No monitor with model {model}");
        return 1;
    }
    var ok = monitor.SetInput(input);
    Console.WriteLine(ok ? "OK" : "Failed");
    return ok ? 0 : 1;
}

static int Watch(string vendorId, string productId)
{
    if (!TryParseIds(vendorId, productId, out var vid, out var pid)) return 1;

    using var watcher = new WindowsDeviceWatcher(vid, pid);
    watcher.PresenceChanged += present =>
        Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff}  {(present ? "back" : "gone")}");

    Console.WriteLine($"Present: {watcher.IsPresent}. Press Enter to stop.");
    Console.ReadLine();
    return 0;
}

static int ShowConfig()
{
    Console.WriteLine(AppPaths.Config);
    if (!TryLoadConfig(out _)) return 1;
    Console.WriteLine(File.ReadAllText(AppPaths.Config));
    return 0;
}

static int SetConfig(string vendorId, string productId, string model, string here, string away)
{
    if (!TryParseIds(vendorId, productId, out var vid, out var pid)) return 1;
    if (!TryParseHex(here, out var hereInput) || !TryParseHex(away, out var awayInput)) return 1;

    ConfigFile.Save(new Config(vid, pid, model, hereInput, awayInput), AppPaths.Config);
    Console.WriteLine($"Saved {AppPaths.Config}");
    return 0;
}

static int Run()
{
    if (!TryLoadConfig(out var config)) return 1;

    Console.WriteLine($"Looking for monitor {config.MonitorModel}...");
    using var switcher = new Switcher(
        new WindowsDeviceWatcher(config.VendorId, config.ProductId),
        () => WindowsMonitor.Find(config.MonitorModel),
        config.HereInput,
        config.AwayInput);
    if (!switcher.HasMonitor) Console.WriteLine("Monitor not found yet, will retry on the first switch.");

    switcher.Switched += (input, ok) =>
        Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff}  input 0x{input:X2}  {(ok ? "OK" : "FAILED")}");

    Console.WriteLine("Running. Press your USB switch. Press Enter to stop.");
    Console.ReadLine();
    return 0;
}

static bool TryLoadConfig(out Config config)
{
    config = null!;
    try
    {
        var loaded = ConfigFile.Load(AppPaths.Config);
        if (loaded is null)
        {
            Console.Error.WriteLine($"No config at {AppPaths.Config}. Create one with: config set <vendor id> <product id> <model> <here input> <away input>");
            return false;
        }
        config = loaded;
        return true;
    }
    catch (JsonException e)
    {
        Console.Error.WriteLine($"Invalid config at {AppPaths.Config}: {e.Message}");
        return false;
    }
}

static bool TryParseIds(string vendorId, string productId, out ushort vid, out ushort pid)
{
    pid = 0;
    if (ushort.TryParse(vendorId, NumberStyles.HexNumber, null, out vid) &&
        ushort.TryParse(productId, NumberStyles.HexNumber, null, out pid))
        return true;
    Console.Error.WriteLine("Vendor and product IDs must be hex, e.g. 3434 D030");
    return false;
}

static bool TryParseHex(string value, out byte result)
{
    if (byte.TryParse(value, NumberStyles.HexNumber, null, out result)) return true;
    Console.Error.WriteLine($"Not a hex byte: {value}");
    return false;
}

static int Usage()
{
    Console.WriteLine("""
        Usage:
          monitors
          set <model> <hex input>
          watch <vendor id> <product id>
          config
          config set <vendor id> <product id> <model> <here input> <away input>
          run
        """);
    return 1;
}
