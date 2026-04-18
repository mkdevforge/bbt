using Bbt.Core.Bitbucket;
using Bbt.Core.Bitbucket.Models;

namespace Bbt.Core.Tests.Bitbucket;

public sealed class PullRequestActivityAnalyzerTests
{
    [Fact]
    public void TryGetMergedAt_ReturnsLatestMergedUpdateTimestamp()
    {
        var activities = new[]
        {
            new BitbucketPullRequestActivity
            {
                Update = new BitbucketPullRequestActivityUpdate
                {
                    State = "OPEN",
                    Date = DateTimeOffset.Parse("2026-04-01T12:00:00Z"),
                },
            },
            new BitbucketPullRequestActivity
            {
                Update = new BitbucketPullRequestActivityUpdate
                {
                    State = "MERGED",
                    Date = DateTimeOffset.Parse("2026-04-02T13:00:00Z"),
                },
            },
            new BitbucketPullRequestActivity
            {
                Update = new BitbucketPullRequestActivityUpdate
                {
                    State = "merged",
                    Date = DateTimeOffset.Parse("2026-04-02T13:05:00Z"),
                },
            },
        };

        var mergedAt = PullRequestActivityAnalyzer.TryGetMergedAt(activities);

        Assert.Equal(DateTimeOffset.Parse("2026-04-02T13:05:00Z"), mergedAt);
    }

    [Fact]
    public void TryGetMergedAt_ReturnsNullWhenMergedUpdateIsMissing()
    {
        var activities = new[]
        {
            new BitbucketPullRequestActivity
            {
                Update = new BitbucketPullRequestActivityUpdate
                {
                    State = "OPEN",
                    Date = DateTimeOffset.Parse("2026-04-01T12:00:00Z"),
                },
            },
        };

        var mergedAt = PullRequestActivityAnalyzer.TryGetMergedAt(activities);

        Assert.Null(mergedAt);
    }
}
