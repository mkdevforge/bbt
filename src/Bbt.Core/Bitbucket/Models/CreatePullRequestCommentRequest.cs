using System.Text.Json.Serialization;

namespace Bbt.Core.Bitbucket.Models;

public sealed class CreatePullRequestCommentRequest
{
    [JsonPropertyName("content")]
    public CreatePullRequestCommentContent Content { get; set; } = new();

    [JsonPropertyName("parent")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CreatePullRequestCommentParent? Parent { get; set; }

    [JsonPropertyName("inline")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CreatePullRequestCommentInline? Inline { get; set; }
}

public sealed class CreatePullRequestCommentContent
{
    [JsonPropertyName("raw")]
    public string Raw { get; set; } = string.Empty;
}

public sealed class CreatePullRequestCommentParent
{
    [JsonPropertyName("id")]
    public long Id { get; set; }
}

public sealed class CreatePullRequestCommentInline
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("to")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? To { get; set; }

    [JsonPropertyName("from")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? From { get; set; }

    [JsonPropertyName("start_to")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? StartTo { get; set; }

    [JsonPropertyName("start_from")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? StartFrom { get; set; }
}
