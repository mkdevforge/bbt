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
            return ApplyToArrayOrThrow(array, fields);
        }

        return ApplyToObjectOrThrow(node, fields);
    }

    private static JsonArray ApplyToArrayOrThrow(JsonArray array, IReadOnlyList<string> fields)
    {
        var output = new JsonArray();
        var allowedFields = new HashSet<string>(StringComparer.Ordinal);

        foreach (var element in array)
        {
            if (element is null)
            {
                output.Add(null);
                continue;
            }

            if (element is not JsonObject obj)
            {
                throw new InvalidOperationException("--fields can only be used when JSON output is an object or an array of objects.");
            }

            foreach (var key in obj.Select(kvp => kvp.Key))
            {
                allowedFields.Add(key);
            }
        }

        if (allowedFields.Count > 0)
        {
            EnsureFieldsExist(fields, allowedFields);
        }

        foreach (var element in array)
        {
            if (element is null)
            {
                output.Add(null);
                continue;
            }

            output.Add(ProjectObject((JsonObject)element, fields, allowMissing: true));
        }

        return output;
    }

    private static JsonObject ApplyToObjectOrThrow(JsonNode node, IReadOnlyList<string> fields)
    {
        if (node is not JsonObject obj)
        {
            throw new InvalidOperationException("--fields can only be used when JSON output is an object or an array of objects.");
        }

        EnsureFieldsExist(fields, obj.Select(kvp => kvp.Key));
        return ProjectObject(obj, fields, allowMissing: false);
    }

    private static JsonObject ProjectObject(JsonObject obj, IReadOnlyList<string> fields, bool allowMissing)
    {
        var output = new JsonObject();
        foreach (var field in fields)
        {
            if (!obj.TryGetPropertyValue(field, out var value))
            {
                if (!allowMissing)
                {
                    var allowed = string.Join(",", obj.Select(kvp => kvp.Key).OrderBy(x => x));
                    throw new InvalidOperationException($"Unknown field '{field}'. Allowed fields: {allowed}");
                }

                output[field] = null;
                continue;
            }

            output[field] = value?.DeepClone();
        }

        return output;
    }

    private static void EnsureFieldsExist(IReadOnlyList<string> fields, IEnumerable<string> allowedFieldNames)
    {
        var allowed = allowedFieldNames.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var allowedSet = new HashSet<string>(allowed, StringComparer.Ordinal);

        foreach (var field in fields)
        {
            if (!allowedSet.Contains(field))
            {
                throw new InvalidOperationException($"Unknown field '{field}'. Allowed fields: {string.Join(",", allowed)}");
            }
        }
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
