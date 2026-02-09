using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bbt.Infrastructure;

public static class BbtJson
{
    public static readonly JsonSerializerOptions OutputSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
        },
    };
}

