using SwitchToKVM.Core.Updates;

namespace SwitchToKVM.Core.Tests;

public class GitHubUpdatesTests
{
    private static readonly Version Current = new(0, 1, 0, 0);

    private static GitHubRelease Release(string tag, params GitHubAsset[] assets) =>
        new(tag, $"https://github.com/owner/repo/releases/tag/{tag}", assets);

    private static readonly GitHubAsset Setup = new("SwitchToKVM-0.2.0-setup.exe", "https://example.com/setup.exe", "sha256:ABC123");
    private static readonly GitHubAsset Zip = new("SwitchToKVM-0.2.0-portable.zip", "https://example.com/portable.zip", "sha256:DEF456");

    [Fact]
    public void FindsNewerReleaseWithInstaller()
    {
        var update = GitHubUpdates.Newer(Release("v0.2.0", Zip, Setup), Current);

        Assert.NotNull(update);
        Assert.Equal(new Version(0, 2, 0), update.Version);
        Assert.Equal("https://example.com/setup.exe", update.InstallerUrl);
        Assert.Equal("ABC123", update.InstallerSha256);
    }

    [Theory]
    [InlineData("v0.1.0")]
    [InlineData("v0.0.9")]
    [InlineData("not-a-version")]
    public void IgnoresSameOlderOrUnreadableTags(string tag) => Assert.Null(GitHubUpdates.Newer(Release(tag, Setup), Current));

    [Fact]
    public void HasNoInstallerWithoutDigest()
    {
        var update = GitHubUpdates.Newer(Release("v0.2.0", Setup with { Digest = null }), Current);

        Assert.NotNull(update);
        Assert.Null(update.InstallerSha256);
    }
}
