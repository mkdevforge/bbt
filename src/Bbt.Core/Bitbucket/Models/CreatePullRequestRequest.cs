using System.Text.Json.Serialization;

namespace Bbt.Core.Bitbucket.Models;

public sealed class CreatePullRequestRequest
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }

    [JsonPropertyName("source")]
    public CreatePullRequestEndpoint Source { get; set; } = new();

    [JsonPropertyName("destination")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CreatePullRequestEndpoint? Destination { get; set; }

    [JsonPropertyName("reviewers")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<CreatePullRequestReviewer>? Reviewers { get; set; }

    [JsonPropertyName("close_source_branch")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? CloseSourceBranch { get; set; }

    [JsonPropertyName("draft")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Draft { get; set; }
}

public sealed class CreatePullRequestEndpoint
{
    [JsonPropertyName("branch")]
    public CreatePullRequestBranch Branch { get; set; } = new();

    public static CreatePullRequestEndpoint ForBranch(string name) => new() { Branch = new CreatePullRequestBranch { Name = name } };
}

public sealed class CreatePullRequestBranch
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public sealed class CreatePullRequestReviewer
{
    [JsonPropertyName("uuid")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Uuid { get; set; }

    [JsonPropertyName("account_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AccountId { get; set; }

    /// <summary>
    /// Values wrapped in braces (e.g. "{1234-...}") are treated as UUIDs; anything else as an Atlassian account id.
    /// </summary>
    public static CreatePullRequestReviewer FromIdentifier(string value)
    {
        var trimmed = value.Trim();
        return trimmed.StartsWith('{') && trimmed.EndsWith('}')
            ? new CreatePullRequestReviewer { Uuid = trimmed }
            : new CreatePullRequestReviewer { AccountId = trimmed };
    }
}
