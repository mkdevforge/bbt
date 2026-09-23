using System.Net;
using System.Text;
using Bbt.Core.Bitbucket;
using Bbt.Core.Bitbucket.Models;

namespace Bbt.Core.Tests.Bitbucket;

public sealed class BitbucketClientRetryTests
{
    private const string PullRequestJson = "{\"id\":42,\"title\":\"t\",\"state\":\"OPEN\"}";

    [Fact]
    public async Task Post_GatewayError_IsNotRetried()
    {
        var handler = new SequenceHandler(_ => Respond(HttpStatusCode.BadGateway, "{}"));
        using var client = CreateClient(handler);

        await Assert.ThrowsAsync<BitbucketApiException>(() => client.CreatePullRequestAsync("ws", "repo", CreateRequest()));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Post_NetworkError_IsNotRetried_AndSaysOutcomeUnknown()
    {
        var handler = new SequenceHandler(_ => throw new HttpRequestException("connection reset"));
        using var client = CreateClient(handler);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => client.CreatePullRequestAsync("ws", "repo", CreateRequest()));
        Assert.Equal(1, handler.Calls);
        Assert.Contains("may already have been applied", ex.Message);
    }

    [Fact]
    public async Task Post_TooManyRequests_IsRetried()
    {
        var handler = new SequenceHandler(call => call == 1
            ? Respond(HttpStatusCode.TooManyRequests, "{}", retryAfterSeconds: 0)
            : Respond(HttpStatusCode.Created, PullRequestJson));
        using var client = CreateClient(handler);

        var created = await client.CreatePullRequestAsync("ws", "repo", CreateRequest());
        Assert.Equal(42, created.Id);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task Get_GatewayError_IsRetried()
    {
        var handler = new SequenceHandler(call => call == 1
            ? Respond(HttpStatusCode.ServiceUnavailable, "{}", retryAfterSeconds: 0)
            : Respond(HttpStatusCode.OK, PullRequestJson));
        using var client = CreateClient(handler);

        var pr = await client.GetPullRequestAsync("ws", "repo", 42);
        Assert.Equal(42, pr.Id);
        Assert.Equal(2, handler.Calls);
    }

    private static CreatePullRequestRequest CreateRequest()
    {
        return new CreatePullRequestRequest { Title = "t", Source = CreatePullRequestEndpoint.ForBranch("feature/x") };
    }

    private static HttpResponseMessage Respond(HttpStatusCode status, string json, int? retryAfterSeconds = null)
    {
        var response = new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        if (retryAfterSeconds is not null)
        {
            response.Headers.TryAddWithoutValidation("Retry-After", retryAfterSeconds.Value.ToString());
        }

        return response;
    }

    private static BitbucketClient CreateClient(HttpMessageHandler handler)
    {
        return new BitbucketClient(
            new BitbucketClientOptions(
                BaseUri: new Uri("https://api.bitbucket.org/2.0/"),
                Email: "test@example.com",
                Token: "token",
                Verbose: false,
                NoRetry: false,
                VerboseLog: null),
            handler);
    }

    private sealed class SequenceHandler(Func<int, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(respond(Calls));
        }
    }
}
