namespace SwitchToKVM.App.ViewModels;

public sealed record SetupProblem(string Title, string Message, bool SuggestAlternative = false);

public static class SetupText
{
    public const string PressSwitch = "Press the button on your USB switch now.";
    public const string PressSwitchHint = "Your keyboard, mouse and screen move to the other computer. Press the button again to come back here.";
    public const string SwitchingAway = "Switching the screen to your other computer...";
    public const string PressAgain = "Press the button again to come back.";
    public const string ScreenDidntSwitch = "The screen didn't switch.";
    public const string ScreenDidntSwitchHint = "Press the button on your USB switch again to come back here, then we'll look at why.";
    public const string SwitchingBack = "Switching back...";

    public const string ChooseInputFirst = "Choose an input first.";
    public const string TryOneFirst = "Choose an input to try first.";

    public static readonly SetupProblem NoMonitor = new(
        "No monitor found",
        "SwitchToKVM couldn't find a monitor it can control. Check that DDC/CI is turned on in your monitor's own menu, then try again.");

    public static readonly SetupProblem DidntComeBack = new(
        "Your monitor didn't come back",
        "Use the buttons on your monitor to switch back to this computer. Your monitor doesn't accept commands while it shows the other computer, so SwitchToKVM can't switch it back for you.",
        SuggestAlternative: true);

    public static SetupProblem CantReadMonitor(string model) => new(
        "Couldn't read the monitor",
        $"SwitchToKVM couldn't tell which input your {model} is showing right now. Make sure it is showing this computer, then try again.");

    public static SetupProblem DidntSwitch(string model, string input) => new(
        "Your monitor didn't switch",
        $"SwitchToKVM couldn't switch your {model} to {input}. Check that DDC/CI is turned on in your monitor's own menu, then try again.");

    public static SetupProblem CantSave(string reason) => new("Couldn't save your settings", reason);

    public static string Trying(string input) => $"Showing {input} for 10 seconds, then coming back here...";
    public static string Tried(string input) => $"Did you see your other computer on {input}? If so, choose \"Use this one\".";
    public static string TryDidntSwitch(string input) => $"The monitor didn't switch to {input}.";
}
