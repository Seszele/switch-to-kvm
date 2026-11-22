using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SwitchToKVM.Core.Configuration;

internal sealed class HexConverter<T> : JsonConverter<T> where T : IBinaryInteger<T>
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String &&
            T.TryParse(reader.GetString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
            return value;
        throw new JsonException("Expected a hex string such as \"0F\"");
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString($"X{value.GetByteCount() * 2}", CultureInfo.InvariantCulture));
}
