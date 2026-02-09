using Bbt.Core.Bitbucket.Models;

namespace Bbt.Core.Bitbucket;

public static class BitbucketPager
{
    public static async Task<List<T>> GetAllAsync<T>(
        Func<string?, CancellationToken, Task<BitbucketPaginated<T>>> getPageAsync,
        int? limit,
        CancellationToken cancellationToken = default)
    {
        var results = new List<T>();
        string? next = null;

        while (true)
        {
            var page = await getPageAsync(next, cancellationToken);
            foreach (var item in page.Values)
            {
                results.Add(item);
                if (limit is not null && results.Count >= limit.Value)
                {
                    return results;
                }
            }

            if (string.IsNullOrWhiteSpace(page.Next))
            {
                return results;
            }

            next = page.Next;
        }
    }
}

