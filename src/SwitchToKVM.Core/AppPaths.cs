namespace SwitchToKVM.Core;

public static class AppPaths
{
    public static string Folder { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SwitchToKVM");

    public static string Config { get; } = Path.Combine(Folder, "config.json");
    public static string Log { get; } = Path.Combine(Folder, "log.txt");
}
