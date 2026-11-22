using System.Globalization;
using System.Text.RegularExpressions;

namespace SwitchToKVM.Core.Usb;

public readonly partial record struct UsbId(ushort VendorId, ushort ProductId)
{
    public static UsbId? FromPath(string path)
    {
        var match = IdRegex().Match(path);
        if (!match.Success) return null;
        return new UsbId(
            ushort.Parse(match.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            ushort.Parse(match.Groups[2].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    }

    public override string ToString() => $"{VendorId:X4}:{ProductId:X4}";

    [GeneratedRegex("VID_([0-9A-F]{4})&PID_([0-9A-F]{4})", RegexOptions.IgnoreCase)]
    private static partial Regex IdRegex();
}
