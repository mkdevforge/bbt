using System.Text.Json.Serialization;

namespace Bbt.Core.Bitbucket.Models;

public sealed class BitbucketAccount
{
    [JsonPropertyName("display_name")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("nickname")]
    public string? Nickname { get; set; }

    [JsonPropertyName("uuid")]
    public string? Uuid { get; set; }

    [JsonPropertyName("account_id")]
    public string? AccountId { get; set; }
}

