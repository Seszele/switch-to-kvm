using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SwitchToKVM.Core.Updates;

public sealed record AvailableUpdate(Version Version, string PageUrl, string? InstallerUrl, string? InstallerSha256);

public static class GitHubUpdates
{
    public const string Repository = "Seszele/switch-to-kvm";
    private const string LatestReleaseUrl = $"https://api.github.com/repos/{Repository}/releases/latest";

    public static Version CurrentVersion { get; } = Normalize(Assembly.GetEntryAssembly()?.GetName().Version ?? new Version());

    public static HttpClient CreateClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SwitchToKVM", CurrentVersion.ToString()));
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return http;
    }

    public static async Task<AvailableUpdate?> CheckAsync(HttpClient http, Version current, CancellationToken cancel = default)
    {
        await using var stream = await http.GetStreamAsync(LatestReleaseUrl, cancel);
        var release = await JsonSerializer.DeserializeAsync(stream, GitHubJsonContext.Default.GitHubRelease, cancel)
                      ?? throw new JsonException("GitHub returned an empty release");
        return Newer(release, current);
    }

    public static async Task<string> DownloadInstallerAsync(HttpClient http, AvailableUpdate update, CancellationToken cancel = default)
    {
        if (update.InstallerUrl is null || update.InstallerSha256 is null)
            throw new InvalidOperationException("This release has no installer to download.");

        var folder = Path.Combine(Path.GetTempPath(), "SwitchToKVM-update");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"SwitchToKVM-{update.Version}-setup.exe");

        await using (var download = await http.GetStreamAsync(update.InstallerUrl, cancel))
        await using (var file = File.Create(path))
            await download.CopyToAsync(file, cancel);

        string hash;
        await using (var file = File.OpenRead(path))
            hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(file, cancel));

        if (!hash.Equals(update.InstallerSha256, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(path);
            throw new InvalidDataException("The downloaded installer doesn't match the checksum GitHub published.");
        }
        return path;
    }

    internal static AvailableUpdate? Newer(GitHubRelease release, Version current)
    {
        var tag = release.TagName.TrimStart('v', 'V').Split('-', '+')[0];
        if (!Version.TryParse(tag, out var version) || Normalize(version) <= Normalize(current)) return null;

        var installer = release.Assets.FirstOrDefault(a => a.Name.EndsWith("-setup.exe", StringComparison.OrdinalIgnoreCase));
        var sha256 = installer?.Digest is { } digest && digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)
            ? digest["sha256:".Length..]
            : null;
        return new AvailableUpdate(Normalize(version), release.HtmlUrl, installer?.BrowserDownloadUrl, sha256);
    }

    private static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0));
}

internal sealed record GitHubRelease(string TagName, string HtmlUrl, IReadOnlyList<GitHubAsset> Assets);

internal sealed record GitHubAsset(string Name, string BrowserDownloadUrl, string? Digest);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(GitHubRelease))]
internal partial class GitHubJsonContext : JsonSerializerContext;
