using System.Text.Json.Serialization;

namespace Bbt.Core.Config;

public sealed class BbtConfig
{
    [JsonPropertyName("currentProfile")]
    public string CurrentProfile { get; set; } = "default";

    [JsonPropertyName("profiles")]
    public Dictionary<string, BbtProfile> Profiles { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class BbtProfile
{
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("defaultWorkspace")]
    public string? DefaultWorkspace { get; set; }

    [JsonPropertyName("defaultRepo")]
    public string? DefaultRepo { get; set; }

    [JsonPropertyName("baseUrl")]
    public string BaseUrl { get; set; } = "https://api.bitbucket.org/2.0";
}

