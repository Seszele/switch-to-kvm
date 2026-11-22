using System.Runtime.CompilerServices;

namespace SwitchToKVM.Core.Tests;

internal static class TestSetup
{
    [ModuleInitializer]
    internal static void Initialize() =>
        Log.Path = Path.Combine(Path.GetTempPath(), "SwitchToKVM-tests", "log.txt");
}
