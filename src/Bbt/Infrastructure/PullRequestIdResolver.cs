using Bbt.Core.Bitbucket;
using Bbt.Core.Git;

namespace Bbt.Infrastructure;

public static class PullRequestIdResolver
{
    public static async Task<int> ResolveAsync(
        BitbucketClient client,
        GitClient gitClient,
        string workspace,
        string repo,
        int? id,
        CancellationToken cancellationToken = default)
    {
        if (id is not null)
        {
            return id.Value;
        }

        if (!await gitClient.IsInsideWorkTreeAsync(cancellationToken))
        {
            throw new InvalidOperationException("PR id is required when not inside a git repository.");
        }

        var branch = await gitClient.TryGetCurrentBranchAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(branch))
        {
            throw new InvalidOperationException("PR id is required when on a detached HEAD or when the current branch cannot be determined.");
        }

        var matches = await PullRequestFinder.FindOpenBySourceBranchAsync(client, workspace, repo, branch, cancellationToken);
        if (matches.Count == 0)
        {
            throw new InvalidOperationException($"No open pull request found for branch '{branch}'. Use `bbt pr list` or specify an explicit id.");
        }

        if (matches.Count > 1)
        {
            var summary = string.Join(
                "\n",
                matches.Take(10).Select(pr => $"  - {pr.Id}: {pr.Title}"));
            throw new InvalidOperationException($"Multiple open pull requests found for branch '{branch}'. Specify an explicit id.\n{summary}");
        }

        return matches[0].Id;
    }
}

