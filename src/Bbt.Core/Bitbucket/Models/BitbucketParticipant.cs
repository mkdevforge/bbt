using System.Text.Json.Serialization;

namespace Bbt.Core.Bitbucket.Models;

public sealed class BitbucketParticipant
{
    [JsonPropertyName("user")]
    public BitbucketAccount? User { get; set; }

    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("approved")]
    public bool? Approved { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("participated_on")]
    public DateTimeOffset? ParticipatedOn { get; set; }
}

