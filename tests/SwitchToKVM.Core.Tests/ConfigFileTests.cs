using System.Text.Json;
using SwitchToKVM.Core.Configuration;

namespace SwitchToKVM.Core.Tests;

public sealed class ConfigFileTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    private string ConfigPath => Path.Combine(_directory, "config.json");

    private static readonly Config Sample = new(0x3434, 0xD030, "PL2770QS", 0x0F, 0x11);

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void SavesAndLoads()
    {
        ConfigFile.Save(Sample, ConfigPath);
        Assert.Equal(Sample, ConfigFile.Load(ConfigPath));
    }

    [Fact]
    public void WritesReadableHex()
    {
        ConfigFile.Save(Sample, ConfigPath);
        var json = File.ReadAllText(ConfigPath);

        Assert.Contains("\"vendorId\": \"3434\"", json);
        Assert.Contains("\"productId\": \"D030\"", json);
        Assert.Contains("\"monitorModel\": \"PL2770QS\"", json);
        Assert.Contains("\"hereInput\": \"0F\"", json);
        Assert.Contains("\"awayInput\": \"11\"", json);
    }

    [Fact]
    public void MissingFileGivesNull() => Assert.Null(ConfigFile.Load(ConfigPath));

    [Theory]
    [InlineData("""{ "vendorId": "3434", "productId": "D030", "monitorModel": "PL2770QS", "hereInput": "zz", "awayInput": "11" }""")]
    [InlineData("""{ "vendorId": "3434", "productId": "D030", "monitorModel": "PL2770QS", "hereInput": 15, "awayInput": "11" }""")]
    [InlineData("""{ "vendorId": "3434", "productId": "D030", "monitorModel": "PL2770QS", "hereInput": "100", "awayInput": "11" }""")]
    [InlineData("""{ "vendorId": "3434", "productId": "D030", "monitorModel": "PL2770QS", "hereInput": "0F" }""")]
    [InlineData("""{ "vendorId": "3434", "productId": "D030", "monitorModel": null, "hereInput": "0F", "awayInput": "11" }""")]
    [InlineData("not json")]
    [InlineData("null")]
    public void InvalidFileThrows(string json)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(ConfigPath, json);
        Assert.Throws<JsonException>(() => ConfigFile.Load(ConfigPath));
    }
}
