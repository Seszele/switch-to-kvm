using System.Globalization;
using SwitchToKVM.Core.Monitors;

var monitors = DdcMonitor.GetAll();
try
{
    switch (args)
    {
        case ["monitors"]:
            foreach (var monitor in monitors)
            {
                var inputs = string.Join(" ", monitor.Inputs.Select(i => $"0x{i:X2}"));
                Console.WriteLine($"{monitor.Model ?? "(unknown)"}  current: 0x{monitor.GetInput():X2}  inputs: {inputs}");
            }
            return 0;

        case ["set", var model, var value]:
            var target = monitors.FirstOrDefault(m => m.Model == model);
            if (target is null)
            {
                Console.Error.WriteLine($"No monitor with model {model}");
                return 1;
            }
            if (!byte.TryParse(value, NumberStyles.HexNumber, null, out var input))
            {
                Console.Error.WriteLine($"Not a hex byte: {value}");
                return 1;
            }
            var ok = target.SetInput(input);
            Console.WriteLine(ok ? "OK" : "Failed");
            return ok ? 0 : 1;

        default:
            Console.WriteLine("Usage: monitors | set <model> <hex input>");
            return 1;
    }
}
finally
{
    foreach (var monitor in monitors) monitor.Dispose();
}
