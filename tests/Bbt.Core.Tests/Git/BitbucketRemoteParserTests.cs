using Bbt.Core.Git;
using Xunit;

namespace Bbt.Core.Tests.Git;

public sealed class BitbucketRemoteParserTests
{
    [Theory]
    [InlineData("https://bitbucket.org/my-ws/my-repo.git", "my-ws", "my-repo")]
    [InlineData("https://bitbucket.org/my-ws/my-repo", "my-ws", "my-repo")]
    [InlineData("git@bitbucket.org:my-ws/my-repo.git", "my-ws", "my-repo")]
    [InlineData("ssh://git@bitbucket.org/my-ws/my-repo.git", "my-ws", "my-repo")]
    public void TryParse_ParsesWorkspaceAndRepo(string origin, string expectedWorkspace, string expectedRepo)
    {
        Assert.True(BitbucketRemoteParser.TryParse(origin, out var ws, out var repo));
        Assert.Equal(expectedWorkspace, ws);
        Assert.Equal(expectedRepo, repo);
    }

    [Fact]
    public void TryParse_RejectsNonBitbucketOrigins()
    {
        Assert.False(BitbucketRemoteParser.TryParse("https://github.com/org/repo.git", out _, out _));
    }
}

