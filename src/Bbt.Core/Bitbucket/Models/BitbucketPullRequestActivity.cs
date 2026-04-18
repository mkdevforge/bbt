using System.Text.Json.Serialization;

namespace Bbt.Core.Bitbucket.Models;

public sealed class BitbucketPullRequestActivity
{
    [JsonPropertyName("update")]
    public BitbucketPullRequestActivityUpdate? Update { get; set; }
}

public sealed class BitbucketPullRequestActivityUpdate
{
    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("date")]
    public DateTimeOffset? Date { get; set; }
}
