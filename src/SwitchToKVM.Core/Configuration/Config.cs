using System.Text.Json.Serialization;

namespace SwitchToKVM.Core.Configuration;

public sealed record Config(
    [property: JsonConverter(typeof(HexConverter<ushort>))] ushort VendorId,
    [property: JsonConverter(typeof(HexConverter<ushort>))] ushort ProductId,
    string MonitorModel,
    [property: JsonConverter(typeof(HexConverter<byte>))] byte HereInput,
    [property: JsonConverter(typeof(HexConverter<byte>))] byte AwayInput,
    bool ShowTrayIcon = true);
