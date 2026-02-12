using System.Text.Json.Serialization;

namespace Bbt.Core.Bitbucket.Models;

public sealed class BitbucketComment
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("content")]
    public BitbucketCommentContent? Content { get; set; }

    [JsonPropertyName("inline")]
    public BitbucketCommentInline? Inline { get; set; }

    [JsonPropertyName("user")]
    public BitbucketAccount? User { get; set; }

    [JsonPropertyName("parent")]
    public BitbucketCommentParent? Parent { get; set; }

    [JsonPropertyName("created_on")]
    public DateTimeOffset? CreatedOn { get; set; }

    [JsonPropertyName("updated_on")]
    public DateTimeOffset? UpdatedOn { get; set; }

    [JsonPropertyName("deleted")]
    public bool? Deleted { get; set; }

    [JsonPropertyName("links")]
    public BitbucketLinks? Links { get; set; }
}

public sealed class BitbucketCommentParent
{
    [JsonPropertyName("id")]
    public long Id { get; set; }
}

public sealed class BitbucketCommentContent
{
    [JsonPropertyName("raw")]
    public string? Raw { get; set; }

    [JsonPropertyName("markup")]
    public string? Markup { get; set; }

    [JsonPropertyName("html")]
    public string? Html { get; set; }
}

public sealed class BitbucketCommentInline
{
    [JsonPropertyName("path")]
    public string? Path { get; set; }

    [JsonPropertyName("to")]
    public int? To { get; set; }

    [JsonPropertyName("from")]
    public int? From { get; set; }

    [JsonPropertyName("start_to")]
    public int? StartTo { get; set; }

    [JsonPropertyName("start_from")]
    public int? StartFrom { get; set; }
}
