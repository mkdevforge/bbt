using System.Text.Json;
using System.Text.Json.Nodes;

namespace Bbt.Core.Json;

public static class JsonFieldSelector
{
    public static JsonNode Apply(JsonNode node, IReadOnlyList<string> fields)
    {
        if (fields.Count == 0)
        {
            return node;
        }

        if (node is JsonArray array)
        {
            var output = new JsonArray();
            foreach (var element in array)
            {
                if (element is null)
                {
                    output.Add(null);
                    continue;
                }

                output.Add(ApplyToObjectOrThrow(element, fields));
            }

            return output;
        }

        return ApplyToObjectOrThrow(node, fields);
    }

    private static JsonObject ApplyToObjectOrThrow(JsonNode node, IReadOnlyList<string> fields)
    {
        if (node is not JsonObject obj)
        {
            throw new InvalidOperationException("--fields can only be used when JSON output is an object or an array of objects.");
        }

        var output = new JsonObject();
        foreach (var field in fields)
        {
            if (!obj.TryGetPropertyValue(field, out var value))
            {
                var allowed = string.Join(",", obj.Select(kvp => kvp.Key).OrderBy(x => x));
                throw new InvalidOperationException($"Unknown field '{field}'. Allowed fields: {allowed}");
            }

            output[field] = value?.DeepClone();
        }

        return output;
    }

    public static IReadOnlyList<string> ParseFieldsCsv(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
        {
            return [];
        }

        var parts = csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Where(p => !string.IsNullOrWhiteSpace(p)).ToArray();
    }

    public static JsonNode SerializeToNode<T>(T value, JsonSerializerOptions options)
    {
        var node = JsonSerializer.SerializeToNode(value, options);
        return node ?? new JsonObject();
    }
}

