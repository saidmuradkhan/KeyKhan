using PassIn.App.Services;

namespace PassIn.App.Tests;

public sealed class AppReleaseTests
{
    [Fact]
    public void Parses_the_github_release()
    {
        const string json = """
            {
              "tag_name": "v1.2.0",
              "html_url": "https://github.com/saidmuradkhan/PassIn/releases/tag/v1.2.0",
              "assets": [
                { "name": "PassIn.exe", "browser_download_url": "https://example.com/PassIn.exe", "digest": "sha256:00" },
                { "name": "PassIn-Setup.exe", "browser_download_url": "https://example.com/PassIn-Setup.exe", "digest": "sha256:ABCDEF" }
              ]
            }
            """;

        var release = AppRelease.Parse(json);

        Assert.NotNull(release);
        Assert.Equal(new Version(1, 2, 0), release.Version);
        Assert.Equal("https://example.com/PassIn-Setup.exe", release.SetupUrl?.AbsoluteUri);
        Assert.Equal("ABCDEF", release.SetupSha256);
    }

    [Fact]
    public void Release_without_installer_has_no_setup()
    {
        var release = AppRelease.Parse("""{ "tag_name": "v1.0.1", "html_url": "https://github.com/x", "assets": [] }""");

        Assert.Null(release?.SetupUrl);
        Assert.Null(release?.SetupSha256);
    }

    [Theory]
    [InlineData("""{ "tag_name": "nightly", "html_url": "https://github.com/x" }""")]
    [InlineData("""{ "html_url": "https://github.com/x" }""")]
    public void Unusable_releases_are_ignored(string json) => Assert.Null(AppRelease.Parse(json));
}
