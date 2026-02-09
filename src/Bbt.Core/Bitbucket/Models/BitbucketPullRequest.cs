using System.Text.Json.Serialization;

namespace Bbt.Core.Bitbucket.Models;

public sealed class BitbucketPullRequest
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("author")]
    public BitbucketAccount? Author { get; set; }

    [JsonPropertyName("source")]
    public BitbucketPullRequestEndpoint? Source { get; set; }

    [JsonPropertyName("destination")]
    public BitbucketPullRequestEndpoint? Destination { get; set; }

    [JsonPropertyName("links")]
    public BitbucketLinks? Links { get; set; }

    [JsonPropertyName("created_on")]
    public DateTimeOffset? CreatedOn { get; set; }

    [JsonPropertyName("updated_on")]
    public DateTimeOffset? UpdatedOn { get; set; }

    [JsonPropertyName("reviewers")]
    public List<BitbucketAccount>? Reviewers { get; set; }

    [JsonPropertyName("participants")]
    public List<BitbucketParticipant>? Participants { get; set; }
}

public sealed class BitbucketPullRequestEndpoint
{
    [JsonPropertyName("branch")]
    public BitbucketBranch? Branch { get; set; }
}

public sealed class BitbucketBranch
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

