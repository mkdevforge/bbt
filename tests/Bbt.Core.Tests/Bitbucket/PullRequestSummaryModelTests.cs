using Bbt.Core.Bitbucket.Models;
using Bbt.Core.Diff;
using Bbt.Infrastructure;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Bbt.Core.Tests.Bitbucket;

public sealed class PullRequestSummaryModelTests
{
    [Fact]
    public void ModelMappers_ToPullRequestSummary_MapsCountsBranchesAndMergedAt()
    {
        var pr = new BitbucketPullRequest
        {
            Id = 123,
            Title = "Add summary command",
            State = "MERGED",
            Author = new BitbucketAccount
            {
                DisplayName = "Author Name",
                Nickname = "author",
                Uuid = "{author}",
            },
            Source = new BitbucketPullRequestEndpoint
            {
                Branch = new BitbucketBranch { Name = "feature/summary" },
            },
            Destination = new BitbucketPullRequestEndpoint
            {
                Branch = new BitbucketBranch { Name = "main" },
            },
            CreatedOn = DateTimeOffset.Parse("2026-04-01T12:34:56Z"),
            UpdatedOn = DateTimeOffset.Parse("2026-04-02T13:10:00Z"),
            CommentCount = 14,
            Reviewers =
            [
                new BitbucketAccount
                {
                    DisplayName = "Reviewer Name",
                    Nickname = "reviewer",
                    Uuid = "{reviewer}",
                },
            ],
            Participants =
            [
                new BitbucketParticipant
                {
                    Approved = true,
                    State = "approved",
                },
                new BitbucketParticipant
                {
                    Approved = false,
                    State = "changes_requested",
                },
                new BitbucketParticipant
                {
                    Approved = false,
                    State = "needs_work",
                },
                new BitbucketParticipant
                {
                    Approved = false,
                    State = null,
                },
            ],
        };

        var summary = ModelMappers.ToPullRequestSummary(
            pr,
            workspace: "my-workspace",
            repo: "my-repo",
            diffStats: new PullRequestDiffStats(FilesChanged: 5, LinesAdded: 120, LinesRemoved: 37),
            mergedAt: DateTimeOffset.Parse("2026-04-02T13:00:00Z"));

        Assert.Equal(123, summary.PrId);
        Assert.Equal("my-workspace", summary.Workspace);
        Assert.Equal("my-repo", summary.Repo);
        Assert.Equal("feature/summary", summary.SourceBranch);
        Assert.Equal("main", summary.TargetBranch);
        Assert.Equal(14, summary.CommentCount);
        Assert.Equal(5, summary.FilesChanged);
        Assert.Equal(120, summary.LinesAdded);
        Assert.Equal(37, summary.LinesRemoved);
        Assert.Equal(1, summary.Approvals);
        Assert.Equal(2, summary.ChangesRequested);
        Assert.Equal(DateTimeOffset.Parse("2026-04-02T13:00:00Z"), summary.MergedAt);
        Assert.Single(summary.Reviewers);
        Assert.Equal("Author Name", summary.Author!.DisplayName);
    }

    [Fact]
    public void PullRequestSummary_Serialization_IncludesMergedAtWhenNull()
    {
        var summary = new Bbt.Models.PullRequestSummary(
            PrId: 1,
            Workspace: "mkdevforge",
            Repo: "bbt-demo",
            Title: "Test",
            State: "OPEN",
            Author: null,
            SourceBranch: "test-pr",
            TargetBranch: "main",
            HtmlUrl: "https://bitbucket.org/mkdevforge/bbt-demo/pull-requests/1",
            OpenedAt: DateTimeOffset.Parse("2026-02-09T21:30:28.727793+00:00"),
            UpdatedAt: DateTimeOffset.Parse("2026-02-13T16:17:08.848810+00:00"),
            MergedAt: null,
            Reviewers: [],
            Approvals: 0,
            ChangesRequested: 0,
            CommentCount: 111,
            FilesChanged: 14,
            LinesAdded: 839,
            LinesRemoved: 112);

        var json = JsonSerializer.Serialize(summary, BbtJson.OutputSerializerOptions);
        var node = JsonNode.Parse(json)!.AsObject();

        Assert.True(node.ContainsKey("mergedAt"));
        Assert.Null(node["mergedAt"]);
    }
}
