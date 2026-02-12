using System.Net;
using System.Text;
using Bbt.Core.Bitbucket;

namespace Bbt.Core.Tests.Bitbucket;

public sealed class BitbucketClientCommentsTests
{
    [Fact]
    public async Task ListPullRequestCommentsAsync_BuildsQueryParams()
    {
        var handler = new CapturingHandler();
        using var client = new BitbucketClient(
            new BitbucketClientOptions(
                BaseUri: new Uri("https://api.bitbucket.org/2.0/"),
                Email: "test@example.com",
                Token: "token",
                Verbose: false,
                NoRetry: true,
                VerboseLog: null),
            handler);

        await client.ListPullRequestCommentsAsync(
            workspace: "my-ws",
            repo: "my-repo",
            pullRequestId: 123,
            pageLen: 10,
            page: 2,
            sort: "-created_on",
            q: "content.raw~\"AI Code Review\"",
            cancellationToken: default);

        var uri = handler.LastRequest?.RequestUri;
        Assert.NotNull(uri);

        var query = ParseQuery(uri!);
        Assert.Equal("10", query["pagelen"]);
        Assert.Equal("2", query["page"]);
        Assert.Equal("-created_on", query["sort"]);
        Assert.Equal("content.raw~\"AI Code Review\"", query["q"]);
    }

    private static Dictionary<string, string?> ParseQuery(Uri uri)
    {
        var dict = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var query = uri.Query;
        if (string.IsNullOrWhiteSpace(query))
        {
            return dict;
        }

        if (query.StartsWith("?", StringComparison.Ordinal))
        {
            query = query[1..];
        }

        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', count: 2);
            var key = Uri.UnescapeDataString(parts[0]);
            var value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : null;
            dict[key] = value;
        }

        return dict;
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            var json = "{\"pagelen\":10,\"page\":2,\"size\":0,\"values\":[]}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
        }
    }
}
