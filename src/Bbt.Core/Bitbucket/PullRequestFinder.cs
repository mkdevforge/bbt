using Bbt.Core.Bitbucket.Models;

namespace Bbt.Core.Bitbucket;

public static class PullRequestFinder
{
    public static async Task<List<BitbucketPullRequest>> FindOpenBySourceBranchAsync(
        BitbucketClient client,
        string workspace,
        string repo,
        string sourceBranchName,
        CancellationToken cancellationToken = default)
    {
        var prs = await BitbucketPager.GetAllAsync<BitbucketPullRequest>(
            async (next, ct) =>
            {
                var page = await client.ListPullRequestsAsync(workspace, repo, state: "OPEN", pageUrl: next, cancellationToken: ct);
                return page;
            },
            limit: null,
            cancellationToken);

        return prs
            .Where(pr => pr.Source?.Branch?.Name?.Equals(sourceBranchName, StringComparison.Ordinal) == true)
            .ToList();
    }
}

