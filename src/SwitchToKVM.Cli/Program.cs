using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using SwitchToKVM.Core;
using SwitchToKVM.Core.Configuration;
using SwitchToKVM.Core.Ipc;
using SwitchToKVM.Core.Monitors.Windows;
using SwitchToKVM.Core.Usb.Windows;

Console.OutputEncoding = Encoding.UTF8;

return args switch
{
    [AppPipe.Status or AppPipe.Here or AppPipe.Away or AppPipe.Reload or AppPipe.Settings] => SendToApp(args[0]),
    [AppPipe.Quit] => Quit(),
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
        Console.WriteLine($"{monitor.Model ?? "(unknown)"}  current: 0x{monitor.GetInput():X2}  inputs: {inputs}  screen: {monitor.Area}{(monitor.IsPrimary ? " (main)" : "")}");
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

    var response = Send(AppPipe.Reload, TimeSpan.FromMilliseconds(500));
    if (response is not null) Console.WriteLine($"App: {response.Message}");
    return 0;
}

static int Run()
{
    if (Send(AppPipe.Status, TimeSpan.FromMilliseconds(500)) is not null)
    {
        Console.Error.WriteLine("The SwitchToKVM app is running, and both would switch the monitor. Exit the app first, or use 'here' and 'away'.");
        return 1;
    }
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

static int SendToApp(string command)
{
    var response = Send(command, TimeSpan.FromSeconds(1));
    if (response is null)
    {
        Console.Error.WriteLine("The SwitchToKVM app isn't running.");
        return 1;
    }
    (response.Ok ? Console.Out : Console.Error).WriteLine(response.Message);
    return response.Ok ? 0 : 1;
}

static int Quit()
{
    var response = Send(AppPipe.Quit, TimeSpan.FromSeconds(1));
    if (response is null)
    {
        Console.WriteLine("The SwitchToKVM app isn't running.");
        return 0;
    }

    var session = Process.GetCurrentProcess().SessionId;
    foreach (var process in Process.GetProcessesByName("SwitchToKVM"))
    {
        using (process)
        {
            if (process.SessionId == session) process.WaitForExit(TimeSpan.FromSeconds(10));
        }
    }
    Console.WriteLine(response.Message);
    return 0;
}

static PipeResponse? Send(string command, TimeSpan timeout) =>
    PipeClient.SendAsync(AppPipe.Name, command, timeout).GetAwaiter().GetResult();

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
    catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
    {
        Console.Error.WriteLine($"Can't read the config at {AppPaths.Config}: {e.Message}");
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
        Talk to the running app:
          status         show what the app is doing
          here           switch the monitor to this computer now
          away           switch the monitor to the other computer now
          reload         re-read the config file
          settings       open the settings window
          quit           exit the app

        Config:
          config
          config set <vendor id> <product id> <model> <here input> <away input>

        Developer tools (work without the app):
          monitors
          set <model> <hex input>
          watch <vendor id> <product id>
          run
        """);
    return 1;
}
