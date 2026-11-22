namespace SwitchToKVM.Core.Ipc;

public static class AppPipe
{
    public const string Status = "status";
    public const string Here = "here";
    public const string Away = "away";
    public const string Reload = "reload";
    public const string Settings = "settings";

    public static string Name { get; } = $"SwitchToKVM-{Environment.UserName}";
}
