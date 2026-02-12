using System.Text.Json;
using Bbt.Core.Bitbucket.Models;
using Bbt.Infrastructure;

namespace Bbt.Core.Tests.Bitbucket;

public sealed class BitbucketCommentModelTests
{
    [Fact]
    public void BitbucketComment_DeserializesLargeIdAndParentId()
    {
        const string json = """
            {
              "id": 3000000000,
              "content": { "raw": "hello" },
              "created_on": "2026-02-12T00:00:00Z",
              "updated_on": "2026-02-12T01:00:00Z",
              "deleted": false,
              "parent": { "id": 3000000001 }
            }
            """;

        var comment = JsonSerializer.Deserialize<BitbucketComment>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(comment);
        Assert.Equal(3000000000L, comment!.Id);
        Assert.Equal(3000000001L, comment.Parent!.Id);
        Assert.Equal(DateTimeOffset.Parse("2026-02-12T01:00:00Z"), comment.UpdatedOn);
        Assert.False(comment.Deleted);
    }

    [Fact]
    public void ModelMappers_ToPullRequestComment_MapsParentAndTimestamps()
    {
        var comment = new BitbucketComment
        {
            Id = 42,
            Content = new BitbucketCommentContent { Raw = "hi" },
            CreatedOn = DateTimeOffset.Parse("2026-02-12T00:00:00Z"),
            UpdatedOn = DateTimeOffset.Parse("2026-02-12T01:00:00Z"),
            Deleted = true,
            Parent = new BitbucketCommentParent { Id = 7 },
        };

        var mapped = ModelMappers.ToPullRequestComment(comment);
        Assert.Equal(42L, mapped.Id);
        Assert.Equal(7L, mapped.ParentId);
        Assert.Equal(DateTimeOffset.Parse("2026-02-12T00:00:00Z"), mapped.CreatedOn);
        Assert.Equal(DateTimeOffset.Parse("2026-02-12T01:00:00Z"), mapped.UpdatedOn);
        Assert.True(mapped.Deleted);
    }
}

