using System.Text.Json.Serialization;

namespace Bbt.Core.Bitbucket.Models;

public sealed class BitbucketLink
{
    [JsonPropertyName("href")]
    public string? Href { get; set; }
}

public sealed class BitbucketLinks
{
    [JsonPropertyName("html")]
    public BitbucketLink? Html { get; set; }

    [JsonPropertyName("self")]
    public BitbucketLink? Self { get; set; }
}

