using System.Text.RegularExpressions;

namespace SwitchToKVM.Core.Monitors;

internal static partial class Capabilities
{
    public static string? Model(string capabilities)
    {
        var match = ModelRegex().Match(capabilities);
        return match.Success ? match.Groups[1].Value : null;
    }

    public static IReadOnlyList<byte> Inputs(string capabilities)
    {
        var match = InputsRegex().Match(capabilities);
        if (!match.Success) return [];
        return HexByteRegex().Matches(match.Groups[1].Value)
            .Select(hex => Convert.ToByte(hex.Value, 16))
            .ToArray();
    }

    [GeneratedRegex(@"model\(([^)]*)\)")]
    private static partial Regex ModelRegex();

    [GeneratedRegex(@"(?<![0-9A-Fa-f])60\(([0-9A-Fa-f ]*)\)")]
    private static partial Regex InputsRegex();

    [GeneratedRegex("[0-9A-Fa-f]{2}")]
    private static partial Regex HexByteRegex();
}
