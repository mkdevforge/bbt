using System.Text.Json.Nodes;
using Bbt.Core.Json;
using Xunit;

namespace Bbt.Core.Tests.Json;

public sealed class JsonFieldSelectorTests
{
    [Fact]
    public void Apply_SelectsFieldsFromObject()
    {
        var node = JsonNode.Parse("""{"id":1,"title":"x","state":"OPEN"}""")!;
        var result = JsonFieldSelector.Apply(node, ["id", "state"]);

        Assert.Equal("""{"id":1,"state":"OPEN"}""", result.ToJsonString());
    }

    [Fact]
    public void Apply_SelectsFieldsFromArray()
    {
        var node = JsonNode.Parse("""[{"id":1,"title":"a"},{"id":2,"title":"b"}]""")!;
        var result = JsonFieldSelector.Apply(node, ["id"]);

        Assert.Equal("""[{"id":1},{"id":2}]""", result.ToJsonString());
    }

    [Fact]
    public void Apply_Array_AllowsOptionalFieldPerItem()
    {
        var node = JsonNode.Parse("""[{"id":1,"body":"a"},{"id":2,"inline":{"path":"README.md","to":10}}]""")!;
        var result = JsonFieldSelector.Apply(node, ["id", "inline"]);

        Assert.Equal("""[{"id":1,"inline":null},{"id":2,"inline":{"path":"README.md","to":10}}]""", result.ToJsonString());
    }

    [Fact]
    public void Apply_ThrowsOnUnknownField()
    {
        var node = JsonNode.Parse("""{"id":1,"title":"x"}""")!;
        var ex = Assert.Throws<InvalidOperationException>(() => JsonFieldSelector.Apply(node, ["missing"]));
        Assert.Contains("Unknown field", ex.Message);
    }

    [Fact]
    public void Apply_Array_ThrowsWhenFieldMissingFromAllItems()
    {
        var node = JsonNode.Parse("""[{"id":1,"title":"a"},{"id":2,"title":"b"}]""")!;
        var ex = Assert.Throws<InvalidOperationException>(() => JsonFieldSelector.Apply(node, ["id", "inline"]));
        Assert.Contains("Unknown field", ex.Message);
    }
}
