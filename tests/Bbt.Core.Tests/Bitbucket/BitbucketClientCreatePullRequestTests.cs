using System.Net;
using System.Text;
using System.Text.Json;
using Bbt.Core.Bitbucket;
using Bbt.Core.Bitbucket.Models;

namespace Bbt.Core.Tests.Bitbucket;

public sealed class BitbucketClientCreatePullRequestTests
{
    [Fact]
    public async Task CreatePullRequestAsync_PostsFullBody()
    {
        var handler = new CapturingHandler();
        using var client = CreateClient(handler);

        var created = await client.CreatePullRequestAsync(
            "my-ws",
            "my-repo",
            new CreatePullRequestRequest
            {
                Title = "Add feature",
                Description = "Details",
                Source = CreatePullRequestEndpoint.ForBranch("feature/x"),
                Destination = CreatePullRequestEndpoint.ForBranch("develop"),
                Reviewers =
                [
                    CreatePullRequestReviewer.FromIdentifier("{11111111-2222-3333-4444-555555555555}"),
                    CreatePullRequestReviewer.FromIdentifier("557058:abc"),
                ],
                CloseSourceBranch = true,
                Draft = true,
            });

        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal("/2.0/repositories/my-ws/my-repo/pullrequests", handler.Uri!.AbsolutePath);
        Assert.Equal(42, created.Id);

        using var doc = JsonDocument.Parse(handler.Body!);
        var root = doc.RootElement;
        Assert.Equal("Add feature", root.GetProperty("title").GetString());
        Assert.Equal("Details", root.GetProperty("description").GetString());
        Assert.Equal("feature/x", root.GetProperty("source").GetProperty("branch").GetProperty("name").GetString());
        Assert.Equal("develop", root.GetProperty("destination").GetProperty("branch").GetProperty("name").GetString());
        Assert.True(root.GetProperty("close_source_branch").GetBoolean());
        Assert.True(root.GetProperty("draft").GetBoolean());

        var reviewers = root.GetProperty("reviewers");
        Assert.Equal("{11111111-2222-3333-4444-555555555555}", reviewers[0].GetProperty("uuid").GetString());
        Assert.False(reviewers[0].TryGetProperty("account_id", out _));
        Assert.Equal("557058:abc", reviewers[1].GetProperty("account_id").GetString());
        Assert.False(reviewers[1].TryGetProperty("uuid", out _));
    }

    [Fact]
    public async Task CreatePullRequestAsync_OmitsUnsetOptionalFields()
    {
        var handler = new CapturingHandler();
        using var client = CreateClient(handler);

        await client.CreatePullRequestAsync(
            "my-ws",
            "my-repo",
            new CreatePullRequestRequest
            {
                Title = "Add feature",
                Source = CreatePullRequestEndpoint.ForBranch("feature/x"),
            });

        using var doc = JsonDocument.Parse(handler.Body!);
        var names = doc.RootElement.EnumerateObject().Select(p => p.Name).ToHashSet();
        Assert.Equal(new HashSet<string> { "title", "source" }, names);
    }

    private static BitbucketClient CreateClient(HttpMessageHandler handler)
    {
        return new BitbucketClient(
            new BitbucketClientOptions(
                BaseUri: new Uri("https://api.bitbucket.org/2.0/"),
                Email: "test@example.com",
                Token: "token",
                Verbose: false,
                NoRetry: true,
                VerboseLog: null),
            handler);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }

        public Uri? Uri { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method;
            Uri = request.RequestUri;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

            var json = "{\"id\":42,\"title\":\"Add feature\",\"state\":\"OPEN\",\"source\":{\"branch\":{\"name\":\"feature/x\"}},\"destination\":{\"branch\":{\"name\":\"develop\"}}}";
            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
        }
    }
}
