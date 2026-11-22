using System.Text.Json;
using System.Text.Json.Serialization;

namespace SwitchToKVM.Core.Configuration;

public static class ConfigFile
{
    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SwitchToKVM", "config.json");

    public static Config? Load(string path)
    {
        if (!File.Exists(path)) return null;
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize(stream, ConfigJsonContext.Default.Config)
               ?? throw new JsonException("Config file is empty");
    }

    public static void Save(Config config, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(config, ConfigJsonContext.Default.Config));
        File.Move(temp, path, overwrite: true);
    }
}

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true)]
[JsonSerializable(typeof(Config))]
internal partial class ConfigJsonContext : JsonSerializerContext;
