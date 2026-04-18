using Bbt.Core.Bitbucket.Models;

namespace Bbt.Core.Bitbucket;

public static class PullRequestActivityAnalyzer
{
    public static DateTimeOffset? TryGetMergedAt(IEnumerable<BitbucketPullRequestActivity> activities)
    {
        DateTimeOffset? mergedAt = null;

        foreach (var activity in activities)
        {
            var update = activity.Update;
            if (update?.Date is null || !string.Equals(update.State, "MERGED", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (mergedAt is null || update.Date > mergedAt)
            {
                mergedAt = update.Date;
            }
        }

        return mergedAt;
    }
}
