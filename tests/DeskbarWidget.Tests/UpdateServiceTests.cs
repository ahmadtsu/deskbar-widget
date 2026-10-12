using System.Net;
using System.Text;
using DeskbarWidget.Core;

namespace DeskbarWidget.Tests;

public sealed class UpdateServiceTests
{
    [Theory]
    [InlineData("v1.0.2", 1, 0, 2)]
    [InlineData("1.0.2", 1, 0, 2)]
    [InlineData("V2.1.0", 2, 1, 0)]
    [InlineData("v1.0.2.4", 1, 0, 2, 4)]
    [InlineData("v3.0", 3, 0)]
    [InlineData("v1.5-preview", 1, 5)]
    public void ParseVersionFromTag_ValidTags_ParsesCorrectly(string tag, int major, int minor, int build = -1, int revision = -1)
    {
        var parsed = UpdateService.ParseVersionFromTag(tag);
        Assert.NotNull(parsed);
        Assert.Equal(major, parsed.Major);
        Assert.Equal(minor, parsed.Minor);
        if (build >= 0)
        {
            Assert.Equal(build, parsed.Build);
        }
        if (revision >= 0)
        {
            Assert.Equal(revision, parsed.Revision);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid-tag")]
    public void ParseVersionFromTag_InvalidTags_ReturnsNull(string? tag)
    {
        var parsed = UpdateService.ParseVersionFromTag(tag);
        Assert.Null(parsed);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenNewerVersionAvailable_ReturnsUpdateAvailable()
    {
        var json = """
        {
            "tag_name": "v1.0.3",
            "name": "Release 1.0.3",
            "html_url": "https://github.com/ahmadtsu/deskbar-widget/releases/tag/v1.0.3",
            "body": "Bug fixes and improvements"
        }
        """;

        using var client = CreateMockHttpClient(HttpStatusCode.OK, json);
        using var service = new UpdateService(httpClient: client);

        var result = await service.CheckForUpdatesAsync(currentVersion: new Version(1, 0, 2));

        Assert.True(result.IsUpdateAvailable);
        Assert.False(result.IsSkipped);
        Assert.Equal(new Version(1, 0, 3), result.LatestVersion);
        Assert.Equal("v1.0.3", result.LatestTag);
        Assert.Equal("https://github.com/ahmadtsu/deskbar-widget/releases/tag/v1.0.3", result.ReleaseUrl);
        Assert.Equal("Release 1.0.3", result.ReleaseTitle);
        Assert.Equal("Bug fixes and improvements", result.ReleaseNotes);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenCurrentVersionIsSameOrNewer_ReturnsUpToDate()
    {
        var json = """
        {
            "tag_name": "v1.0.2",
            "name": "Release 1.0.2",
            "html_url": "https://github.com/ahmadtsu/deskbar-widget/releases/tag/v1.0.2"
        }
        """;

        using var client = CreateMockHttpClient(HttpStatusCode.OK, json);
        using var service = new UpdateService(httpClient: client);

        // Same version
        var resultSame = await service.CheckForUpdatesAsync(currentVersion: new Version(1, 0, 2));
        Assert.False(resultSame.IsUpdateAvailable);
        Assert.Equal(new Version(1, 0, 2), resultSame.LatestVersion);

        // Newer local version
        var resultNewer = await service.CheckForUpdatesAsync(currentVersion: new Version(1, 0, 3));
        Assert.False(resultNewer.IsUpdateAvailable);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenVersionIsSkipped_ReturnsSkippedUnlessIgnored()
    {
        var json = """
        {
            "tag_name": "v1.0.3",
            "name": "Release 1.0.3",
            "html_url": "https://github.com/ahmadtsu/deskbar-widget/releases/tag/v1.0.3"
        }
        """;

        using var client1 = CreateMockHttpClient(HttpStatusCode.OK, json);
        using var service1 = new UpdateService(httpClient: client1);

        // Normal check with skipped version
        var resultSkipped = await service1.CheckForUpdatesAsync(
            currentVersion: new Version(1, 0, 2),
            skippedVersion: "1.0.3",
            ignoreSkipped: false);

        Assert.False(resultSkipped.IsUpdateAvailable);
        Assert.True(resultSkipped.IsSkipped);

        using var client2 = CreateMockHttpClient(HttpStatusCode.OK, json);
        using var service2 = new UpdateService(httpClient: client2);

        // Manual check ignores skipped flag
        var resultIgnored = await service2.CheckForUpdatesAsync(
            currentVersion: new Version(1, 0, 2),
            skippedVersion: "1.0.3",
            ignoreSkipped: true);

        Assert.True(resultIgnored.IsUpdateAvailable);
        Assert.False(resultIgnored.IsSkipped);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenApiReturnsError_ReturnsFailed()
    {
        using var client = CreateMockHttpClient(HttpStatusCode.NotFound, "Not Found");
        using var service = new UpdateService(httpClient: client);

        var result = await service.CheckForUpdatesAsync(currentVersion: new Version(1, 0, 2));

        Assert.False(result.IsUpdateAvailable);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("404", result.ErrorMessage);
    }

    private static HttpClient CreateMockHttpClient(HttpStatusCode statusCode, string content)
    {
        var handler = new MockHttpMessageHandler(statusCode, content);
        return new HttpClient(handler);
    }

    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _content;

        public MockHttpMessageHandler(HttpStatusCode statusCode, string content)
        {
            _statusCode = statusCode;
            _content = content;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_content, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
